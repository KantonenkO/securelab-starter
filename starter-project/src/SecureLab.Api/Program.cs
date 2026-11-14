using Microsoft.AspNetCore.Http;
using SecureLab.Api.Scaffolding;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Presentation.Endpoints;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Client"
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("SecureLab")
    ?? throw new InvalidOperationException(
        "Connection string 'SecureLab' не налаштовано.");

builder.Services.AddDbContext<SecureLabDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IncidentQueries>();

builder.AddLab03Identity();
// ЛР 04: resource-based handler-и (рішення для Incident і для child IncidentComment).
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SecureLab.Api.Application.Authorization.IncidentAuthorizationHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SecureLab.Api.Application.Authorization.IncidentCommentAuthorizationHandler>();
// Некоректний JSON у register/incidents дає 400, а не 500 від UseExceptionHandler.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

// ЛР 5: навчальна CORS policy лише для Development. Дозволено єдиний exact origin probe (без / наприкінці).
// 4174 лишається негативним контролем. Production цю policy не реєструє і не застосовує.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Lab05BrowserProbe", policy =>
        {
            policy.WithOrigins("http://localhost:4173")
                  .WithMethods("GET")
                  .WithHeaders("X-Lab-Cors-Probe")
                  .AllowCredentials();
        });
    });
}

var app = builder.Build();

var resetRequested = args.Contains("--reset-database", StringComparer.Ordinal);
await DatabaseBootstrap.InitializeAsync(app.Services, app.Configuration, app.Environment, resetRequested);

if (resetRequested)
{
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// ЛР 5: CSP складено за фактичними ресурсами клієнта (власні scripts/styles/images з того самого origin, API на
// тому самому origin). Без unsafe-inline і зовнішніх джерел; frame-ancestors не має fallback до default-src.
const string csp = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; "
    + "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    // /scalar - Development-довідник на сторонньому UI package з власними inline/зовнішніми ресурсами: окремий виняток,
    // лише в Development; CSP основного клієнта через нього не послаблюється.
    var isDevelopmentApiReference = app.Environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/scalar");
    if (!isDevelopmentApiReference)
    {
        context.Response.Headers["Content-Security-Policy"] = csp;
    }
    await next(context);
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// UseRouting явно і UseCors одразу після нього: CORS працює до authentication/authorization, тож дозволені
// headers є і в 401/403 відповідях.
app.UseRouting();
if (app.Environment.IsDevelopment())
{
    app.UseCors("Lab05BrowserProbe");
}
app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", async (SecureLabDbContext dbContext, CancellationToken cancellationToken) =>
    await dbContext.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.Problem(
            title: "База даних недоступна",
            statusCode: StatusCodes.Status503ServiceUnavailable))
    .WithName("GetHealth")
    .Produces(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapIncidentEndpoints();
app.MapIncidentMutationEndpoints();
app.MapLab03Identity();
app.MapLab02Endpoints();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
