# Точні фрагменти коду WEB ЛР1

Повні консольні програми мають окремі проєкти. Інші блоки нижче є фрагментами для читання/вставлення в указаному місці методички, не повними файлами starter. Не запускайте весь довідник. Очікування в методичці не є вашими фактичними результатами.

## Розділ 3. Маршрут як ланцюжок доказів

```text
натискання картки інциденту
  → loadIncidentDetails у Client/app.js
  → GET /api/incidents/{id}
  → IncidentEndpoints.GetDetailsAsync
  → IncidentQueries.GetDetailsAsync
  → SecureLabDbContext.Incidents
  → PostgreSQL: incidents
  → IncidentDetailsResponse
  → JSON response
  → renderIncidentDetails
  → DOM-вузли з textContent і document.createTextNode
```

## Розділ 3. Endpoint, маршрутизація та серверна валідація

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Інцидент не знайдено",
  "status": 404,
  "detail": "Інцидент '99999999-9999-9999-9999-999999999999' не існує.",
  "traceId": "00-16b2c1b66dbd194a05026c641ec430a5-7699f311fe926bbc-00"
}
```

## Розділ 3. Endpoint, маршрутизація та серверна валідація

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "status": [
      "Допустимі значення: New, Triaged, InProgress, Resolved, Closed."
    ]
  }
}
```

## Розділ 3. LINQ без БД: бачимо результат кожного кроку

```csharp
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
```

## Розділ 3. LINQ без БД: бачимо результат кожного кроку

```text
Source: 4
Where: R1,R2,R3
Select: Note:16,Book:24,Note:10
Group Note: 16,10
Group Book: 24
Dictionary Book: 24
Dictionary Note: 26
Source after: 4
```

## Розділ 3. Повний перелік категорій: що робити з відсутніми групами

```csharp
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
```

## Розділ 3. Повний перелік категорій: що робити з відсутніми групами

```text
Наявні: KindTotal { Kind = Note, Minutes = 13 }; KindTotal { Kind = Book, Minutes = 12 }
Повний перелік: KindTotal { Kind = Note, Minutes = 13 }; KindTotal { Kind = Book, Minutes = 12 }; KindTotal { Kind = Article, Minutes = 0 }
Порядок enum: Note,Book,Article
Порядок тексту: Article,Book,Note
```

## Розділ 3. Відкладений запит і зафіксований список

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var numbers = new List<int> { 1, 2, 3 };
var query = numbers.Where(n => n % 2 == 0);
var snapshot = query.ToList();
numbers.Add(4);
Console.WriteLine(string.Join(",", query));
Console.WriteLine(string.Join(",", snapshot));
```

## Розділ 3. Той самий синтаксис у EF Core має інше джерело виконання

```csharp
var cards = await db.Books
    .AsNoTracking()
    .Where(book => book.Pages >= 100)
    .Select(book => new BookCard(book.Title, book.Pages))
    .ToListAsync(cancellationToken);
```

## Розділ 3. Сортуйте й фільтруйте до створення record

```csharp
var cards = await db.Books
    .Select(book => new BookCard(book.Title, book.Pages))
    .OrderBy(card => card.Pages)
    .ToListAsync(cancellationToken);
```

## Розділ 3. Сортуйте й фільтруйте до створення record

```csharp
var cards = await db.Books
    .AsNoTracking()
    .Where(book => book.Pages >= 100)
    .OrderBy(book => book.Pages)
    .ThenBy(book => book.Id)
    .Select(book => new BookCard(book.Title, book.Pages))
    .ToListAsync(cancellationToken);
```

## Розділ 3. Сортуйте й фільтруйте до створення record

```csharp
var loaded = await db.Books
    .AsNoTracking()
    .Where(book => book.Pages >= 100)
    .Select(book => new BookCard(book.Title, book.Pages))
    .ToListAsync(cancellationToken);
var cards = loaded
    .OrderBy(card => card.Pages)
    .ThenBy(card => card.Title)
    .ToList();
```

## Розділ 4, етап 1, крок 3. Запустіть PostgreSQL

```text
 ✔ Network secure-lab_default          Created
 ✔ Volume "secure-lab_postgres-data"   Created
 ✔ Container secure-lab-postgres-1     Healthy
