namespace SecureLab.Api.Tests;

public static class TestPassword
{
    // Локальний навчальний пароль береться лише з середовища (не з файлів проєкту).
    public static string Seed => Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    // Інший, але валідний за політикою пароль: не друкується й не зберігається.
    public static string Other => Seed + "-other";
}
