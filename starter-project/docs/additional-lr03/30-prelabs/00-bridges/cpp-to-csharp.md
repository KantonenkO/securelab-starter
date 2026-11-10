# Міст 1. Від C++ до C# та ASP.NET Core

**Статус:** підготовка за потреби; обов’язкова, якщо самоперевірка показала прогалину; не оцінюється  
**Коли опрацьовувати:** після встановлення інструментів і отримання ZIP, до запуску повного стенда; потребу визначайте самостійно за таблицею нижче  
**Орієнтовний час:** 15–20 хвилин читання і 45–70 хвилин практики  


## Навіщо потрібен цей міст

Ви вже вмієте програмувати імперативною мовою, працювали з функціями, класами,
колекціями та винятками. Цей текст не починає програмування заново і не
перетворює C# на «C++ з іншим синтаксисом». Його практична мета — допомогти
зібрати, запустити, прочитати й мінімально змінити готовий ASP.NET Core API.

Міст не є окремою «нульовою» лабораторною роботою. Він не оцінюється, не
замінює самостійну роботу і не є умовою допуску до лекцій 1–3. Перевірте готовність самостійно за таблицею нижче. Якщо хоча б одну дію не можете відтворити без підказки, виконайте міст повністю. Якщо всі дії нижче вже
зрозумілі й відтворюються на вашому комп’ютері, використовуйте матеріал як
короткий довідник.

Незалежні приклади надано в `lab-01-starter.zip` поряд зі `starter-project/`. Розпакуйте ZIP навіть тоді, коли сам starter одержали через приватний remote. Приклади залишаються поза вашим студентським Git-репозиторієм. Правила роботи з Git наведено в [довіднику](../../docs/git-workflow.md).
Незалежний приклад міститься в директорії
[`bridge-csharp-minimal-api`](../../one-time-examples/bridge-csharp-minimal-api/README.md);
перед роботою прочитайте README у цій директорії. Приклад не є стартовим застосунком із планованим тегом `v0.1.0` і не залежить
від його бази даних, Docker, автентифікації чи лабораторного завдання.

## Ознаки готовності до роботи

Після опрацювання мосту ви маєте вміти показати результат, а не лише впізнати
термін.

| Що перевіряється | Де це опрацьовано | Спостережуваний результат |
|---|---|---|
| Знайти створення об’єкта, результат методу і можливе місце винятку | «Типи, властивості й `null` як частина контракту», «Один інтегрований фрагмент: LINQ, атрибут і впровадження залежностей» | Ви вказуєте ці три місця в новому фрагменті приблизно на 20 рядків |
| Виконати `dotnet restore` і `dotnet build` | «Цикл роботи: restore → build → run → запит» | Складання повідомляє `Build succeeded` і `0 Error(s)` |
| Запустити мінімальний API | «Цикл роботи: restore → build → run → запит» | Застосунок слухає вказану локальну адресу |
| Викликати готову кінцеву точку API | «Цикл роботи: restore → build → run → запит» | Ви бачите код стану HTTP і тіло відповіді |
| Змінити безпечний текст, перезапустити й перевірити відповідь | «Мінімальна безпечна зміна» | У відповіді з’являється нове значення |
| Прочитати `Where` → `Select` → `FirstOrDefault` | «Один інтегрований фрагмент: LINQ, атрибут і впровадження залежностей» | Ви пояснюєте результат для відомого списку |
| Розпізнати призначення атрибута | «Один інтегрований фрагмент: LINQ, атрибут і впровадження залежностей» | Ви відрізняєте метадані від механізму, що їх читає |
| Розпізнати залежність, передану через конструктор | «Один інтегрований фрагмент: LINQ, атрибут і впровадження залежностей» | Ви знаходите реєстрацію і пояснюєте, звідки береться об’єкт |

## Цикл роботи: restore → build → run → запит