```

## Розділ 4, етап 1, крок 3. Запустіть PostgreSQL

```text
NAME                    IMAGE                      SERVICE    STATUS                    PORTS
secure-lab-postgres-1   postgres:18.4-alpine3.24   postgres   Up 20 seconds (healthy)   127.0.0.1:54329->5432/tcp
```

## Розділ 4, етап 1, крок 4. Відновіть залежності та запустіть API

```text
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (...) ...
      SELECT "MigrationId", "ProductVersion"
      FROM "__EFMigrationsHistory"
      ORDER BY "MigrationId";
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20260803173310_InitialCreate'.
```

## Розділ 4, етап 1, крок 4. Відновіть залежності та запустіть API

```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5080
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
```

## Розділ 4, етап 2, крок 1. Оберіть готовий маршрут

```text
натискання картки інциденту
  → loadIncidentDetails у Client/app.js
  → GET /api/incidents/{id}
  → IncidentEndpoints.GetDetailsAsync
  → IncidentQueries.GetDetailsAsync
  → SecureLabDbContext.Incidents
  → PostgreSQL: incidents
  → IncidentDetailsResponse
  → JSON response
  → renderIncidentDetails
  → DOM-вузли з textContent і document.createTextNode
```

## Розділ 4. Розібраний приклад перед етапом 3: одна зміна крізь п’ять файлів

```csharp
public sealed record LatestIncidentResponse(
    string Title,
    DateTimeOffset CreatedAtUtc);
```

## Розділ 4. Розібраний приклад перед етапом 3: одна зміна крізь п’ять файлів

```csharp
public Task<LatestIncidentResponse?> GetLatestAsync(
    CancellationToken cancellationToken)
{
    return dbContext.Incidents
        .AsNoTracking()
        .OrderByDescending(incident => incident.CreatedAtUtc)
        .ThenBy(incident => incident.Id)
        .Select(incident => new LatestIncidentResponse(
            incident.Title,
            incident.CreatedAtUtc))
        .FirstOrDefaultAsync(cancellationToken);
}
```

## Розділ 4. Розібраний приклад перед етапом 3: одна зміна крізь п’ять файлів

```csharp
group.MapGet("/latest", GetLatestAsync)
    .WithName("GetLatestIncident")
    .Produces<LatestIncidentResponse>()
    .ProducesProblem(StatusCodes.Status404NotFound);
```

## Розділ 4. Розібраний приклад перед етапом 3: одна зміна крізь п’ять файлів

```csharp
private static async Task<IResult> GetLatestAsync(
    IncidentQueries queries,
    CancellationToken cancellationToken)
{
    var latest = await queries.GetLatestAsync(cancellationToken);
    return latest is null
        ? Results.Problem(
            title: "Інцидентів ще немає",
            statusCode: StatusCodes.Status404NotFound)
        : Results.Ok(latest);
}
```

## Розділ 4. Розібраний приклад перед етапом 3: одна зміна крізь п’ять файлів

```json
{
  "title": "Перевірка журналу комп'ютерного класу",
  "createdAtUtc": "2026-08-03T08:00:00+00:00"
}
```

## Розділ 4. Розібраний приклад перед етапом 3: одна зміна крізь п’ять файлів

```html
<section class="panel" aria-labelledby="latest-title">
  <h2 id="latest-title">Найновіший інцидент</h2>
  <button id="latest-button" type="button">Показати найновіший</button>
  <p id="latest-result" role="status">Ще не запитували.</p>
</section>
```

## Розділ 4. Розібраний приклад перед етапом 3: одна зміна крізь п’ять файлів

```javascript
const latestButton = document.querySelector("#latest-button");
const latestResult = document.querySelector("#latest-result");

async function loadLatestIncident() {
  latestResult.textContent = "Завантаження…";

  try {
    const latest = await apiFetch("/api/incidents/latest");
    latestResult.textContent =
      `${latest.title} ` + `(створено ${latest.createdAtUtc})`;
  } catch (error) {
    latestResult.textContent = `Помилка: ${error.message}`;
  }
}

