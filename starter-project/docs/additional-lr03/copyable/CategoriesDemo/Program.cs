Console.OutputEncoding = System.Text.Encoding.UTF8;
var entries = new[]
{
    new { Kind = ReadingKind.Note, Minutes = 8 },
    new { Kind = ReadingKind.Book, Minutes = 12 },
    new { Kind = ReadingKind.Note, Minutes = 5 }
};

// Крок 1. Агрегат: лише ті види, що є в даних.
var totals = entries
    .GroupBy(e => e.Kind)
    .Select(g => new KindTotal(g.Key, g.Sum(e => e.Minutes)))
    .ToList();
Console.WriteLine("Наявні: " + string.Join("; ", totals));

// Крок 2. Повний перелік усіх значень enum.
var byKind = totals.ToDictionary(t => t.Kind, t => t.Minutes);
var full = Enum.GetValues<ReadingKind>()
    .Select(kind => new KindTotal(
        kind,
        byKind.TryGetValue(kind, out var minutes) ? minutes : 0))
    .ToList();
Console.WriteLine("Повний перелік: " + string.Join("; ", full));

// Крок 3. Два різні порядки тих самих даних.
Console.WriteLine("Порядок enum: " +
    string.Join(",", full.OrderBy(t => t.Kind).Select(t => t.Kind)));
Console.WriteLine("Порядок тексту: " + string.Join(",", full
    .OrderBy(t => t.Kind.ToString())
    .Select(t => t.Kind)));

public enum ReadingKind { Note, Book, Article }

public sealed record KindTotal(ReadingKind Kind, int Minutes);
