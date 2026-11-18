using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

public sealed class SecureLabApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // Звичайні тести часто входять тим самим акаунтом, тому ліміт входу тут високий;
        // власний тест rate limit перевизначає ці значення на малі (LoginRateLimitTests).
        builder.UseSetting("Auth:LoginThrottle:PermitLimit", "1000");
        builder.UseSetting("Auth:LoginThrottle:IpPermitLimit", "5000");
        // ЛР 6: так само для search і register; власні тести rate limit перевизначають значення на малі.
        builder.UseSetting("RateLimiting:Search:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Register:PermitLimit", "100000");
    }
}
