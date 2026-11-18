# F-01. Змінювальні cookie-authenticated маршрути без antiforgery-валідації (CSRF)

**Статус:** Fixed  
**Актив:** A3 власні Incident, A4 перехід `Incident.Status`, A1 сеанс (logout)  
**Компонент:** `IncidentMutationEndpoints.cs`, `Lab02Endpoints.cs` (POST /api/incidents), `Lab03Identity.cs` (logout), клієнт `Client/*.js`  
**Пріоритет:** P1

## Передумови і межі огляду

- Commit уразливого стану коду: `9ddd31f` (код ідентичний `v0.5.0`/`8d1ad57`); тести-відтворення: `12cd7af` (тег `lr06-vulnerable`).
- Development, `http` profile, локальна БД після `--reset-database`, тестові акаунти alice/bob/morgan. Дані штучні.
- Маршрути: SC-01…SC-05 з таблиці периметра (`docs/security-review.md`, розділ 8). Поза межами: HTTPS/HSTS на реальному домені, XSS (окремо — REG-XSS-01).

## Відтворення

1. alice входить (cookie сеансу є, request token **немає**) — `POST /api/auth/login` → 204.
2. Для ресурсу alice: `PUT /api/incidents/{id}` з `{"title":"CSRF-attacker"}` без header токена.
3. Очікувано: 4xx і незмінний ресурс. **Фактично (v0.5.0):** `200 OK`, `title` змінено. Те саме для `DELETE` (204, ресурс зник), `POST …/triage` (200, статус `Triaged`), `POST /api/incidents` (201, новий запис), `POST /api/auth/logout` (204, сеанс завершено).
4. Автоматично: `CsrfRegressionTests` на commit `12cd7af` — 11 із 11 червоних (`docs/evidence/lab-06/red_v0.5.0.txt`).

## Вплив і ймовірність

**Статус перевірки:** спостережено локально (HTTP-рівень: клієнт із автоматичною cookie без токена).

Сторонній сайт у браузері автентифікованого користувача може запускати змінювальні запити з його автоматичною cookie: зміна/видалення власних інцидентів, примусовий `logout`, для Analyst — перехід статусу. Impact: Medium–High (цілісність даних і привілейований перехід), likelihood: Medium (потрібен сторонній сайт у тому самому браузері; JSON-тіло потребує CORS-preflight для `fetch`, але `DELETE`, `POST …/triage` без тіла й атаки з same-site джерела preflight не мають; `SameSite=Lax` — лише часткове пом’якшення). Перші в черзі виправлення (P1): стосується усіх 5 cookie-маршрутів, а не одного.

## Першопричина

Жоден змінювальний маршрут не перевіряв, що запит ініційовано власним клієнтом: серверу вистачало самої authentication cookie. `UseAntiforgery()` middleware автоматично захищає лише form-data endpoint-и, тому JSON-маршрути й `DELETE` лишалися поза захистом; перевірку потрібно викликати явно.

## Виправлення

Fixed commit `4f57e9f`: `Security/AntiforgeryProtection.cs` — стандартний `IAntiforgery` (cookie `SecureLab.Antiforgery` + request token у header `X-CSRF-TOKEN`), маршрут видачі токена `GET /api/antiforgery/token` (`Cache-Control: no-store`), **єдиний** фільтр `AntiforgeryFilter` (`ValidateRequestAsync`; `AntiforgeryValidationException` → один точний `400` Problem Details `Antiforgery token is missing or invalid.`, а не 500; handler не викликається). Фільтр підключено до всіх п’яти cookie-маршрутів через `.RequireAntiforgery()`; login/register — явне `ExemptFromAntiforgery(reason)` (див. R-01). Клієнт: `Client/csrf.js` (токен лише в пам’яті, lazy перед першою mutation, очищення після login/logout, без автоповтору POST). Периметр не тримається на пам’яті: `CsrfPerimeterTests` читає `EndpointDataSource` і вимагає метадані antiforgery/виключення для кожного POST/PUT/PATCH/DELETE.

## Retest

`REG-CSRF-01` (`CsrfRegressionTests`): без header, з невалідним значенням і з токеном іншого сеансу → рівно `400 application/problem+json` (title `Antiforgery token is missing or invalid.`), стан ресурсу (title/description/status) незмінний; theory для PUT/DELETE/TRIAGE/CREATE/LOGOUT — той самий результат, сеанс після logout-CSRF лишається активним. Ручний trace: `docs/evidence/lab-06/csrf_manual_trace.txt`.

## Positive regression

Легітимний шлях (login → токен тим самим сеансом → header): `PUT` 200 і очікувана зміна; `DELETE` 204; `POST …/triage` 200 `Triaged`; `POST /api/incidents` 201; `logout` 204 і `/api/me` → 401. Браузер: `auth-lab.html` і `browser-lab.html` (`docs/evidence/lab-06/client_flow.txt`: 6 запитів несуть header, F5 → новий токен, 0 порушень CSP). Усі 99 попередніх тестів лишилися зеленими (127 разом).

## Залишковий ризик

Login/register не захищені antiforgery (R-01); токен доступний коду, що виконується на власному origin (XSS) — частково стримується CSP `script-src 'self'` і безпечним DOM; поведінку з простроченим токеном після тривалого простою вкладки перевірено лише в частині «400 → токен очищується» (без автоповтору).

## Доказ

Тести `CsrfRegressionTests`, `CsrfPerimeterTests`; команда `dotnet test SecureLab.sln -c Release` (127 passed); `docs/evidence/lab-06/csrf_manual_trace.txt`, `client_flow.txt`, `red_v0.5.0.txt`. Без значень cookie і токенів.
