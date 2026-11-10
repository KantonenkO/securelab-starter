# Підготовка до лабораторної роботи № 1: один запит крізь усю систему

**Орієнтовний час:** 8–12 хвилин читання і 20–30 хвилин практики

**Перевірений starter:** `starter-v0.1.0`, commit `4aa18b3`, варіант 2-A

## Навіщо цей текст

У ЛР 1 потрібно не лише запустити готову систему, а й провести один запит від
дії на сторінці до рядка PostgreSQL і назад. Без такого маршруту неможливо
точно сказати, який компонент одержав дані, де виникла відповідь і що саме
доводить запис у DevTools Network.

Після підготовки ви маєте вміти за дві-три хвилини показати один шлях
`форма → fetch → API → DbContext → PostgreSQL → JSON → DOM` і назвати на ньому
щонайменше три межі довіри.

## Що потрібно вміти до початку

Якщо ви ще не складали й не запускали мінімальний ASP.NET Core API, опрацюйте
[міст C++ → C#/ASP.NET Core](../00-bridges/cpp-to-csharp.md). Якщо важко
прочитати готовий `fetch`, знайти request і response в Network або змінити текст
через `textContent`, спершу виконайте
[міст HTML → DOM → fetch → DevTools](../00-bridges/browser-dom-fetch.md).
Правила гілок і фіксації стану наведено окремо в
[Git workflow](../../docs/git-workflow.md).

## Механізм

Розглянемо один наскрізний маршрут:

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

Обробник події читає значення з DOM і передає його до `fetch`. Браузер не
викликає C#-метод безпосередньо: він формує HTTP-запит і надсилає його іншому
процесу. Тому пару `method + URL` потрібно шукати спочатку в Network, а вже
потім — у маршруті серверного коду. Той самий URL може підтримувати різні
methods.

**Кінцева точка API (endpoint)** — обробник, який routing обирає за method і
маршрутом. Дані request перетворюються на параметри endpoint або на **об’єкт
передавання даних (data transfer object, DTO)**. DTO задає зовнішній контракт:
які поля API приймає або повертає. **Entity** описує модель збереження. Це різні
ролі, тому наявність поля в entity не означає, що клієнтові дозволено його
надсилати або бачити.

Endpoint може передати роботу application service. Далі `DbContext` дає
доступ до моделі даних, а `DbSet` є початком побудови запиту для конкретного
типу entity. EF Core виконує операцію через з’єднання з PostgreSQL. Контейнер
PostgreSQL і процес API є окремими компонентами: успішний запуск API ще не
доводить, що база доступна. Рядок з’єднання належить до конфігурації та може
містити секрет, тому його не додають до запису HTTP-обміну або звіту.
**Міграція (migration)** фіксує версійну зміну схеми, а **початкові дані
(seed data)** дають змогу після reset повернути навчальний стенд до відомого
стану. У цій підготовці ви лише знаходите ці частини готової системи, а не
проєктуєте міграцію самостійно.

Після читання даних сервер створює response DTO. ASP.NET Core серіалізує його в
JSON і надсилає HTTP-відповідь зі status code, headers і body. `fetch` одержує
response, JavaScript читає JSON і виводить текст через `textContent`. Network
підтверджує фактичний HTTP-обмін, але не показує SQL-запит і сам собою не
доводить правильність серверної перевірки.

На маршруті є щонайменше три **межі довіри (trust boundaries)**:

1. браузер → API: method, URL, headers і body контролює клієнт;
2. API → PostgreSQL: сервер перетворює недовірені дані на операцію з даними;
3. API → браузер → DOM: навіть текст із БД може бути збереженим раніше
   користувацьким вводом, тому його не слід автоматично вважати безпечним.

## Чому саме так

Увесь практикум побудовано на вмінні локалізувати рішення в чужій системі.
Вразливість виникає не «десь у вебзастосунку», а тоді, коли конкретний компонент
не перевірив дані або право, одержані через конкретну межу. Якщо не можна
зіставити запис Network з endpoint і доступом до даних, не можна обґрунтовано
назвати ані місце перевірки, ані першопричину помилки.

## Перевірений приклад

Прикладом є готовий фільтр інцидентів у
[`starter-project`](../../starter-project/README.md). Якщо робота з `fetch` і
Network ще викликає труднощі, перед ним використайте незалежний
[`bridge-browser-fetch-dom`](../../one-time-examples/bridge-browser-fetch-dom/README.md).

03.08.2026 starter на тезі `starter-v0.1.0` перевірено з .NET SDK `10.0.302`.
Команда `bash scripts/test.sh` підняла PostgreSQL до стану `healthy`, зібрала
API й виконала всі `4/4` тести. Команди нижче — протокол попередньої авторської перевірки. Стенд запускайте командами етапу 1; цей протокол не замінює інструкцію. У PowerShell замість curl використовуйте curl.exe, а багаторядкові Bash-команди вводьте одним рядком без символу продовження.

Відомий seed-стан відновлено командою:

```bash
dotnet run --no-build --configuration Release \
  --project src/SecureLab.Api -- --reset-database
```

Після запуску API командою
`dotnet run --no-build --configuration Release --project src/SecureLab.Api`
фактично виконано:

```bash
curl -i 'http://localhost:5080/api/incidents?status=Triaged' \
  -H 'Accept: application/json'
```

Request мав method `GET`, query parameter `status=Triaged` і не мав body.
Спостережено status code `200`, response header
`Content-Type: application/json; charset=utf-8` і один JSON-об’єкт:

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

Read-only `SELECT` у PostgreSQL виконано командою:

```bash
docker compose --env-file infra/.env.example -f infra/compose.yaml \
  exec -T postgres psql -U securelab -d securelab \
  -c "SELECT id, title, severity, status FROM incidents WHERE status = 'Triaged';"
```

Результат підтвердив той самий `id`, `title`, `severity` і `status` у таблиці
`incidents`.

### Три вправи у starter

Виконуйте після етапу 1, коли PostgreSQL та API вже працюють.

1. Відкрийте сторінку `http://localhost:5080/` і DevTools Network. У фільтрі
   виберіть `Triaged` та натисніть «Застосувати». Знайдіть
   `GET /api/incidents?status=Triaged`; перевірте відсутність request body,
   status code `200` і наведений вище response body.
2. Простежте цей запис у коді. У
   [`Client/app.js`](../../starter-project/src/SecureLab.Api/Client/app.js)
   рядки 81–90 формують URL і викликають `apiFetch`. У
   [`IncidentEndpoints.cs`](../../starter-project/src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs)
   рядки 11–17 зіставляють `GET` із `GetListAsync`, а рядки 34–54 перевіряють
   `status`. У
   [`IncidentQueries.cs`](../../starter-project/src/SecureLab.Api/Application/Incidents/IncidentQueries.cs)
   рядки 16–31 будують запит через `dbContext.Incidents` і створюють
   `IncidentListItemResponse`. У
   [`SecureLabDbContext.cs`](../../starter-project/src/SecureLab.Api/Data/SecureLabDbContext.cs)
   рядки 10 і 27–35 пов’язують `DbSet<Incident>` із таблицею `incidents`.
   Seed-рядок визначено в
   [`DbSeeder.cs`](../../starter-project/src/SecureLab.Api/Data/DbSeeder.cs),
   рядки 56–67.
3. Виконайте
   `GET /api/incidents/99999999-9999-9999-9999-999999999999`. Перевірено status
   code `404`, `Content-Type: application/problem+json`, title
   `Інцидент не знайдено` і detail із переданим UUID. Response також містить
   змінний `traceId`. Рішення про `404` ухвалює `GetDetailsAsync` у
   `IncidentEndpoints.cs`, рядки 62–68, після того як query повернув `null`.

## Типова хибна контрміра

Твердження «кнопку приховано, тому операцію неможливо викликати» плутає
інтерфейс із серверним контролем. Будь-який HTTP-клієнт може повторити request
без кнопки. Так само `required` або `maxlength` обмежує одну форму, а не всі
запити до API. Сервер повинен перевіряти недовірені дані незалежно від того,
які обмеження показав браузер.

Ще одна хибна ознака — «одержали `200`, отже все працює правильно». Status code
описує результат конкретного HTTP-обміну. Він не доводить, що повернено
правильний рядок, застосовано всі правила або не розкрито зайвих полів.

## Що залишається на лабораторну роботу

У ЛР 1 ви самостійно проходите маршрут спільного baseline 2-A, заповнюєте
`docs/architecture.md`, додаєте невелику наскрізну функцію, готуєте `.http`
сценарій або інтеграційний тест, перевіряєте seed/reset, logging і відсутність
секретів у diff та створюєте тег `v0.1.0`. Цей текст не містить готового
endpoint, матриці перевірок або реалізації вашої функції.

## Запитання для самоперевірки

1. Чому для пошуку endpoint потрібні одночасно method і URL?
2. Який метод визначає відповідь `404` для відсутнього інциденту і після якого
   результату query він це робить?
3. Назвіть три межі довіри на простеженому маршруті та дані, які їх перетинають.