latestButton.addEventListener("click", loadLatestIncident);
```

## Розділ 4, етап 3, крок 2. Реалізуйте query; для доброго рівня додайте окремий response DTO

```csharp
public sealed record IncidentSeveritySummaryResponse(
    string Severity,
    int Count);
```

## Розділ 4, етап 3, крок 3. Підключіть endpoint замість baseline `501`

```csharp
.Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()
```

## Розділ 4, етап 3, крок 4. Додайте керований елемент до browser client

```javascript
const item = document.createElement("li");
item.textContent = `${summary.severity}: ${summary.count}`;
summaryList.append(item);
```

## Розділ 4, етап 3, крок 5. Додайте структуроване журналювання

```csharp
logger.LogInformation("Loading incidents with status filter {Status}", status);
```

## Розділ 4, етап 3, крок 5. Додайте структуроване журналювання

```text
info: SecureLab.Api.Application.Incidents.IncidentQueries[0]
      Loading incidents with status filter Triaged
```

## Розділ 6. Артефакт DEL-02: карта архітектури

```text
кнопка summary у Client/index.html
  → handler у Client/app.js
  → GET /api/incidents/severity-summary
  → IncidentEndpoints
  → IncidentQueries
  → SecureLabDbContext.Incidents / таблиця incidents
  → IncidentSeveritySummaryResponse як JSON
  → textContent у списку підсумку
```

## Розділ 10. Сторінка, DOM і подія

```javascript
form.addEventListener("submit", async (event) => {
  event.preventDefault();
  // Підготовка й надсилання HTTP-запиту.
});
```

## Розділ 10. HTTP очима клієнта

```http
POST /api/greetings HTTP/1.1
Host: 127.0.0.1:5078
Content-Type: application/json

{"name":"Олена"}

HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"message":"Вітаємо, Олена!"}
```

## Розділ 10. Як читати готовий fetch

```javascript
const response = await fetch("/api/greetings", {
  method: "POST",
  headers: {
    "Content-Type": "application/json"
  },
  body: JSON.stringify({ name: nameInput.value })
});
```

## Розділ 10. Текстова відповідь у DOM

```javascript
function showGreeting(message) {
  // Крок B4: змініть лише підпис перед значенням message.
  result.textContent = `Відповідь API: ${message}`;
}
```

## Розділ 10. Текстова відповідь у DOM

```javascript
const item = document.createElement("li");
item.textContent = message;
historyList.append(item);
```

## Розділ 10. JavaScript, який ви зустрінете у `Client/app.js` starter

```javascript
// options = {} застосовується, якщо аргумент не передано.
async function apiFetch(path, options = {}) {
  const response = await fetch(path, {
    // ... копіює поля іншого об’єкта.
    headers: {
      Accept: "application/json",
      ...options.headers,
    },
    ...options,
  });

  // ok дорівнює true для кодів 200–299.
  if (!response.ok) {
    // JSON помилки; для іншого формату — null.
    const problem = await response.json().catch(() => null);
    // Виняток обробить catch того, хто викликав apiFetch.
    throw new Error(problem?.title ?? `HTTP ${response.status}`);
  }

  // Читаємо JSON успішної відповіді.
  return response.json();
}
```

## Розділ 10. JavaScript, який ви зустрінете у `Client/app.js` starter

```javascript
try {
  const data = await apiFetch("/api/…");
  // показати data
} catch (error) {
  // показати повідомлення про помилку
}
```

## Розділ 9. Цикл роботи: restore → build → run → запит

```text
Now listening on: http://127.0.0.1:5077
Application started. Press Ctrl+C to shut down.
Hosting environment: Production
Content root path: <шлях до директорії прикладу>
```

## Розділ 9. Цикл роботи: restore → build → run → запит

```text
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"message":"Міст C# працює"}
```

## Розділ 9. Цикл роботи: restore → build → run → запит

```text
Request finished HTTP/1.1 GET http://127.0.0.1:5077/hello - 200 - application/json;+charset=utf-8 27.9650ms
```

## Розділ 9. Мінімальна безпечна зміна

```csharp
internal static class BridgeText
{
    // Крок зміни з мосту: змініть лише цей безпечний навчальний текст.
    internal const string HelloMessage = "Міст C# працює";
}
```

## Розділ 9. Типи, властивості й `null` як частина контракту

```csharp
public sealed class IncidentQuery
{
    [Required]
    public string? Owner { get; init; }
}
```

## Розділ 9. Типи, властивості й `null` як частина контракту

```cpp
struct Counter { int value; };
void increment(Counter counter) { ++counter.value; }
```

## Розділ 9. Типи, властивості й `null` як частина контракту

```csharp
sealed class Counter { public int Value { get; set; } }
static void Increment(Counter counter) => counter.Value++;
```

## Розділ 9. Один інтегрований фрагмент: LINQ, атрибут і впровадження залежностей

```csharp
app.MapGet("/incidents", (string owner, IncidentLookupService service) =>
{
    var incident = service.FindFirst(new IncidentQuery { Owner = owner });

    return incident is null
        ? Results.NotFound()
        : Results.Ok(incident);
});

