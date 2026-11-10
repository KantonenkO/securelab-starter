using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace SecureLab.Api.Scaffolding;

public sealed class LoginThrottleOptions
{
    // Спроб входу на пару «IP + ім'я з запиту» за вікно.
    public int PermitLimit { get; set; } = 5;
    // Спроб входу з одного IP (будь-які імена) за вікно: захист від password spraying.
    public int IpPermitLimit { get; set; } = 30;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Обмеження частоти входу. Два незалежні лічильники фіксованого вікна:
/// ключ «IP|ім'я» (проти підбору пароля до одного акаунта) та ключ «IP» (проти перебору багатьох імен).
/// Ключ формується з даних запиту, а не з наявності акаунта, тож 429 не розкриває, чи існує користувач.
/// </summary>
public sealed class LoginThrottle : IDisposable
{
    private readonly PartitionedRateLimiter<string> _perAccount;
    private readonly PartitionedRateLimiter<string> _perIp;

    public LoginThrottle(IOptions<LoginThrottleOptions> options)
    {
        var settings = options.Value;
        Window = settings.Window;
        _perAccount = Create(settings.PermitLimit, settings.Window);
        _perIp = Create(settings.IpPermitLimit, settings.Window);
    }

    public TimeSpan Window { get; }

    public bool TryAcquire(string ip, string userName)
    {
        var accountKey = ip + "|" + userName.Trim().ToLowerInvariant();
        // Обидва лічильники зменшуються за кожну спробу; відмова - якщо вичерпано будь-який.
        using var account = _perAccount.AttemptAcquire(accountKey);
        using var perIp = _perIp.AttemptAcquire(ip);
        return account.IsAcquired && perIp.IsAcquired;
    }

    private static PartitionedRateLimiter<string> Create(int permitLimit, TimeSpan window) =>
        PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit, Window = window, QueueLimit = 0, AutoReplenishment = true
            }));

    public void Dispose()
    {
        _perAccount.Dispose();
        _perIp.Dispose();
    }
}