Відкрийте термінал у корені **розпакованого ZIP**, де поряд видно `starter-project/` і `one-time-examples/`. Якщо ви перебуваєте у `starter-project` із цього самого ZIP, спочатку перейдіть на рівень вище командою `cd ..` (дві крапки). Для клонованого starter відкрийте окремий термінал у розпакованому ZIP. Тоді виконайте:

```bash
cd one-time-examples/bridge-csharp-minimal-api
dotnet --version
dotnet restore
dotnet build --no-restore
dotnet run --no-build --urls http://127.0.0.1:5077
```

`global.json` прикладу задає нижню межу SDK `10.0.100`, `rollForward: latestFeature`, `allowPrerelease: false`. `dotnet --version` тут має показати найвищу встановлену стабільну SDK сімейства `10.0`. Установіть останню стабільну .NET 10 SDK з [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0); перед цим поза проєктом перевірте `dotnet --list-sdks`. За `A compatible .NET SDK was not found` перевірте SDK та власний `global.json` мосту. Якщо в `global.json` прикладу вказано `10.0.302`, оновіть у його об’єкті `sdk` ці три значення, збережіть інші ключі й повторіть команду. Після встановлення SDK перезапустіть IDE та термінал. Повний маршрут IDE й діагностики є в README starter; [правила Microsoft](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) пояснюють вибір SDK.

Команди виконують різну роботу:

- `dotnet restore` визначає залежності й готує потрібні для складання файли;
- `dotnet build --no-restore` компілює проєкт після відновлення залежностей;
- `dotnet run --no-build` запускає складений застосунок, тому після зміни коду
  потрібно знову виконати `build`.

Під час авторської перевірки `restore` і `build` завершилися з кодом виходу `0`,
а складання повідомило `Build succeeded`, `0 Warning(s)` і `0 Error(s)`. Код
виходу останньої команди показує команда `echo $?` у bash/zsh або змінна
`$LASTEXITCODE` у PowerShell.

Після запуску Kestrel — вебсервер, який обслуговує ASP.NET Core застосунок, —
фактично повідомляє:

```text
Now listening on: http://127.0.0.1:5077
Application started. Press Ctrl+C to shut down.
Hosting environment: Production
Content root path: <шлях до директорії прикладу>
```

Перед кожним повідомленням стоїть рядок `info:` з назвою джерела журналу; це не
помилка. `Production` є усталеним значенням, якщо змінні `DOTNET_ENVIRONMENT` і
`ASPNETCORE_ENVIRONMENT` не задано. Якщо порт зайнято, застосунок завершується
винятком із рядком `Failed to bind to address … address already in use`.
Довгий stack trace для цієї відмови нормальний: виберіть інший порт і однаково
змініть `--urls` та `@baseUrl` у `BridgeApi.http`.

Не закриваючи сервер, у другому терміналі виконайте:

```bash
curl -i "http://127.0.0.1:5077/hello"
```

У Windows PowerShell 5.1 використовуйте `curl.exe`, оскільки ім’я `curl` там
може означати іншу команду:

```powershell
curl.exe -i "http://127.0.0.1:5077/hello"
```

