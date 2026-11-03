using Microsoft.EntityFrameworkCore;

// ToQueryString лише перекладає вираз, не з'єднується з БД.
var options = new DbContextOptionsBuilder<BookContext>()
    .UseNpgsql("Host=localhost;Database=translation_probe")
    .Options;
using var db = new BookContext(options);

var badOrder = db.Books
    .Select(b => new BookCard(b.Title, b.Pages))
    .OrderBy(card => card.Pages);
ExpectTranslationError("record then OrderBy", badOrder);

var badFilter = db.Books
    .Select(b => new BookCard(b.Title, b.Pages))
    .Where(card => card.Pages >= 100);
ExpectTranslationError("record then Where", badFilter);

var goodOrder = db.Books
    .Where(b => b.Pages >= 100)
    .OrderBy(b => b.Pages)
    .ThenBy(b => b.Id)
    .Select(b => new BookCard(b.Title, b.Pages));
ShowSql("order before record", goodOrder);

var badGroupOrder = db.Books
    .GroupBy(b => b.Kind)
    .Select(g => new KindPages(g.Key.ToString(), g.Sum(b => b.Pages)))
    .OrderBy(row => row.Kind);
ExpectTranslationError("group record then OrderBy", badGroupOrder);

var goodGroupOrder = db.Books
    .GroupBy(b => b.Kind)
    .OrderBy(g => g.Key)
    .Select(g => new KindPages(g.Key.ToString(), g.Sum(b => b.Pages)));
ShowSql("group order before record", goodGroupOrder);

// Приклад LINQ у пам'яті; не результат виконання SQL.
var loaded = new List<BookCard>
{
    new("B", 120), new("A", 120), new("C", 200)
};
var sorted = loaded.OrderBy(c => c.Pages).ThenBy(c => c.Title);
Console.WriteLine("MEMORY: " +
    string.Join(",", sorted.Select(c => c.Title)));

static void ExpectTranslationError<T>(
    string label, IQueryable<T> query)
{
    try
    {
        query.ToQueryString();
        throw new Exception("Expected translation error: " + label);
    }
    catch (InvalidOperationException e)
        when (e.Message.Contains("could not be translated"))
    {
        Console.WriteLine("EXPECTED TRANSLATION ERROR: " + label);
    }
}

static void ShowSql<T>(string label, IQueryable<T> query)
{
    Console.WriteLine("SQL OK: " + label);
    Console.WriteLine(query.ToQueryString());
}

public sealed record BookCard(string Title, int Pages);
public sealed record KindPages(string Kind, int Pages);
public enum ReadingKind { Note, Book, Article }
public sealed class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int Pages { get; set; }
    public ReadingKind Kind { get; set; }
}
public sealed class BookContext(DbContextOptions<BookContext> options)
    : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Book>().Property(b => b.Kind)
            .HasConversion<string>();
    }
}
