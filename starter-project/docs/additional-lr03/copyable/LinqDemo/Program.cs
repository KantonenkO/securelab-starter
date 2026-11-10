using System;
using System.Linq;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var entries = new[]
{
    new { Id = "R1", Kind = "Note", Minutes = 8 },
    new { Id = "R2", Kind = "Book", Minutes = 12 },
    new { Id = "R3", Kind = "Note", Minutes = 5 },
    new { Id = "R4", Kind = "Book", Minutes = 3 }
};
Console.WriteLine($"Source: {entries.Length}");

var kept = entries.Where(e => e.Minutes >= 5).ToList();
Console.WriteLine("Where: " + string.Join(",", kept.Select(e => e.Id)));

var projected = kept
    .Select(e => new { e.Kind, Points = e.Minutes * 2 })
    .ToList();
Console.WriteLine("Select: " + string.Join(",",
    projected.Select(e => $"{e.Kind}:{e.Points}")));

var groups = projected.GroupBy(e => e.Kind).ToList();
foreach (var group in groups)
    Console.WriteLine($"Group {group.Key}: " +
        string.Join(",", group.Select(e => e.Points)));

var totals = groups.Select(g => new
{
    Kind = g.Key,
    Points = g.Sum(e => e.Points)
}).ToList();
var byKind = totals.ToDictionary(t => t.Kind, t => t.Points);
foreach (var pair in byKind.OrderBy(p => p.Key))
    Console.WriteLine($"Dictionary {pair.Key}: {pair.Value}");
Console.WriteLine($"Source after: {entries.Length}");