Якщо у Windows `curl.exe` показує спотворені українські літери, але status і JSON-поля правильні, це може бути кодування термінала. У Command Prompt виконайте `chcp 65001` **до нового виклику** `curl.exe`; у PowerShell також задайте `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8`. Повторіть запит. Для звірки можна відкрити той самий GET у браузері/Scalar. Не змінюйте API через саму лише помилку відображення. Докладніше — у довідці Microsoft: [кодові сторінки консолі Windows](https://learn.microsoft.com/en-us/windows/console/console-code-pages) і [команда chcp](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/chcp).

GET-запит можна також відкрити у браузері, а всі сценарії — у `BridgeApi.http`.
Спостережений результат, скорочений до релевантних рядків:

```text
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

{"message":"Міст C# працює"}
```

Консольний вивід сервера описує процес, код стану HTTP (`status code`) —
результат запиту, а тіло відповіді (`response body`) — дані. Під час авторської
перевірки запит `/hello` залишив повний рядок:

```text
Request finished HTTP/1.1 GET http://127.0.0.1:5077/hello - 200 - application/json;+charset=utf-8 27.9650ms
```

Час наприкінці змінюється між запусками. Такий рядок доводить, що запит дійшов
до сервера й отримав відповідь. У лабораторних роботах цикл «змінити → скласти
→ перезапустити → надіслати запит → спостерегти відповідь» повторюватиметься
постійно.

### Чому саме такий цикл

Після невдалої зміни не варто лише оновлювати сторінку: якщо новий код не
складено або запущено інший процес, ви досліджуєте стару програму. Явні кроки
відділяють помилки залежностей, складання, запуску та HTTP-запиту.

## Мінімальна безпечна зміна

У `Program.cs` знайдіть клас `BridgeText` і коментар `Крок зміни з мосту`:

```csharp
internal static class BridgeText
{
    // Крок зміни з мосту: змініть лише цей безпечний навчальний текст.
    internal const string HelloMessage = "Міст C# працює";
}
```

Замініть лише текст між лапками, наприклад на `"Зміну перевірено"`. Зупиніть
сервер через `Ctrl+C`, повторіть `dotnet build --no-restore` і
`dotnet run --no-build --urls http://127.0.0.1:5077`, а потім викличте `/hello`.
Спостережена відповідь містила `{"message":"Зміну перевірено"}`.

Значення є звичайним навчальним текстом і не містить секрету, HTML або команди.
Не повертайте початкове значення: воно знадобиться для перевірки зв’язку між
двома кінцевими точками далі. Ця вправа перевіряє повний зворотний зв’язок між
вихідним кодом і поведінкою API, а не вміння додати нову складну функцію. Саме
спостережувана відповідь підтверджує, що запущено потрібну версію застосунку.

## Типи, властивості й `null` як частина контракту

У C# **властивість (`property`)** поєднує назване значення з правилами читання
або запису. Вона виконує роль, для якої в C++ часто використовують поле разом з
окремими методами читання та зміни:

```csharp
public sealed class IncidentQuery
{
    [Required]
    public string? Owner { get; init; }
}
```

`init` означає, що значення задають під час створення об’єкта. `string?` містить
**анотацію допустимості `null` для посилального типу (`nullable reference
type`)**: контракт і статичний аналіз ураховують можливість `null`. Попередження
вмикає `<Nullable>enable</Nullable>` у файлі проєкту, але анотація не створює
автоматичної перевірки під час виконання. Компілятор може попередити про
небезпечний шлях, але відповідь на зовнішній запит визначає лише фактично
підключений код перевірки.

`IncidentQuery`, `IncidentSummary` та `Incident` — посилальні типи (`class` або
`record class`), а `int` і `bool` — значеннєві. C# за замовчуванням передає
параметр за значенням, але для `class` копіюється посилання. Тому після
`increment(c)` у наведеному нижче C++ значення об’єкта викликача не зміниться,
а після `Increment(c)` у C# зміниться властивість того самого об’єкта:

```cpp
struct Counter { int value; };
void increment(Counter counter) { ++counter.value; }
```

```csharp
sealed class Counter { public int Value { get; set; } }
static void Increment(Counter counter) => counter.Value++;
```

В обох мовах параметр передано за значенням, але скопійовані значення різні:
цілий C++-об’єкт у першому випадку та посилання на C#-об’єкт у другому. Це дає
спостережуваний наслідок, а не лише відмінність запису.

`FindFirst` повертає `IncidentSummary?`, бо результат може бути `null`. Звичайний
`int` не може бути `null`; для цього потрібен `int?`. Об’єкт запиту створює
`new IncidentQuery { Owner = owner }`. Вираз
`ArgumentNullException.ThrowIfNull(query)` позначає місце можливого винятку,
якщо передати `null` замість `IncidentQuery`.

### Чому це важливо

Контракт допомагає людині й аналізатору побачити припущення до запуску, а
перевірка під час виконання захищає від неприйнятних даних. Якщо сприйняти
анотацію як готовий захист, сервер може обробити дані, які ніхто не перевірив.

## Один інтегрований фрагмент: LINQ, атрибут і впровадження залежностей

Повний файл міститься у
[`Program.cs`](../../one-time-examples/bridge-csharp-minimal-api/Program.cs).

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

Читайте LINQ-ланцюжок зліва направо: `source.Items` дає колекцію, `Where`
залишає відкриті інциденти власника, `Select` створює короткий
`IncidentSummary`, а `FirstOrDefault` запускає перебір і повертає перший
результат або `default` — тут `null`. `Where` і `Select` самі формують
відкладену послідовність: елементи починає перебирати операція, якій потрібен
результат, у цьому фрагменті — `FirstOrDefault`. Для посилального
`IncidentSummary` значення `default` дорівнює `null`, тоді як для `int` воно
дорівнювало б `0`. LINQ не перевіряє вхідний запит і не робить довільну
операцію безпечною лише своєю наявністю.

`[Required]` — **атрибут (`attribute`)**, тобто декларативні метадані. Для появи
поведінки потрібні дві незалежні умови: механізм платформи має бути підключений,
а тип — проходити через нього. У .NET 10 валідацію Minimal API підключає
`builder.Services.AddValidation()`, яка працює з типами параметрів кінцевих
точок. У прикладі немає цього виклику, а `IncidentQuery` створює сам обробник,
тому не виконано жодної умови; сам лише `AddValidation()` поведінки не змінив
би. В API іншої форми місце підключення відрізняється, але спосіб пошуку той
самий. Пізніше метадані `[Authorize]` читатиме налаштований механізм
авторизації; будову конвеєра розглядає лекція 3.

`IncidentLookupService(IncidentSource source)` — **первинний конструктор
(`primary constructor`)**. `source` є залежністю, яку контейнер
**впровадження залежностей (`dependency injection`, DI)** знаходить за
реєстраціями:

```csharp
builder.Services.AddSingleton<IncidentSource>();
builder.Services.AddSingleton<IncidentLookupService>();
```

Момент створення об’єкта визначає час життя (`lifetime`) реєстрації:
`AddSingleton` тут означає один екземпляр на застосунок. Походження залежності
перевіряйте за параметром конструктора і реєстрацією в точці складання
(`composition root`). Об’єкт не виникає лише тому, що конструктор його
попросив: реєстрація є окремим місцем, яке потрібно вміти знайти й перевірити.

### Чому ці три конструкції подано разом

Кінцева точка отримує сервіс, сервіс — залежність, DTO та атрибут описують дані,
а LINQ перетворює колекцію. Такий спосіб читання допомагає не плутати метадані з
реальним механізмом і знаходити компонент, де ухвалюється рішення щодо безпеки.
У готовому ASP.NET Core коді ці конструкції діють разом, тому корисніше
простежити один шлях даних і викликів, ніж запам’ятовувати непов’язані таблиці
синтаксису.

Перевірте ланцюжок запитами:

```bash
curl -i "http://127.0.0.1:5077/incidents?owner=olena"
curl -i "http://127.0.0.1:5077/incidents?owner=unknown"
curl -i "http://127.0.0.1:5077/incidents?owner=maksym"
curl -i "http://127.0.0.1:5077/incidents"
```

У Windows замініть `curl` на `curl.exe`. Спостережені результати:

| Запит | Код стану | Доказ і пояснення |
|---|---:|---|
| `owner=olena` | `200` | Тіло `{"id":1,"title":"Перевірити журнал входів"}` |
| `owner=unknown` | `404` | `Content-Length: 0`; такого власника немає |
| `owner=maksym` | `404` | `Content-Length: 0`; запис закритий і відкинутий `Where` |
| без `owner` | `400` | `Content-Length: 0`; обов’язковий `string owner` не вдалося прив’язати |

Для відповідей із порожнім тілом рядок `Request finished` містить відповідно
`404 0` або `400 0`. Для `olena` відкриті записи мають `Id` 1 і 3, але
`FirstOrDefault` повертає `Id = 1`, бо він перший у `IncidentSource.Items`. Для
запиту до бази даних порядок без явного `OrderBy` не гарантований. Відсутність
результату виражено машиночитним кодом `404`, а не відповіддю `200` із текстом
«не знайдено», тому клієнт може розрізнити ці результати без аналізу фрази.

Запит без `owner` зупиняє прив’язування параметрів (`parameter binding`) до
ненульованого `string owner` ще до обробника. Це не результат `[Required]`:
фреймворк не може сформувати обов’язковий аргумент кінцевої точки, тому повертає
`400` до виконання `new IncidentQuery { Owner = owner }`. Отже, однаковий код
відмови ще не доводить однакову причину відмови.

## Асинхронні сигнатури на рівні читання

У кінцевій точці `/delayed-hello` прочитайте ланцюжок зверху вниз:

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

`Task<string>` представляє майбутній результат типу `string`; найближча аналогія в C++ — `std::future<std::string>`. Якщо операція ще не завершилася, `await` призупиняє виконання методу без блокування потоку на час очікування, тоді як `future.get()` може блокувати потік. Це аналогія результату, а не тотожність механізмів. Докладніше — [оператор await у документації Microsoft](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/await). `async` дозволяє
використати `await`. Отриманий `Task` потрібно очікувати або повернути наступному
рівню. `GreetingService` надходить через DI, а `CancellationToken` платформа
прив’язує до `HttpContext.RequestAborted`, а не бере з DI. Токен сигналізує, що
запит перервав клієнт або сервер і тривалу роботу можна припинити; детальна
модель скасування до мосту не належить. `.Result` і `.Wait()` не є коротшими
замінами `await`: вони блокують потік Thread Pool під час очікування і під
навантаженням можуть зменшувати пропускну здатність. Внутрішня будова
планувальника та `ConfigureAwait` залишаються поза цим матеріалом.

Викличте `/delayed-hello`. Він і `/hello` читають ту саму
`BridgeText.HelloMessage`, тому після попередньої зміни обидва повертають
`{"message":"Зміну перевірено"}`. Це доводить зв’язок із константою, але не
вимірює продуктивність.


## Конструкції C#, які ви зустрінете у starter

Фрагмент мосту навмисно малий. У `starter-project` є ще кілька конструкцій; нижче — кожна з них, файл, де вона трапляється, найближчий відповідник у C++ і пояснення. Це довідкова таблиця: поверніться до неї, коли читатимете код на етапах 2 і 3.

| Запис у starter | Файл | Найближче в C++ | Що означає |
|---|---|---|---|
| `using SecureLab.Api.Data;` і `namespace SecureLab.Api.Data;` | перші рядки кожного `.cs` | `using namespace`, `namespace X { … }` | `using` відкриває імена простору імен. `namespace …;` з крапкою з комою діє на весь файл. Заголовкових файлів і `#include` немає: усі `.cs` проєкту компілюються разом. Простори `System`, `System.Linq`, `System.Collections.Generic` підключено неявно (`ImplicitUsings` у `.csproj`), тому `using System.Linq;` у starter не пишуть |
| `public sealed record IncidentListItemResponse(Guid Id, string Title, …);` | `IncidentResponses.cs` | `struct` із конструктором і полями лише для читання | один запис оголошує тип, конструктор і властивості. У JSON-відповіді назви властивостей починаються з малої літери: `Title` → `"title"`, `CreatedAtUtc` → `"createdAtUtc"`. Саме так їх читає JavaScript: `incident.title` |
| `MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)` | `IncidentEndpoints.cs` | вільна функція, яку дозволено викликати як метод | `this` перед першим параметром робить статичний метод методом розширення. Тому в `Program.cs` написано `app.MapIncidentEndpoints();` |
| `group.MapGet("/", GetListAsync)` | `IncidentEndpoints.cs` | передавання вказівника на функцію | другий аргумент — назва методу без дужок, а не його виклик. У мості на цьому місці стояла лямбда; для фреймворку це те саме |
| `.WithName(…)`, `.Produces<…>()`, `.ProducesProblem(…)` | `IncidentEndpoints.cs` | — | описові метадані для OpenAPI і Scalar. Поведінку endpoint вони не змінюють |
| `GetListAsync(string? status, IncidentQueries queries, CancellationToken cancellationToken)` | `IncidentEndpoints.cs` | — | звідки береться кожен параметр: `status` — з `?status=…` за назвою; `queries` — з контейнера DI; `cancellationToken` — від платформи. У `GetDetailsAsync` параметр `Guid id` береться з частини шляху `{id:guid}` |
| `Results.Problem(title: "…", statusCode: …)` | `IncidentEndpoints.cs` | — | іменовані аргументи: назва параметра, двокрапка, значення. Порядок можна змінювати, зайві параметри пропускати |
| `Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var candidate)` | `IncidentEndpoints.cs` | функція з вихідним параметром `T&`, що повертає `bool` | `out var candidate` оголошує змінну просто у виклику; метод записує в неї результат і повертає `true` або `false`. `Enum.IsDefined(candidate)` поруч потрібен тому, що `TryParse` приймає і числовий текст, наприклад `"99"` |
| `status is not null`, `incident is null ? … : …` | `IncidentEndpoints.cs` | `!= nullptr`, `== nullptr` | перевірка на `null` |
| `… ?? throw new InvalidOperationException(…)` | `Program.cs` | — | `a ?? b` дає `a`, якщо воно не `null`, інакше `b`; тут замість `b` одразу виняток |
| `["status"] = ["…"]`, `= [];`, `new(1, "…")` | `IncidentEndpoints.cs`, `Incident.cs`; `new(…)` — у `Program.cs` прикладу мосту | список ініціалізації `{ … }` | квадратні дужки створюють колекцію потрібного типу; `new(…)` без назви типу створює об’єкт типу, який очікується в цьому місці |
| `public DbSet<Incident> Incidents => Set<Incident>();` | `SecureLabDbContext.cs` | метод-геттер з одним `return` | `=>` після назви властивості або методу — це скорочене тіло з одного виразу, а не лямбда |
| `AddScoped<IncidentQueries>()`, `AddDbContext<SecureLabDbContext>(…)` | `Program.cs` | — | реєстрації в контейнері DI. `Scoped` — окремий екземпляр на кожен HTTP-запит (`Singleton` у мості — один на весь застосунок). `DbContext` теж реєструється як scoped |
| `app.UseExceptionHandler();` … `app.Use(async (context, next) => { …; await next(context); });` | `Program.cs` | ланцюжок обгорток навколо функції | middleware — ланки конвеєра обробки. Запит проходить їх у порядку запису в `Program.cs` зверху вниз, доходить до endpoint, а відповідь повертається крізь ті самі ланки у зворотному порядку. У starter: обробка винятків → сторінки кодів стану → заголовки `X-Content-Type-Options` і `Referrer-Policy` → статичні файли з `Client/` → endpoint-и → запасний `index.html` |
| `public required string Title { get; set; }`, `= null!;` | `Incident.cs` | — | `required` — властивість мусить бути задана під час створення об’єкта; `null!` — обіцянка компіляторові, що значення заповнить EF Core |
| `enum IncidentSeverity { Low, Medium, High, Critical }` | `Incident.cs` | `enum class` | `value.ToString()` дає назву (`"Low"`), `Enum.GetValues<IncidentSeverity>()` — усі значення в порядку оголошення |
| `$"Інцидент '{id}' не існує."` | `IncidentEndpoints.cs` | `std::format` | рядок із підстановкою значень у фігурних дужках |
| `IReadOnlyList<T>`, `List<T>`, `Dictionary<K, V>`, `Guid`, `DateTimeOffset` | скрізь | `const std::vector<T>&`, `std::vector<T>`, `std::unordered_map<K, V>` | `Guid` — 128-бітний ідентифікатор (UUID); `DateTimeOffset` — момент часу зі зміщенням від UTC |
| `sealed`, `static class`, `internal` | скрізь | `final`; клас лише зі статичними членами; видимість у межах проєкту | модифікатор доступу пишуть перед кожним членом окремо, секцій `public:` немає |

Кілька рядків таблиці можна перевірити окремою програмою. Готовий повний файл — [ReadDemo/Program.cs](../../copyable/ReadDemo/Program.cs), а проєкт поруч із ним можна одразу запустити `dotnet run`. Він є також у `resources/copyable/ReadDemo/` розпакованого ZIP. Якщо створюєте пробу самі, відкрийте окрему папку **поза starter** і виконайте:

```bash
dotnet new console -n ReadDemo -f net10.0
cd ReadDemo
```

Замініть **увесь** `Program.cs` наступним текстом (або готовим файлом), збережіть його в UTF-8 і виконайте `dotnet run`:

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

Очікуємо:

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

Четвертий рядок показує, навіщо після `TryParse` потрібен `IsDefined`: текст `"99"` розібрано «успішно», хоча такого значення в `enum` немає. П’ятий рядок показує перетворення назв: `Kind` і `Minutes` у C# стали `kind` і `minutes` у JSON. `JsonSerializerDefaults.Web` — ті самі правила, які ASP.NET Core застосовує до відповідей API.

## Типові хибні уявлення

| Хибне уявлення | Що відбувається насправді |
|---|---|
| «C# — це C++ зі збирачем сміття» | Подібний синтаксис не скасовує відмінностей типів, параметрів і будови застосунку. |
| «Атрибут сам виконує перевірку» | Атрибут зберігає метадані; потрібен підключений механізм, який їх прочитає. |
| «`string?` відхиляє `null` під час виконання» | Анотація керує статичним аналізом; перевірка під час виконання є окремою дією. |
| «DI-об’єкт з’являється через запис у конструкторі» | Контейнер потребує реєстрації або іншого правила створення. |
| «`.Result` — короткий запис `await`» | `.Result` блокує потік, тоді як `await` не тримає його заблокованим під час очікування. |

## Що залишається на лабораторну роботу та лекції

У ЛР 1 ви працюватимете з готовим стартером, пройдете шлях
`browser → API → PostgreSQL`, знайдете кінцеву точку, зробите власну обмежену
зміну та зафіксуєте докази. Не копіюйте навчальний `/hello` або колекцію в
пам’яті як виконану частину ЛР.

Лекції 2–3 систематизують стратегію читання незнайомої кодової бази C# і будову
ASP.NET Core застосунку. Поза мостом лишаються CLR і налаштування збирання
сміття, розширений LINQ, expression trees, reflection, проєктування
DI-контейнера, детальна модель скасування, `ConfigureAwait`, Docker і публікація
API. Міст також не навчає створювати застосунок із нуля: його завдання — дати
мінімум, потрібний для читання та обмеженої зміни вже наданого коду.

До етапу 2 повторіть маршрут без підказки. Якщо крок не відтворюється,
зафіксуйте точну команду та її фактичне повідомлення замість формулювання
«нічого не працює».

## Запитання для самоперевірки

1. Який об’єкт повертає `FindFirst`, де він створюється і де може виникнути
   `ArgumentNullException`?
2. Що поверне LINQ-ланцюжок для `owner=olena`, чому саме `Id = 1` і чого він сам
   по собі не перевіряє?
3. Звідки береться `IncidentSource` і де це підтвердити у проєкті?

### Відповіді для самоперевірки

1. `FindFirst` повертає перший `IncidentSummary` або `null`. Об’єкт створюється
   у `Select` виразом `new IncidentSummary(incident.Id, incident.Title)`, а
   виняток можливий у `ArgumentNullException.ThrowIfNull(query)`.
2. Повертається відкритий інцидент з `Id = 1` і назвою `Перевірити журнал
   входів`, бо він перший відповідний елемент. LINQ не перевіряє запит і не
   перетворює атрибут на перевірку.
3. DI-контейнер надає `IncidentSource` за реєстрацією
   `builder.Services.AddSingleton<IncidentSource>()` у `Program.cs`.


