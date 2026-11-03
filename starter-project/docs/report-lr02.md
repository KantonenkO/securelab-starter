# Звіт WEB ЛР 2. Контракт API, серверна валідація та захист від SQL injection

## 1. Ідентифікація

- ПІБ і група: Антоненко Катерина Юріївна, КБ-42.
- Спільний baseline 2-A; індивідуального номера немає.
- Заявлений рівень: **відмінний («Відмінно»)**.
- Гілка: `lab/2-input-sqli` (створена від тегу `v0.1.0`).
- Release: `lab-02-start-v1`; base_commit `3275a6b4636a4a854d4a2802e65ba31ddc53760d`; entry_parent `235526c0982509401b32e7661cf35b56917fccde` (`.scaffolds/lab-02.json`).
- Vulnerable commit: `1cc79d890a1e75a5df5fc46b8937dd7da6d31a00`.
- Fixed commit коду: `8c2a6d02380bb5acedfaf355ca6b130a0af2a3a5`. Тег `v0.2.0` стоїть на commit із завершеним звітом, створеному після fixed commit (між ними змінено лише `docs/`).
- DEL-01: remote не передавався; історію зібрано в Git bundle `securelab-lr02.bundle` (команди `git bundle create/verify/list-heads` виконано в п. 7).

## 2. Контракт і CP-01

Файл: `src/SecureLab.Api/Scaffolding/Lab02Endpoints.cs`. Обробник `POST /api/incidents` приймає лише `CreateIncidentRequest(Title, Description, Severity, OccurredAtUtc)`; `id`, `ownerUserId`, `status`, `createdAtUtc`, `updatedAtUtc` встановлює сервер (власник - Alice із `DbSeeder.AliceId`, статус `New`). Відповідь 201 - окремий `CreatedIncidentResponse`.

| Поле | Правило | Відповідь при порушенні |
|---|---|---|
| title | не порожній після Trim(); довжина до Trim() не більша за 160 | 400, ключ `title` |
| description | не порожній після Trim(); довжина до Trim() не більша за 4000 | 400, ключ `description` |
| severity | `Enum.TryParse(ignoreCase)` **і** `Enum.IsDefined`; значення зі списком через кому відхиляються | 400, ключ `severity` |
| occurredAtUtc | обов'язкове; не пізніше `now + 5 хв`; після перевірки `.ToUniversalTime()` | 400, ключ `occurredAtUtc` |
| cross-field (T-09) | для High/Critical опис після Trim() не коротший за 40 символів | 400, ключ `description` |
| конфлікт (T-03) | той самий title після Trim() з тим самим регістром серед статусів New/Triaged/InProgress/Resolved (Closed не блокує) | 409 Problem Details |

Порядок обробки: час фіксується один раз -> базові перевірки -> нормалізація Trim() -> cross-field -> `Results.ValidationProblem(errors)` до звернення до БД -> предметний конфлікт `AnyAsync` -> створення entity -> `SaveChangesAsync` (унікальне обмеження `23505` теж перетворюється на 409) -> response DTO.

**Чому потрібні і `Enum.TryParse`, і `Enum.IsDefined`.** `TryParse` приймає числовий рядок: `"7"` успішно перетворюється на `(IncidentSeverity)7`, хоча такого елемента в enum немає. Лише `IsDefined` відхиляє таке значення. Тому запит `severity: "7"` отримує 400 (докази: `evidence/t02a-response.txt`, тест `T02_InvalidDto_Returns400WithFieldKey`).

**Чому `<select>` у браузері не замінює перевірку.** HTML-атрибути й JavaScript належать клієнту, яким керує користувач; будь-який інший HTTP-клієнт надішле довільний JSON. Контролем є лише серверна валідація.

**Overposting.** `CreateIncidentRequest` не має властивостей `Id`, `OwnerUserId`, `Status`, `CreatedAtUtc`, тому зайві поля JSON ігноруються: запит із `id`, `ownerUserId` (Bob), `status: "Closed"` створив запис з новим id, статусом New та власником «Аліса Коваль» (`evidence/a02-cp01-response.txt`, `evidence/a02-cp01-details.txt`).

Скріншоти: `evidence/screens/e03-fixed-post-validation.png` (код POST), `evidence/screens/e07-http-checks.png` (сценарії `tests/http/lab-02-checks.http`).

## 3. Security-сценарій і CP-02/CP-03

