# Звіт ЛР04. Авторизація, ролі, володіння ресурсом та IDOR/BOLA

Автор: Антоненко Катерина Юріївна, група КБ-42. Дисципліна «Прикладні технології програмування в інформаційній безпеці», викладач Бабенко Ю.М.

## Контекст і межа виконання

- **Рівень:** відмінний («Відмінно»): усі пункти достатнього й доброго рівнів, A-01…A-12, відокремлені resource-based handler-и, A-11 для search і severity-summary, обґрунтування видимості, least privilege і залишкового ризику.
- **Baseline:** 2-A «Трекер інцидентів»; base tag `v0.3.0` (commit `bd9adf7`); гілка `lab/4-authorization`.
- **Головна action:** `GET /api/incidents/{id}` (details); додатково update, delete, triage, child comment, list, search, severity-summary.
- **Політика відмов:** чужий ресурс → **404** (тотожна відповіді для неіснуючого id), ресурс видимий, але дію заборонено → **403**, недопустимий стан → **409**, зайві поля DTO → **400**, анонім → **401**. Повна таблиця — `docs/access-matrix.md`.
- **Хеші:** vulnerable `907321ae6d27a1f634744cfc6e984f2d72533673`; fixed — див. розділ «Перевірки» (хеш тегу `v0.4.0`).
- **Середовище:** Windows 10 Pro, .NET SDK 10.0.302, PostgreSQL у Docker Compose, VS Code; пароль навчальних акаунтів — змінна середовища `SeedUsers__Password`.
- ЛР 3 після тегу не виправлялась; тег `v0.3.0` не пересувався.

## Матриця і server-side рішення

Матриця — `docs/access-matrix.md` (усі клітинки заповнені: allow/deny із статусом відповіді). Правила в одному місці — `IncidentAccessRules`; два resource-based handler-и (`IncidentAuthorizationHandler` для `Incident`, `IncidentCommentAuthorizationHandler` для child) викликаються через `IAuthorizationService`. Query scoping для list/search/summary використовує ту саму умову видимості (`IncidentAccessRules.VisibleTo`) безпосередньо в запиті (до сортування, `Take(50)` і групування). Роль і id — лише з `HttpContext.User`. DTO: `UpdateIncidentRequest(Title, Description)` і `TriageIncidentRequest(Reason)` з `[JsonUnmappedMemberHandling(Disallow)]`; owner/status/role/severity з body не приймаються.

## Вразливий стан і PoC

