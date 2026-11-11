using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

// ЛР 03, чекліст: один user store (ApplicationUser), legacy StudyUser не в EF, domain FK ведуть на identity_users.
public sealed class IdentityModelTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task OnlyApplicationUserIsUserStore_AndDomainKeysPointToIdentityUsers()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

        Assert.Null(db.Model.FindEntityType(typeof(StudyUser))); // legacy клас не зареєстрований в EF
        Assert.NotNull(db.Model.FindEntityType(typeof(ApplicationUser)));

        foreach (var (entity, property) in new (Type, string)[]
                 { (typeof(Incident), nameof(Incident.OwnerUserId)),
                   (typeof(IncidentComment), nameof(IncidentComment.AuthorUserId)),
                   (typeof(IncidentStatusHistory), nameof(IncidentStatusHistory.ChangedByUserId)) })
        {
            var type = db.Model.FindEntityType(entity)!;
            var key = type.GetForeignKeys().Single(fk => fk.Properties.Any(p => p.Name == property));
            Assert.Equal(typeof(Guid), key.Properties.Single().ClrType);
            Assert.Equal(typeof(ApplicationUser), key.PrincipalEntityType.ClrType);
            Assert.Equal("identity_users", key.PrincipalEntityType.GetTableName());
        }

        // У БД немає старої таблиці study_users, і жоден зовнішній ключ на неї не посилається.
        var tables = await db.Database.SqlQueryRaw<string>(
            "select table_name as \"Value\" from information_schema.tables where table_schema = 'public'").ToListAsync();
        Assert.DoesNotContain("study_users", tables);
        Assert.Contains("identity_users", tables);
    }
}
