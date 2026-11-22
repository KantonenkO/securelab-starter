# Карта архітектури

Карта ЛР 1: маршрут `severity-summary`, ключові файли, межі довіри та конфігураційні входи.

## Компоненти

| Компонент | Розташування | Відповідальність |
|---|---|---|
| Browser client | `src/SecureLab.Api/Client/` | Надсилає HTTP-запити, безпечно показує відповідь через DOM API |
| Presentation | `Presentation/` | Описує endpoints, читає зовнішні параметри, формує HTTP-відповідь |
| Application | `Application/` | Виконує сценарій отримання списку або деталей інциденту |
| Data | `Data/` | Відображає C#-сутності на PostgreSQL через EF Core/Npgsql |
| PostgreSQL | `infra/compose.yaml` | Зберігає навчальні дані у локальному контейнері |

## Мій маршрут: підсумок за severity

Вибраний маршрут: `GET /api/incidents/severity-summary` з необов'язковим `?status=`.

```text
кнопка «Показати підсумок» (#summary-button) у Client/index.html
  → обробник loadSeveritySummary у Client/app.js (apiFetch)
  → GET /api/incidents/severity-summary[?status=Triaged]
  → Presentation/Endpoints/IncidentEndpoints.cs  (GetSeveritySummaryAsync, перевірка status)
  → Application/Incidents/IncidentQueries.cs      (GetSeveritySummaryAsync: Where → GroupBy → Count)
  → Data/SecureLabDbContext.cs  (DbSet Incidents)
  → таблиця incidents у PostgreSQL
  → IncidentSeveritySummaryResponse(severity, count) у Presentation/Contracts/
  → JSON-масив
  → li.textContent у Client/app.js
```

Ключові файли:

| Роль | Файл |
|---|---|
| Клієнт | `src/SecureLab.Api/Client/index.html`, `Client/app.js` |
| Endpoint | `Presentation/Endpoints/IncidentEndpoints.cs` |
| Application layer | `Application/Incidents/IncidentQueries.cs` |
| Response DTO | `Presentation/Contracts/` (`IncidentSeveritySummaryResponse`) |
| DbContext і таблиця | `Data/SecureLabDbContext.cs`, таблиця `incidents` |
| Сценарії | `tests/http/incidents.http`, `tests/SecureLab.Api.Tests/IncidentEndpointTests.cs` |

Контракт: `200 OK` і масив `{ severity, count }` для кожного рівня severity (нульові теж).
Параметр `status` перевіряється за allowlist `New, Triaged, InProgress, Resolved, Closed`;
інше значення дає `400 Validation Problem Details`. Entity не віддається, лише DTO.

## Межі довіри

| Межа | Чому даним ще не можна довіряти | Де перевіряємо або обмежуємо |
|---|---|---|
| Користувач → Browser client | Користувач контролює все, що вводить | Клієнт нічого не вставляє як HTML: лише `textContent` |
| Browser client → API | Запит можна змінити поза UI (curl, DevTools), у т. ч. `status` | Allowlist `status` на сервері, `400` для невідомого значення |
| API → PostgreSQL | Значення з query не можна вставляти в SQL | LINQ/EF Core параметризує запит, SQL-рядків немає |
| PostgreSQL → API → DOM | У БД може бути раніше введений недовірений текст | Response DTO без зайвих полів; безпечний DOM sink (`textContent`/`createTextNode`) |

Дані, що перетинають межі: `status` (рядок з URL), JSON із `severity` та `count`,
текст інцидентів (`title`, `description`) на шляху details.

## Конфігураційні входи

- `global.json` — версія .NET SDK;
- `src/SecureLab.Api/appsettings*.json` — режим міграцій і локальний connection string;
- `infra/compose.yaml` — версія PostgreSQL, порт і локальні навчальні облікові дані;
- змінна середовища `ConnectionStrings__SecureLab` — безпечний спосіб перевизначити connection string поза репозиторієм.

Реальні значення паролів і connection string у репозиторій не записуємо.

## Повернення до відомого seed-стану

```text
dotnet run --project src/SecureLab.Api -- --reset-database
```

Команда перестворює локальну БД і заново застосовує seed (три інциденти: Triaged, InProgress, New).