Vulnerable commit `907321a`: details, update, delete, triage і child-comment шукають ресурс лише за id, без порівняння з principal. Сценарій — `tests/http/lab-04-access.http` і `docs/evidence/lab-04/poc_before.txt` (Python helper з cookie лише в пам'яті).

| Дія | Очікування за матрицею | Фактично до виправлення |
|---|---|---|
| Alice → власний інцидент | 200 | 200 |
| Alice → інцидент Bob (…0002) | 404 | **200**, дані Bob (IDOR/BOLA) |
| Анонім → інцидент Bob | 401 | **200** |
| Alice PUT інцидент Bob `{title}` | 404, стан без змін | **200**, назву Bob змінено |
| Alice DELETE інцидент Bob | 404 | **204**, видалено |
| Alice PUT власний `{status, ownerUserId}` | 400 | **200**, статус змінено, власником став Bob |
| Reporter Alice triage власного `{status:Closed}` | 403 | **200**, статус Closed |
| Alice → коментар до інциденту Bob / internal | 404 | **200** / **200** (`isInternal: true`) |
| Alice list / severity-summary / search O'Brien | лише власні | усі 5 / High 1 Medium 1 Low 3 / знайдено …0004 |

**Першопричина:** запит `Where(i => i.Id == id)` (і так само для child) без умови за власником/роллю і без виклику централізованого рішення; `RequireAuthorization()` перевіряє лише автентифікацію; update DTO містить поля, якими має керувати сервер; transition бере статус із body.

Browser client на vulnerable стані: Alice відкриває деталі Bob і бачить його дані (знімок у docx-звіті).

## Виправлення та retest

Fixed commit — див. `v0.4.0`. Порядок у кожній mutation: завантаження tracked entity → рішення handler-а → (для відмови: audit для privileged) → перевірка поточного статусу → сервер визначає next status → `SaveChangesAsync`.

| Дія | До | Після |
|---|---|---|
| Alice → Bob details | 200 | **404** `{"title":"Інцидент не знайдено"}` |
| Анонім → details | 200 | **401** |
| Alice PUT/DELETE Bob | 200/204 | **404**, стан Bob без змін |
| Alice PUT власний із forged status/owner/role/severity | 200 | **400**, стан без змін |
| Alice triage власного | 200 | **403** + audit; Bob triage чужого — **404** + audit |
| Analyst triage `New` | 200 | **200**, `Triaged`; повторно **409** |
| Alice → коментар Bob / internal | 200/200 | **404/404**; Analyst → internal **200** |
| Alice list | усі 5 | …0001, …0003, …0005 |
| severity-summary (Alice) | High 1 Medium 1 Low 3 | **Medium 1 Low 2**, High 0 |
| search O'Brien (Alice) | …0004 | **[]** |

Позитивна регресія: Alice читає/редагує власне (A-02); Morgan читає інцидент Alice (A-03R); Morgan виконує triage (A-07). Для read тіло відмови не містить даних Bob; для mutation стан до/після однаковий (перевірено повторним читанням власником).

## Доказова таблиця

| ID | Рівень | Сценарій | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|---|---|
| A-01 | добрий | Анонімний доступ | немає authentication | 8 protected endpoint-ів | 401, не redirect | 401, без Location, не HTML | `AuthorizationTests.A01_*` (8 InlineData) |
| A-02 | достатній | Власний ресурс | Alice, seed-ресурс | details; update власного | allow; змінилися лише дозволені поля | 200; owner/status/severity/description без змін | `A02_*` |
| A-03 | достатній | Чужий ресурс | Alice, Bob resource | details з підміненим id | 404, дані не розкрито | 404; тіло без title/імені Bob/id | `A03_*` |
| A-03R | достатній | Аналітичний read | Morgan, ресурс Alice | `GET /api/incidents/{id}` | allow; без права зміни | 200; PUT/DELETE → 403, стан без змін | `A03R_*` |
| A-04 | добрий | Усі зміни чужого | Alice, Bob resource | PUT, DELETE | deny, стан без змін | 404/404; Bob бачить ресурс без змін | `A04_*` |
| A-05 | добрий | List | Alice / Morgan / Admin | list | лише дозволена множина | Alice: 3 власні; Analyst: 5; Admin: 0 | `A05_*` |
| A-06 | добрий | Вертикальне підвищення | Reporter | triage | deny, статус без змін | owner 403; чужий 404; статус New | `A06_*` |
| A-07 | добрий | Дозволена привілейована дія | Analyst, інцидент New | triage | allow, сервер ставить Triaged | 200 → Triaged; forged status → 400; повтор → 409 | `A07_*` |
| A-08b | добрий | Overposting | Reporter | update з forged owner/status/severity/role | 400, стан без змін | 400 ×4, стан без змін | `A08b_*` |
| A-09 | добрий | Audit | denied triage | `authorization.denied` у виводі API | безпечна подія, той самий traceId | `docs/evidence/lab-04/audit_out.txt`; traceId у події = traceId у Problem Details | `A09_*` |
| A-10 | добрий (404) | Нерозрізнюваність | один клієнт Alice | чужий і неіснуючий id (GET/PUT/DELETE/POST triage) | однакові status, Content-Type, тіло без traceId | збігається | `A10_*`, `a10_out.txt` |
| A-11 | відмінний | search і severity-summary | Alice | summary, search O'Brien | лише дозволена множина | Medium 1 Low 2 High 0; search порожній; Analyst: High 1 Low 3, знайдено | `A11_*` |
| A-12 | добрий | Child, internal | Alice (Reporter) | comment-2, comment-3, details | deny; internal не у відповіді | 404; internal лише Analyst | `A12_*` |

Кількісно: усього **84** тести (62 попередніх + 22 нових кейсів), усі пройдені; на vulnerable commit нові тести **падають** (16 із 22 кейсів — `docs/evidence/lab-04/red_on_vulnerable.txt`), на fixed — проходять.

## Основний finding і залишковий ризик

**До:** будь-який автентифікований користувач читає, змінює й видаляє чужий інцидент за id, підвищує власний статус і стає «власником» через body. **Причина:** пошук за id без ресурсного рішення + role-less update/transition + довіра до body. **Fix:** query scoping + resource-based handler + вузькі DTO + роль із server-issued claim. **Після:** таблиця вище. **Залишковий ризик:** CSRF для cookie-автентифікованих mutation (навчальний борг до ЛР 6); Analyst бачить усі інциденти (за матрицею, у промисловій системі потрібен аудит такого доступу); роль потрапляє в cookie при login — після зміни ролі потрібен повторний вхід; часові side-channels для нових маршрутів не вимірювались.

## Основний finding A-11 (відмінний рівень)

Додаткова проблема на шляхах ЛР 1–2: `search` повертав записи й тексти опису чужих інцидентів (знайдено …0004 за `O'Brien`), а `severity-summary` агрегував усі п'ять записів, розкриваючи кількість і серйозність чужих інцидентів. **Fix:** умова видимості додана до самого запиту (search — до сортування й `Take(50)`; summary — до `GroupBy`); маршрути захищено `RequireAuthorization()`. **Після:** Alice — Medium 1 / Low 2 / High 0 і порожній пошук; Analyst — усі.

## Browser flow

Login на `auth-lab.html` → головна сторінка → власні details → Bob probe (`loadIncidentDetails`). Before: показано деталі Bob; after: «Помилка: Інцидент не знайдено», список обмежено трьома власними. Клієнт лише показує рішення сервера (`textContent`, `createTextElement`); `innerHTML` не використовується.

## Роль, transition, child, audit

- **Transition:** `POST /api/incidents/{id}/triage` — лише Analyst і лише `New → Triaged`; DTO не має status, next status встановлює сервер; запис у `IncidentStatusHistory`.
- **Child:** `GET /api/incident-comments/{id}` — коментар завантажується разом із parent; Reporter не бачить internal і коментарі чужих інцидентів.
- **Audit:** `authorization.denied subject=… action=incident.triage resource=incident:… result=denied occurredAtUtc=… traceId=…`; без cookie, пароля, token, body, stack trace; `traceId` = `Activity.Current?.Id ?? TraceIdentifier` — те саме значення, що в Problem Details.

## Відмінний рівень: обґрунтування

- **Handler і policy:** правило в `IncidentAccessRules` + два handler-и; endpoint-и не містять умов owner/role.
- **Видимість ресурсу:** чужий ресурс не відрізняється від неіснуючого (404 з тілом без id), тому існування не розкривається; 403 використовується лише коли суб'єкт і так бачить ресурс.
- **Least privilege:** Reporter — тільки власне; Analyst — читання всього й один перехід `New → Triaged`, без зміни вмісту та видалення; Administrator — без додаткових прав на Incident (в предметному правилі їх немає).
- **Охоплення:** details, update, delete, triage, child, list, search, severity-summary. Export/batch відсутні; concurrent transitions — не застосовується (сценарій викладач не надав).

## Перевірки, відтворення і подання

- Команди: `docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait`; `dotnet run --project src/SecureLab.Api -- --reset-database`; `dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release`.
- Результат: build без помилок, 84 тести з 84; два reset поспіль — по 84/84.
- Тег `v0.4.0` вказує на перевірений fixed commit (див. `git show v0.4.0`). Посилання на private remote — за погодженням.
- Не виконувалось: concurrent transitions (немає сценарію викладача); Visual Studio/Rider (використано VS Code).

## Використання ШІ

Використано генеративний ШІ (Claude, Anthropic) для підготовки коду, тестів і тексту за методичкою; код і результати перевірено власним запуском (PoC до/після, 84 тести, red-on-vulnerable, два reset, браузерний сценарій). Авторка пояснює рішення при захисті.

| Дата | Інструмент | Для чого | Що використано | Що перевірено самостійно |
|---|---|---|---|---|
| 2026-11-11 | Claude (Sonnet) | handler-и, scoping, тести A-01…A-12, PoC, звіт | код і текст | build, тести, PoC до/після, red на vulnerable commit, браузерні знімки |
