using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

// 1. enum: назва значення, перелік значень, розбір тексту
ReadingKind kind = ReadingKind.Book;
Console.WriteLine(kind.ToString());
Console.WriteLine(string.Join(",", Enum.GetValues<ReadingKind>()));

var ok = Enum.TryParse<ReadingKind>(
    "book", ignoreCase: true, out var parsed);
Console.WriteLine($"{ok} {parsed}");
var numeric = Enum.TryParse<ReadingKind>(
    "99", ignoreCase: true, out var strange);
Console.WriteLine($"{numeric} {strange} {Enum.IsDefined(strange)}");

// 2. record -> JSON: PascalCase у C#, camelCase у JSON
var total = new KindTotal("Book", 24);
var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
Console.WriteLine(JsonSerializer.Serialize(total, web));

// 3. null-оператори
string? missing = null;
Console.WriteLine(missing ?? "запасне значення");
Console.WriteLine(missing is null ? "null" : "є значення");

// 4. вираз колекції, new() без назви типу, іменований аргумент
List<KindTotal> list =
[
    new("Note", 13),
    new(Minutes: 24, Kind: "Book")
];
Console.WriteLine($"{list.Count} {list[1].Kind}");

// 5. метод розширення з тілом-виразом
Console.WriteLine(total.Describe());

public enum ReadingKind { Note, Book, Article }

public sealed record KindTotal(string Kind, int Minutes);

public static class KindTotalExtensions
{
    public static string Describe(this KindTotal item) =>
        $"{item.Kind}: {item.Minutes} хв";
}