1. **Контекст і гіпотеза.** Локальний `GET /api/incidents/search` отримує недовірені `q` і `sortBy`; є ризик, що значення увійде до SQL-структури.
2. **Стан до.** Vulnerable commit `1cc79d8`, файл `src/SecureLab.Api/Scaffolding/Lab02Endpoints.cs`, обробник `MapGet("/api/incidents/search")`: рядок 19 (`var order = sortBy switch { ... _ => sortBy }`) повертає клієнтський `sortBy` як SQL-фрагмент, рядки 24-25 з'єднують `q` і `order` зі SQL-текстом, рядок 26 виконує `db.Incidents.FromSqlRaw(sql)`.
3. **Мінімальний PoC.** Власний read-only сценарій C із `tests/http/lab-02-checks.http` (значення `q` закодоване в URL; без DROP/DELETE/UPDATE/DDL/stacked statements).
4. **Спостереження.** Запит C повернув усі 5 записів seed замість порожньої множини; легітимний апостроф (`O'Brien`, `комп'ютерного`) дав 500, у журналі `Failed executing DbCommand ... syntax error at or near "Brien"`; у всіх записах журналу `Parameters=[]`.
5. **Першопричина.** `q` і `sortBy` з'єднувались із SQL-текстом до виконання, тому PostgreSQL отримував один суцільний текст і не міг відрізнити дані від інструкції. Апостроф закриває рядковий літерал SQL; решта введення читається як SQL. Фільтр апострофа або заборона `OR` не усуває механізму: легітимні дані ламаються, а інші SQL-контексти (наприклад `sortBy`) лишаються відкритими.
6. **Виправлення.** `LINQ` + `EF.Functions.ILike(..., pattern, "\\")` для Title і Description; `pattern` складається з `EscapeLike(q)` (спершу екранується `\`, потім `_` і `%`) і передається **параметром** (`@pattern`); `sortBy` обирається allowlist `switch` із трьох написаних виразів (createdAtUtc, severity, status з рангами Critical/High/Medium/Low та New/Triaged/InProgress/Resolved/Closed), другий ключ `ThenBy(Id)`, `Take(50)`; невідоме значення дає 400 із ключем `sortBy` і до SQL не доходить.
7. **Retest і позитивна регресія.** Той самий PoC C повертає 200 і `[]`; звичайний пошук USB, `O'Brien` та `комп'ютерного` повертають очікувані записи; `%` шукається буквально; у журналі `Parameters=[@pattern=..., @pattern0=..., @p=...]`.
8. **Залишковий ризик.** Перевірка не замінює authorization (її додасть ЛР 3), обмеження ресурсу або пагінацію, перевірку інших endpoint та production logging policy.

**CP-02 (доказ першопричини).** Vulnerable commit `1cc79d8`; таблиця спостереження:

| Поле | Значення |
|---|---|
| Передумови | local Development, контейнер PostgreSQL, reset, vulnerable commit `1cc79d8` |
| Дія | `GET /api/incidents/search`, контрольний сценарій C (S-01) |
| Очікування без дефекту | пошук не повертає записи, що не відповідають значенню |
| Фактичне спостереження | 200 і 5 записів (`...0001`-`...0005`) замість порожньої множини |
| Пояснення | апостроф закрив рядковий літерал, `OR TRUE` став частиною умови WHERE, а `--` перетворив решту рядка (включно з частиною запиту та `ORDER BY`) на коментар |

Планований механізм виправлення: замість передавати в PostgreSQL SQL-текст, до якого вже приєднано `q`, я передаю значення параметром `@pattern` через LINQ/`EF.Functions.ILike`. Значення `sortBy` я не приймаю як SQL, а зіставляю з allowlist із трьох дозволених виразів.

**CP-03 (перевірений механізм).** Різниця між commits (`git diff 1cc79d8..8c2a6d0`, скріншот `e04-diff-vuln-fixed.png`): раніше СУБД отримувала один текст після конкатенації (`Parameters=[]`), тепер значення `pattern` передається окремо (`Parameters=[@pattern=..., ...]`), а структуру SQL вибирає серверний allowlist. Результати baseline-тестів і retest наведено нижче та на скріншотах `s02-fixed-run.png`, `s06-tests-reset-build.png`.

Скріншоти: `s01-vulnerable-run.png` (стан до), `s02-fixed-run.png` (стан після), `e01-vulnerable-root-cause.png`, `e02-fixed-search.png`, `e04-diff-vuln-fixed.png`.

## 4. Фактичні перевірки DEL-02

Усі перевірки виконано локально в Development із штучним seed. Ручні сценарії - `tests/http/lab-02-checks.http`; автоматичні - `tests/SecureLab.Api.Tests/CreateContractTests.cs` і `SearchSecurityTests.cs` (кожен тест, що створює дані, прибирає лише власні записи у `finally`).

| ID | Сценарій | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|---|
| T-01 | Коректне створення | reset; унікальний title | POST валідного DTO (`severity: "low"`, зміщення `+03:00`) | 201, response DTO без зайвих полів | 201 Created, `Location`, status New, title обрізано Trim(), без `ownerUserId` та `description`; тест `T01_...` пройдено | `evidence/t01-response.txt` |
| T-02 | Некоректний DTO | - | POST: порожній title, `severity: "7"`, відсутній `occurredAtUtc`; окремо дата 2099 | 400, `application/problem+json` | 400, `application/problem+json`, `errors` містить `title`, `severity`, `occurredAtUtc`; без stack trace/SQL; автоматичні тести (title, "7", "Low,High", "Unknown", довжини 161/4001, дата) пройдено | `evidence/t02a-response.txt`, `evidence/t02b-response.txt` |
| T-03 | Предметний конфлікт | існує активний інцидент із тим самим title | повторний POST із тим самим title | 409 Problem Details | 409, `application/problem+json`, `detail` без внутрішніх назв, поля `errors` немає; інший регістр - 201; Closed не блокує - 201 (автоматично) | `evidence/t03-response.txt` |
| S-01 | SQLi до виправлення | vulnerable commit, local seed | сценарій C | небажано розширена вибірка | 200 і 5 записів; D (`O'Brien`) - 500 | `evidence/s01-C-vuln.txt`, `evidence/s01-D-vuln.txt`, скріншот `s01-vulnerable-run.png` |
| S-02 | Retest SQLi | fixed commit, той самий seed | той самий сценарій C; автоматичний тест | порожня множина | 200 і `[]`; автоматичний `S02_ControlInput_ReturnsEmptySet` та позитивний контроль `S02_PositiveControl_UsbReturnsExactlyOneKnownRecord` (рівно один запис, Id `...0005`) пройдено | `evidence/s02-C-fixed.txt`, скріншот `s02-fixed-run.png` |
| T-04 | Позитивна регресія | fixed commit | USB; `O'Brien`; `комп'ютерного`; `%` | коректні записи, без 500 | USB - 1 запис `...0005`; `O'Brien` - 1 запис `...0004`; `комп'ютерного` - 1 запис `...0003`; `%` - `[]`; wildcard `_`, `%` та `\` шукаються буквально (автоматично) | `evidence/s02-A-fixed.txt`, `evidence/t04-D-apostrophe-fixed.txt`, `evidence/t04-kompiuternoho-fixed.txt`, `evidence/t04-percent-fixed.txt` |
| T-05 | Невідоме сортування | fixed commit, reset | `GET /api/incidents/search?sortBy=price` | 400 | 400, `application/problem+json`, `errors.sortBy` із переліком createdAtUtc, severity, status; також `title; DROP TABLE incidents`, `severity DESC, id`, `created_at_utc` -> 400 (автоматично) | `evidence/t05-response.txt` |
| T-06 | Ресурс не знайдено | endpoint ЛР 1 | `GET /api/incidents/99999999-9999-9999-9999-999999999999` | 404 Problem Details | 404, `application/problem+json`, title «Інцидент не знайдено» | `evidence/t06-response.txt` |
| T-09 | Cross-field 39/40 | унікальні title; решта полів коректна | два POST: High, description 39 та 40 символів | 400 `description`; потім проходить | 39 - 400 з ключем `description`; 40 - 201; автоматично для High і Critical (з пробілами по краях); для Low короткий опис дозволений | `evidence/t09a-39-response.txt`, `evidence/t09b-40-response.txt` |
| T-10 | Додаткова предметна перевірка | задокументовано нижче | POST із `occurredAtUtc` старшим за 365 днів | 400 `occurredAtUtc`; допустимий - 201 | 2020-01-01 - 400 `occurredAtUtc`; 2026-10-01 - 201; автоматично 366 днів - 400, 364 - 201 | `evidence/t10a-response.txt`, `evidence/t10b-response.txt` |
| A-01 | Огляд data-access points | fixed commit | `git grep` і Python-скан із методики; повний перегляд `src` | висновок для кожної точки | таблиця нижче | `evidence/a01-search-fixed.txt`, скріншот `s07-a01-data-access-scan.png` |
| A-02 | Overposting | fixed commit | автоматичний тест із зайвими `id`, `ownerUserId`, `status`, `createdAtUtc` | server-managed значення не підмінено | тест `A02_Overposting_...` пройдено; мутація (тимчасово `Status` у DTO та `Enum.Parse`) дає `Expected: "New", Actual: "Closed"`, після відкату - пройдено | `evidence/a02-cp01-response.txt`, `evidence/a02-mutation-fail.txt`, `evidence/a02-mutation-restored.txt` |

**T-10 (додаткове правило).** Правило: `occurredAtUtc` не раніше ніж за 365 днів до поточного UTC-часу сервера; межа - 365 днів; ключ помилки `occurredAtUtc`; відповідь 400. Правило перевіряється в тому самому handler до доступу до БД і не послаблює правил 2-A. Допустимий запит: `occurredAtUtc = 2026-10-01T10:00:00Z` (201). Недопустимий: `2020-01-01T00:00:00Z` (400).

**A-02.** Маршрут overposting. «До»: відтвореного дефекту в коді не було; ризик - поля `id`, `ownerUserId`, `status`, `createdAtUtc` міг би підмінити клієнт, якби request DTO повторював entity; у `CreateIncidentRequest` (`Lab02Endpoints.cs`, наприкінці файла) цих властивостей немає, тобто ризик усунено контрактом DTO. «Після»: автоматичний тест надсилає зайві поля й перевіряє новий id (не надісланий), status New і власника «Аліса Коваль». Чутливість тесту перевірено локальною мутацією (її не закомічено).

**A-01. Огляд усіх точок формування й виконання запитів до БД у `src` (fixed commit).**

| Файл:рядок / метод | Категорія | Висновок |
|---|---|---|
| `Scaffolding/Lab02Endpoints.cs`, GET `/api/incidents/search` | LINQ + `EF.Functions.ILike`, `pattern` - параметр `@pattern`; сортування через allowlist | безпечно: значення не входить у текст SQL, структуру вибирає серверний `switch`; невідоме `sortBy` -> 400 |
| `Scaffolding/Lab02Endpoints.cs`, POST `/api/incidents` (`AnyAsync`, `Add`, `SaveChangesAsync`) | LINQ, значення - параметр `@title` | безпечно; вхід провалідовано до звернення до БД |
| `Scaffolding/Lab02Seed.cs`, `EnsureAsync` | LINQ `AnyAsync` за фіксованим Guid, `Add` статичних даних | безпечно: користувацького введення немає |
| `Application/Incidents/IncidentQueries.cs`, `GetListAsync` | LINQ; фільтр за enum `IncidentStatus` після `TryParse`+`IsDefined` | безпечно |
| `Application/Incidents/IncidentQueries.cs`, `GetDetailsAsync` | LINQ за `Guid id` (route constraint `:guid`) | безпечно |
| `Application/Incidents/IncidentQueries.cs`, `GetSeveritySummaryAsync` | LINQ `GroupBy` без введення | безпечно |
| `Data/DatabaseBootstrap.cs:29`, `ExecuteSqlRawAsync` | trusted static SQL (`TRUNCATE` фіксованих таблиць; перед цим перевірка Development) | безпечно: рядок не складається з введення, виконується лише в `--reset-database` |
| `Data/DatabaseBootstrap.cs:26,42`, `MigrateAsync` | міграції EF Core | безпечно: статичні міграції |
| `Data/DbSeeder.cs`, `SeedAsync` | LINQ `AnyAsync`, `AddRange`, `SaveChangesAsync` статичних даних | безпечно |
| `Program.cs:53`, `/health` -> `CanConnectAsync` | перевірка з'єднання | безпечно: без запитів із даними користувача |
| `tests/.../CreateContractTests.cs`, `SearchSecurityTests.cs` (`ExecuteDeleteAsync`, `ExecuteUpdateAsync`) | LINQ у тестах, значення - параметри; прибирають лише власні записи | безпечно (лише тестовий код) |

Збіг `+ query` у `tests/.../SearchSecurityTests.cs:24` - формування URL тесту (не SQL), безпечно. Порожній результат другого пошуку для `src` теж занесено до таблиці.

**Build/baseline-tests після останньої зміни.** Перед фінальним commit виконано reset, `dotnet build tests/SecureLab.Api.Tests -c Release` (0 попереджень, 0 помилок) і `dotnet test tests/SecureLab.Api.Tests -c Release`: усього 36, успішно 36, збоїв 0 (`evidence/dotnet-test-final.txt`, скріншот `s06-tests-reset-build.png`). До цього входять `Search_ReturnsJson`, 4 baseline-тести ЛР 1, 14 тестів `CreateContractTests` та 17 тестів `SearchSecurityTests` (з урахуванням параметризованих випадків). Тест підсумку за severity з ЛР 1 пройшов без змін.

**Порядок сортування штатного seed** (`evidence/sort-default.txt`, `sort-severity.txt`, `sort-status.txt`): без `sortBy` - 0004, 0005, 0003, 0002, 0001; `severity` - 0002 High, 0001 Medium, 0003/0004/0005 Low; `status` - 0003/0004/0005 New, 0001 Triaged, 0002 InProgress; `price` - 400.

**Запуск за README (чотири посилання).** Після `docker compose ... up -d --wait` і `dotnet run` відкрито: клієнт (`b01-client.png`), `/health` (`b02-health.png`), Scalar (`b03-scalar.png`), `openapi/v1.json` (`b04-openapi-json.png`). Scalar показує додані `GET /api/incidents/search` і `POST /api/incidents`. Налаштування середовища: порт 54329 належить діапазону, зарезервованому Windows, тому PostgreSQL піднято на 54430 через локальний `infra/.env` (в Git не потрапляє), а рядок підключення передається змінною `ConnectionStrings__SecureLab` поза Git.

Додаткові матеріали ЛР 1 додано до `docs/additional-lr01/` (лише документація; код ЛР 1 не змінено).

## 5. Обмеження і висновок

Перевірено: серверна валідація контракту створення (базові правила 2-A, cross-field, предметний конфлікт, додаткове правило T-10), безпечні Problem Details без внутрішніх деталей, відтворення read-only SQLi на локальному vulnerable commit, усунення першопричини через LINQ/`ILike` із параметром, буквальний пошук `%`, `_`, `\`, allowlist `sortBy` з рангами, автоматичні T-02/T-03/T-09/T-10/A-02/S-02 і повний огляд A-01.

Обмеження: перевірка `AnyAsync` перед записом не гарантує інваріанту за паралельних створень - у реальному застосунку потрібне обмеження БД або транзакційне рішення (міграцію й унікальний індекс у ЛР 2 не додавали; унікальне порушення `23505` лише перетворюється на 409). Зіпсований JSON або відсутній `Content-Type` у стартовому конвеєрі дають 500 до handler (`BadHttpRequestException`) - окремий дефект обробки, не доказ валідації; цей випадок не покрито. Пошук без пагінації (`Take(50)`), authorization відсутня до ЛР 3. Доступ до `docs/architecture.md`, `docs/report-template.md` і `tests/http/incidents.http` (власні незакомічені правки ЛР 1) цією роботою не змінювався.

## 6. Декларація використання ШІ

| Сервіс | Задачі | Авторська адаптація/інтерпретація | Фактична перевірка |
|---|---|---|---|
| Claude (Anthropic), Claude Code | встановлення scaffold, реалізація валідації POST і безпечного пошуку, написання тестів, збір доказів, чернетка звіту | код і твердження звірено з методичними рекомендаціями; рішення (allowlist, рангування, екранування, правило T-10) пояснено у розділах 2-4 | усі команди виконано локально; результати - у скріншотах і `docs/evidence/`: 36 з 36 тестів, retest S-02, мутаційна перевірка A-02 |

## 7. Передавання

- Remote доступний викладачеві або bundle: створено `securelab-lr02.bundle` (поруч із репозиторієм), виконано `git bundle verify` та `git bundle list-heads` (скріншот `s08-final-git-bundle.png` у файлі звіту `.docx`).
- У Moodle файли/докази: звіт `docs/report-lr02.md` (докази - `docs/evidence/`) і bundle; надсилання в Moodle виконує студентка.
- Git diff та історію перевірено на секрети: нових секретів немає; у Git є лише відкриті навчальні credentials `securelab / local-study-password`, успадковані від starter (compose.yaml, README, appsettings.Development.json), які методичка визначає як несекретні; `infra/.env`, `.vscode/` і логи в Git не потрапили.
