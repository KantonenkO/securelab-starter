# Модель загроз SecureLab, ЛР 6

Рівень огляду: **відмінний** (накопичувальний: достатній + добрий + відмінний). Усі дані штучні, стенд локальний.

| Елемент | Що зафіксовано | Запис |
|---|---|---|
| Версія та середовище | commit початку огляду (review-start), профіль запуску, seed/reset, SDK | review-start `8d1ad57092294a63710885a0e7fcdd14a04652bb` (= `v0.5.0`, гілка `lab/6-hardening-review`); Development (`http` launch profile, `AllowReset=true`) для функціональних перевірок і Production-like (`--no-launch-profile`, `ASPNETCORE_ENVIRONMENT=Production`) для перевірки помилок; reset/seed: `dotnet run --project src/SecureLab.Api -- --reset-database` (лише Development); SDK `10.0.302` за `global.json` (`10.0.100`, `latestFeature`, `allowPrerelease=false`); лише тестові облікові записи alice/bob/morgan/admin |
| Компоненти в межах огляду | браузерний клієнт, API, PostgreSQL у Docker Compose, конфігурація, журнали | статичний клієнт `src/SecureLab.Api/Client` (index, auth-lab, browser-lab); ASP.NET Core 10 Minimal API (`Program.cs`, `Scaffolding/*`, `Presentation/*`); PostgreSQL 18 (`infra/compose.yaml`, порт `127.0.0.1:54329`); `appsettings*.json`, `infra/.env.example`; console-логи (`SecureLab.Auth`, `SecureLab.Audit`, ASP.NET Core). Redis **не** використовується |
| Активи | облікові дані, власні записи, привілейовані переходи, дані аудиту, секрети конфігурації | A1 сеансова cookie `.AspNetCore.Identity.Application` (HttpOnly, SameSite=Lax); A2 пароль / `PasswordHash` (хеш — не пароль); A3 власні `Incident` (право Reporter змінювати/видаляти лише свої); A4 привілейований перехід `Incident.Status` (`New → Triaged`, лише Analyst/Administrator) та `IncidentStatusHistory`; A5 audit events (`authorization.denied`, `incident.status.changed`); A6 connection string (`ConnectionStrings__SecureLab`) і `SeedUsers__Password` — лише поза Git |
| Суб’єкти | анонімний відвідувач, автентифікований користувач, moderator/admin, локальний розробник | anonymous; Reporter (alice, bob); Analyst (morgan); Administrator (admin); локальний розробник/студентка; **сторонній сайт у тому самому браузері** (CSRF-атакувальник) |
| Entry points | HTTP-маршрути, login, форми браузера, завантаження конфігурації, restore пакетів, журнали | усі маршрути з таблиці периметра (`docs/security-review.md`, SC-01…SC-07 + GET-маршрути); форми `auth-lab.html`, `browser-lab.html`; змінні середовища й `appsettings*.json`; `dotnet restore --locked-mode` (NuGet); console-логи |
| Межі довіри | browser→API, API→PostgreSQL, repository→build, environment→configuration | див. схему нижче |
| Наявні controls | validation, authentication, authorization, безпечна робота з DOM, CORS, CSP, antiforgery тощо | validation і ProblemDetails — `Lab02Endpoints.cs` (T-*); Identity + cookie, login limiter — `Lab03Identity.cs`, `LoginThrottle.cs` (A-*/E-*); resource-based authorization — `Application/Authorization/*` (A-*); безпечний DOM (`textContent`), CORS policy `Lab05BrowserProbe`, CSP — `Program.cs`, `Client/*.js` (REG-XSS-01, REG-CORS-01). **Antiforgery до ЛР 6 відсутній** |
| Виключення | що не перевірялося й чому | не виконувався зовнішній penetration test; немає production host/TLS termination/HSTS на реальному домені; Redis і розподілене сховище не входять; історія Git поза поточними tracked-файлами переглянута лише в межах `git log --all` для конфігураційних файлів (див. розділ 9 review); зовнішній CI та чужі fork-и недоступні |

