using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

public static class Lab03Identity
{
    // Межі беремо з моделі БД (SecureLabDbContext): DisplayName - 120, UserName/Email - 256.
    private const int UserNameMin = 3, UserNameMax = 64, DisplayNameMax = 120, EmailMax = 256, PasswordMax = 128;
    private static readonly Regex UserNamePattern = new("^[A-Za-z0-9._@+-]+$", RegexOptions.Compiled);

    public static void AddLab03Identity(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
        }).AddEntityFrameworkStores<SecureLabDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            // Явний строк дії замість неявного: ковзний сеанс на 2 години.
            options.ExpireTimeSpan = TimeSpan.FromHours(2);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        // Після зміни SecurityStamp (зміна пароля, ролі тощо) старі cookie втрачають чинність
        // не пізніше ніж за ValidationInterval (за замовчуванням Identity - 30 хв; тут - 1 хв).
        builder.Services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.FromMinutes(1));
        builder.Services.Configure<LoginThrottleOptions>(builder.Configuration.GetSection("Auth:LoginThrottle"));
        builder.Services.AddSingleton<LoginThrottle>();
    }

    public static void MapLab03Identity(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest input, UserManager<ApplicationUser> users,
            ILoggerFactory loggers) =>
        {
            var log = loggers.CreateLogger("SecureLab.Auth");
            var errors = ValidateRegistration(input);
            if (errors.Count > 0)
            {
                log.LogInformation("auth.register.rejected reason=validation");
                return Results.ValidationProblem(errors);
            }
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = input.UserName!.Trim(), Email = input.Email!.Trim(),
                DisplayName = input.DisplayName!.Trim()
            };
            // Хеш пароля обчислює лише UserManager (стандартний PasswordHasher); власного хешування немає.
            var result = await users.CreateAsync(user, input.Password!);
            if (!result.Succeeded)
            {
                var codes = result.Errors.Select(error => error.Code).ToArray();
                if (codes.Any(code => code is "DuplicateUserName" or "DuplicateEmail"))
                {
                    // 409 без уточнення, що саме зайнято (ім'я чи email).
                    log.LogInformation("auth.register.rejected reason=duplicate");
                    return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                        title: "Registration conflict", detail: "Обліковий запис із такими даними вже існує.");
                }
                log.LogInformation("auth.register.rejected reason=policy");
                return Results.ValidationProblem(MapIdentityErrors(codes));
            }
            var role = await users.AddToRoleAsync(user, DbSeeder.ReporterRole);
            if (!role.Succeeded) throw new InvalidOperationException("Default role assignment failed.");
            log.LogInformation("auth.register.succeeded userId={UserId}", user.Id);
            return Results.Created("/api/me", new RegisterResponse(user.Id, user.UserName!, user.DisplayName));
        });

        app.MapPost("/api/auth/login", async (LoginRequest input, HttpContext http,
            SignInManager<ApplicationUser> signIn, LoginThrottle throttle, ILoggerFactory loggers) =>
        {
            var log = loggers.CreateLogger("SecureLab.Auth");
            var safeName = SafeForLog(input.UserName);
            // Ключ обмеження: IP + ім'я з запиту (а не лише існуючі акаунти), тому 429 не розкриває існування акаунта.
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!throttle.TryAcquire(ip, input.UserName ?? ""))
            {
                log.LogWarning("auth.login.throttled userName={UserName}", safeName);
                http.Response.Headers.RetryAfter = ((int)throttle.Window.TotalSeconds).ToString();
                return Results.Problem(statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Too many attempts", detail: "Забагато спроб входу. Повторіть пізніше.");
            }
            if (string.IsNullOrWhiteSpace(input.UserName) || string.IsNullOrEmpty(input.Password))
            {
                log.LogInformation("auth.login.failed userName={UserName}", safeName);
                return Unauthorized();
            }
            // Пароль перевіряє SignInManager (PasswordHasher), рядки самостійно не порівнюються.
            // Для невідомого імені виконується фіктивна перевірка хешу, щоб вирівняти час відповіді.
            var known = await signIn.UserManager.FindByNameAsync(input.UserName);
            if (known is null)
            {
                signIn.UserManager.PasswordHasher.VerifyHashedPassword(DummyUser, DummyHash, input.Password);
                log.LogInformation("auth.login.failed userName={UserName}", safeName);
                return Unauthorized();
            }
            var result = await signIn.PasswordSignInAsync(known, input.Password, false, false);
            if (!result.Succeeded)
            {
                log.LogInformation("auth.login.failed userName={UserName}", safeName);
                return Unauthorized();
            }
            log.LogInformation("auth.login.succeeded userId={UserId}", known.Id);
            return Results.NoContent();
        });

        app.MapPost("/api/auth/logout", async (HttpContext http, SignInManager<ApplicationUser> signIn,
            ILoggerFactory loggers) =>
        {
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            await signIn.SignOutAsync();
            loggers.CreateLogger("SecureLab.Auth").LogInformation("auth.logout userId={UserId}", userId ?? "anonymous");
            return Results.NoContent();
        });

        // ЛР 03: verified principal замість заявленого X-Demo-UserId. Header ігнорується повністю.
        app.MapGet("/api/me", async (HttpContext http, UserManager<ApplicationUser> users) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                return Results.Unauthorized();
            var user = await users.FindByIdAsync(id.ToString());
            if (user is null) return Results.Unauthorized();
            var roles = http.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).OrderBy(role => role).ToArray();
            return Results.Ok(new MeResponse(user.Id, user.UserName!, user.DisplayName, roles));
        }).RequireAuthorization();

        // Приклад лише mechanics: не повертає identity і не реалізує оцінювану operation.
        app.MapGet("/api/auth/session-check", () => Results.NoContent()).RequireAuthorization();
    }

    private static IResult Unauthorized() => Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
        title: "Unauthorized", detail: "Невірне ім'я користувача або пароль.");

    // Фіктивний користувач і хеш для вирівнювання часу відповіді на невідоме ім'я.
    private static readonly ApplicationUser DummyUser = new() { Id = Guid.Empty, UserName = "dummy", DisplayName = "dummy" };
    private static readonly string DummyHash = new PasswordHasher<ApplicationUser>()
        .HashPassword(DummyUser, "Dummy-Password-For-Timing-1!");

    private static string SafeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "(empty)";
        var cut = value.Length > UserNameMax ? value[..UserNameMax] : value;
        return UserNamePattern.IsMatch(cut) ? cut : "(invalid-characters)";
    }

    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest input)
    {
        var errors = new Dictionary<string, string[]>();
        var userName = input.UserName?.Trim();
        if (string.IsNullOrEmpty(userName)) errors["userName"] = ["Ім'я користувача обов'язкове."];
        else if (userName.Length is < UserNameMin or > UserNameMax)
            errors["userName"] = [$"Довжина імені: від {UserNameMin} до {UserNameMax} символів."];
        else if (!UserNamePattern.IsMatch(userName))
            errors["userName"] = ["Дозволені латинські літери, цифри та символи - . _ @ +."];

        var email = input.Email?.Trim();
        if (string.IsNullOrEmpty(email)) errors["email"] = ["Email обов'язковий."];
        else if (email.Length > EmailMax || !IsEmail(email)) errors["email"] = ["Некоректний email."];

        var displayName = input.DisplayName?.Trim();
        if (string.IsNullOrEmpty(displayName)) errors["displayName"] = ["Відображуване ім'я обов'язкове."];
        else if (displayName.Length > DisplayNameMax)
            errors["displayName"] = [$"Відображуване ім'я не довше {DisplayNameMax} символів."];

        // Пароль не нормалізується; тут лише наявність і верхня межа (захист від надмірно великих тіл).
        if (string.IsNullOrEmpty(input.Password)) errors["password"] = ["Пароль обов'язковий."];
        else if (input.Password.Length > PasswordMax) errors["password"] = [$"Пароль не довший за {PasswordMax} символів."];
        return errors;
    }

    private static bool IsEmail(string value) =>
        MailAddress.TryCreate(value, out var address) && address.Address == value && value.Contains('@');

    // Помилки політики пароля та формату: лише узагальнені повідомлення, без значень із запиту.
    private static Dictionary<string, string[]> MapIdentityErrors(string[] codes)
    {
        var errors = new Dictionary<string, string[]>();
        if (codes.Any(code => code.StartsWith("Password", StringComparison.Ordinal)))
            errors["password"] = ["Пароль не відповідає політиці: щонайменше 10 символів, велика й мала літери, цифра і спецсимвол."];
        if (codes.Contains("InvalidUserName")) errors["userName"] = ["Некоректне ім'я користувача."];
        if (codes.Contains("InvalidEmail")) errors["email"] = ["Некоректний email."];
        if (errors.Count == 0) errors["registration"] = ["Реєстрацію відхилено."];
        return errors;
    }
}

// Невідомі поля (role, id, ownerUserId ...) у register відхиляються; для /api/incidents політика інша (ігнорування).
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterRequest(string? UserName, string? Email, string? DisplayName, string? Password);

public sealed record RegisterResponse(Guid Id, string UserName, string DisplayName);

public sealed record LoginRequest(string? UserName, string? Password);

public sealed record MeResponse(Guid Id, string UserName, string DisplayName, string[] Roles);