public sealed class IncidentQuery
{
    [Required]
    public string? Owner { get; init; }
}

public sealed class IncidentSource
{
    public IReadOnlyList<Incident> Items { get; } =
    [
        new(1, "Перевірити журнал входів", "olena", false),
        new(2, "Оновити правила доступу", "maksym", true),
        new(3, "Перевірити резервну копію", "olena", false)
    ];
}

public sealed class IncidentLookupService(IncidentSource source)
{
    public IncidentSummary? FindFirst(IncidentQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return source.Items
            .Where(incident =>
                !incident.IsClosed && incident.Owner == query.Owner)
            .Select(incident =>
                new IncidentSummary(incident.Id, incident.Title))
            .FirstOrDefault();
    }
}

public sealed record Incident(
    int Id,
    string Title,
    string Owner,
    bool IsClosed);
```

## Розділ 9. Один інтегрований фрагмент: LINQ, атрибут і впровадження залежностей

```csharp
builder.Services.AddSingleton<IncidentSource>();
builder.Services.AddSingleton<IncidentLookupService>();
```

## Розділ 9. Асинхронні сигнатури на рівні читання

```csharp
app.MapGet("/delayed-hello", async (
    GreetingService service,
    CancellationToken cancellationToken) =>
{
    var message = await service.GetDelayedGreetingAsync(cancellationToken);
    return Results.Ok(new MessageResponse(message));
});

public sealed class GreetingService
{
    public async Task<string> GetDelayedGreetingAsync(
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        return BridgeText.HelloMessage;
    }
}
```

## Розділ 9. Конструкції C#, які ви зустрінете у starter

```csharp
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
```

## Розділ 9. Конструкції C#, які ви зустрінете у starter

```text
Book
Note,Book,Article
True Book
True 99 False
{"kind":"Book","minutes":24}
запасне значення
null
2 Book
Book: 24 хв
```

## Розділ 8 (додаток А). Механізм

```text
HTML form
  → DOM event handler
  → fetch(method, URL, headers, JSON body)
  → routing → endpoint → request DTO
  → application service → DbContext / DbSet
  → PostgreSQL row
  → response DTO → JSON
  → fetch response → textContent
```

## Розділ 8 (додаток А). Перевірений приклад

```json
[
  {
    "id": "20000000-0000-0000-0000-000000000001",
    "title": "Підозрілий лист із вкладенням",
    "severity": "Medium",
    "status": "Triaged",
    "occurredAtUtc": "2026-08-01T08:30:00+00:00",
    "createdAtUtc": "2026-08-01T09:00:00+00:00"
  }
]
```

## Розділ 15. 6. Структура

```text
src/SecureLab.Api/
  Presentation/   endpoints і зовнішні response contracts
  Application/    сценарії застосунку
  Data/           DbContext, entities, migration, seed/reset
  Client/         HTML/CSS/Vanilla JavaScript
tests/
  SecureLab.Api.Tests/   інтеграційні й security regression приклади
  http/                  ручні HTTP-сценарії
infra/                   локальний PostgreSQL
docs/                    карта архітектури, варіант, матриця доступу, звіт
```