## Схема меж довіри

```
browser client (untrusted input, automatic auth cookie, sites of other origins in the same browser)
      │  HTTP (local dev transport; HTTPS/HSTS terminate outside the project)
      ▼
ASP.NET Core API (validation, authn/authz, antiforgery, rate limiting, headers, logging)
      │  connection string (env) / EF Core, parameterized SQL
      ▼
PostgreSQL (application data)

repository + NuGet sources ──► restore/build (packages.lock.json, locked mode)
environment configuration ──► API startup (Development vs Production-like)
API ──► console log sink (diagnostic log + audit events; traceId links to client Problem Details)
```

## Очікувані властивості на межах

| Межа | Очікувана властивість (усі рівні) | Конкретний test або перевірка |
|---|---|---|
| browser→API | змінювальний запит із автоматичною cookie без валідної пари antiforgery-токенів відхиляється (4xx) без зміни стану; легітимний запит із токеном виконується | `REG-CSRF-01` (`CsrfRegressionTests`), `REG-CSRF-PERIMETER` (кожен змінювальний endpoint має antiforgery-метадані) |
| browser→API | текст користувача не виконується як код; CORS дозволяє лише точний origin | `REG-XSS-01` (`tests/browser/lab05-dom.js`), `REG-CORS-01` (`BrowserSecurityTests`) |
| browser→API | зловживання (підбір пароля, масовий пошук/реєстрація) обмежується за перевіреною ідентичністю або IP, не замінюючи authn/authz | `LoginHardeningTests` (login), `SearchRateLimitTests` (search, два partitions), `RegisterRateLimitTests` |
| API→PostgreSQL | параметр пошуку не змінює структуру SQL; чужий запис недоступний | `S-02` (`SearchSecurityTests`), `A-*` (`AuthorizationTests`) |
| API→client | помилка не розкриває stack trace/SQL/connection string; безпечні заголовки є і на 500; чутливі відповіді не кешуються | `ERR-01`/`HDR-01` (`ErrorPathTests`), `CACHE-01` |
| repository→build | tracked-файли не містять реальних credentials; restore не змінює lockfile непомітно | `git grep` (розділ 9 review), `dotnet restore --locked-mode` |
| environment→configuration | у Production не діє Development-конфігурація, reset заборонено, connection string поза Git | доказ `Hosting environment: Production` + `--reset-database` відмовлено (етап 1, п. 4) |
| API→log sink | журнали не містять password/cookie/token/body; audit event має subject, action, resource, result, time, traceId | `AuditTrailTests`, перегляд `lr06-logs/api.log` (6 сценаріїв) |

## Asset → threat → control → verification (ключові активи)

| Актив | Загроза | Control | Перевірка |
|---|---|---|---|
| A3 власні Incident | сторонній сайт у браузері Alice шле `PUT/DELETE` з її автоматичною cookie (CSRF) | antiforgery validation на всіх змінювальних cookie-маршрутах | `REG-CSRF-01` |
| A4 перехід статусу | CSRF на `POST …/triage` змінює статус від імені Analyst | той самий фільтр + resource-based handler + audit event | `REG-CSRF-01`, `AuditTrailTests` |
| A1 сеанс | login-CSRF / підміна сеансу; XSS читає токен | anonymous bootstrap токена, HttpOnly cookie, CSP `script-src 'self'`, токен лише в пам’яті JS | `REG-CSRF-01`, `REG-XSS-01`, CSP-перевірка в браузері |
| A2 облікові дані | перебір/spraying; enumeration через 409 на register | login limiter (ЛР 3), register limiter (F-04), однакові відповіді | `LoginHardeningTests`, `RegisterRateLimitTests` |
| A6 секрети | витік у Git/логи/відповідь 500 | `.gitignore`, `.env.example`, ProblemDetails, 500 без деталей | `git grep`, `ERR-01` |
| A5 audit | відсутній слід дозволеного привілейованого переходу | `incident.status.changed` + `authorization.denied` | `AuditTrailTests` |
