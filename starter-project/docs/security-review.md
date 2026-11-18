# Security review SecureLab, ЛР 6

Рівень: **відмінний («Відмінно»)** (обрано до етапу 1; рівні накопичувальні). Пункт «погоджений advanced variant» **не виконано** — погодження з викладачем не отримано (див. розділ 8.4 і 10). Commit початку огляду (review-start): `8d1ad57092294a63710885a0e7fcdd14a04652bb` (= тег `v0.5.0`). Хеші прийнятого merge commit і тегу `v1.0.0` додає скрипт експорту в PDF (поза tracked docs). Дата огляду: 2026-11-18. SDK: 10.0.302 (`global.json` 10.0.100, `latestFeature`, без prerelease). Клієнти: `curl`, HttpClient (xUnit), Microsoft Edge 157 (CDP), VS Code. Профілі: Development (`--no-launch-profile` + env) і Production-like.

## 1. Executive summary

Перевірено стан `v0.5.0` (commit `8d1ad57`) у межах локального навчального стенда (API + PostgreSQL у Docker + статичний клієнт). Зафіксовано й виправлено **5** знахідок: F-01 (P1) cookie-authenticated mutations без antiforgery; F-02 (P2) заголовки безпеки зникали на 500; F-04 (P2) публічна реєстрація без rate limit; F-03 (P3) відповіді API без `Cache-Control`; F-05 (P3) немає audit event дозволеного переходу статусу. Фінальний прогін (tip гілки й повторно на прийнятому merge commit): `dotnet test SecureLab.sln -c Release` — 127 тестів, 0 failed (99 успадкованих + 28 нових). Головні залишкові ризики: login-CSRF/register (R-01, прийнято з компенсувальними controls), in-memory лічильники лише для одного інстансу (R-02), відсутність реального HTTPS/HSTS (R-03), токен доступний коду власного origin при XSS (R-04).

## 2. Scope та exclusions

**У межах:** усі tracked-файли поточного стану (`git ls-files`), ASP.NET Core API, статичний клієнт `Client/*`, PostgreSQL у Docker Compose, `appsettings*.json`, `infra/*`, restore/build, журнали консолі. **Історія Git:** переглянуто лише `git log --all` для конфігураційних файлів (`appsettings.Development.json`, `infra/compose.yaml`, `.env*`) — одна початкова версія, жодних `.env`/`appsettings.Local.json` в історії; решту історії, stash, інші clones, fork-и й зовнішній CI **не перевірено**. **Поза межами:** зовнішній penetration test; реальний TLS/HSTS/reverse proxy; Redis і розподілене сховище; file upload (не існує); продуктивність; сторонні host-и (усі запити лише на localhost / 127.0.0.1).

## 3. System overview і trust boundaries

Схема і таблиця активів — `docs/threat-model.md`. Зв’язок «межа → перевірка»:

| Межа | Очікувана властивість | Конкретний test / перевірка |
|---|---|---|
| browser→API | cookie без request token не змінює стан | `REG-CSRF-01`, `REG-CSRF-PERIMETER` (`CsrfRegressionTests`, `CsrfPerimeterTests`) |
| browser→API | текст не виконується як код; CORS exact origin | `REG-XSS-01` (DOM-тест + `BrowserSecurityTests`), `REG-CORS-01` |
| browser→API | зловживання обмежене | `LoginHardeningTests`, `RL-SEARCH-01`, `RL-REG-01` |
| API→PostgreSQL | параметр пошуку не змінює SQL | `S02_*` (`SearchSecurityTests`) |
| API→client | 500 безпечна, заголовки є, відповіді не кешуються | `ERR-01/HDR-01` (`ErrorPathTests`), `CACHE-01` |
| repository→build | немає реальних credentials; lockfile | `git grep` (розділ 9), `dotnet restore --locked-mode` |
| environment→configuration | у Production немає Development-конфігурації, reset заборонено | `production_normal.txt`, `production_reset_refused.txt` |
| API→log sink | журнали без secret/body; audit із traceId | `AuditTrailTests`, `log_sensitive_check.txt` |

## 4. Methodology та test environment

Відомий початковий стан: review-start `8d1ad57` (`v0.5.0`); гілка `lab/6-hardening-review`; локальна БД `securelab` (контейнер `secure-lab-postgres-1`, порт 54329), reset/seed командою `dotnet run --project src/SecureLab.Api -- --reset-database` перед кожним контрольованим експериментом і перед фінальним прогоном. `SeedUsers__Password` — лише змінна середовища (значення не друкується й не зберігається в Git). Порядок: (1) vulnerable commit `12cd7af` (тег `lr06-vulnerable`): нові тести червоні на коді `v0.5.0` (22 із 24); (2) fixed commits `4f57e9f` (F-01), `49a5fad` (F-02/F-03/F-04), `180096f` (F-05): зелені; (3) docs; (4) merge `--no-ff` у `main`, final regression на merge commit, анотований тег `v1.0.0`. Усі evidence — у `docs/evidence/lab-06/` (очищені: без cookie, токенів, паролів, connection string).

## 5. Findings summary

| ID | Актив | Вплив | Ймовірність | Пріоритет | Статус | Retest / регресія |
|---|---|---|---|---|---|---|
| F-01 CSRF: mutations без antiforgery | A3, A4, A1 | Medium–High | Medium | **P1** | Fixed (`4f57e9f`) | `REG-CSRF-01`, `REG-CSRF-PERIMETER` |
| F-02 Заголовки безпеки зникають на 500 | browser defense in depth | Low | Medium | **P2** | Fixed (`49a5fad`) | `ERR-01`, `HDR-01` |
| F-04 `register` без rate limit | A2 | Medium | High | **P2** | Fixed (`49a5fad`) | `RL-REG-01` |
| F-03 API без `Cache-Control` (поза checklist) | A3, профіль | Low–Medium | Low | P3 | Fixed (`49a5fad`) | `CACHE-01` |
| F-05 Немає audit event дозволеного переходу | A4, A5 | Low | Medium | P3 | Fixed (`180096f`) | `AUD-01` |

**Порядок виправлення (обґрунтування).** F-01 першою: збігаються найвищий impact (зміна/видалення ресурсів і привілейований перехід), найширша поверхня (5 маршрутів) і низький поріг атаки (автоматична cookie). F-02 і F-04 мають однакову ймовірність/середній impact, але F-04 доступний анонімно без передумов, тому виправлялася ближче до F-01; F-02 впливає лише на defense in depth. F-03 і F-05 — дешеві, без віддаленого прямого впливу. Тимчасово прийнятих ризиків серед знахідок немає; прийняті залишкові ризики — розділ 10. Третя й четверта знахідки (F-03, F-04) знайдено власним аналізом поза прямим checklist (таблиця заголовків «по класах» і повторна перевірка login limiter), а не з підказки методички; error-path gap (F-02) методичка називає прямо.

## 6. Detailed findings

Окремі картки: `docs/findings/F-01-csrf-antiforgery-missing.md`, `F-02-headers-missing-on-500.md`, `F-03-api-responses-cacheable.md`, `F-04-register-unthrottled.md`, `F-05-status-transition-audit-missing.md` (відтворення, вплив, першопричина, виправлення, retest, positive regression, залишковий ризик, доказ).

## 7. Regression results

Команда: `dotnet test SecureLab.sln -c Release` (SDK 10.0.302, `SeedUsers__Password` зі змінної середовища) після `--reset-database`; результат прогону на tip гілки до злиття — Passed 127, Failed 0 (`docs/evidence/lab-06/final_regression.txt`); той самий прогін повторено на прийнятому merge commit після злиття (результат і хеші — в експорті `lr06-submission`, tracked docs після тегу не редагуються). Початкові ID сценаріїв ЛР 2–5 збережено.

| Рівень | ID сценарію | Property | Передумови | Негативний сценарій | Evidence / command | Позитивна перевірка | Result |
|---|---|---|---|---|---|---|---|
| достатній | `S-02` (`SearchSecurityTests.S02_*`, `T04_*`) | query не змінює структуру SQL | seed | read-only SQLi-сценарій ЛР 2 (`' OR 1=1 --` тощо як literal) | `dotnet test --filter SearchSecurity` | `S02_PositiveControl_UsbReturnsExactlyOneKnownRecord`, `T04_ApostropheAndNormalSearch` | PASS |
| достатній | `A-01…A-07`, `E-*` (`AuthSecurityTests`, `RegisterTests`, `LoginHardeningTests`) | identity lifecycle | test account | invalid/anonymous login, forged owner | `dotnet test --filter AuthSecurity` | `A02_Login_ThenMe…`, `A06_Logout_Then…401` | PASS |
| достатній | `A-01…A-12` (`AuthorizationTests`) | server-side ownership/role check | Alice/Bob seed | запит до чужого object → 404/403 | `dotnet test --filter AuthorizationTests` | owner/role-permitted action (`A-*` allow-тести, `New→Triaged`) | PASS |
| достатній | `REG-XSS-01` (`tests/browser/lab05-dom.js`, `BrowserSecurityTests`) | text не виконується як code | local marker | marker before/fixed behavior | `docs/evidence/lab-06/reg_xss_dom_test.txt` (Edge 157) | normal complex text відображається | PASS |
| достатній | `REG-CSRF-01` (`CsrfRegressionTests`) | token pair required | authenticated cookie + reset | без токена / invalid / чужий токен → 400, стан незмінний | `dotnet test --filter CsrfRegression` | valid pair → success, зміна стану | PASS (127/127) |
| добрий | `T-02` (`CreateContractTests.T02_*`) | server validation | login + чинний токен | недопустимий input → 400 field key | `dotnet test --filter CreateContract` | допустимий input зберігається (`T01`) | PASS |
| добрий | `REG-CORS-01` (`BrowserSecurityTests.Cors_*`) | exact browser origin scope | два development origins | disallowed origin 4174 → без `Access-Control-*` | `reg_cors_http.txt` | allowed origin 4173 → exact `ACAO` + credentials | PASS |
| добрий | `RL-SEARCH-01` (`SearchRateLimitTests`) | abuse flow limited | login Alice/Bob | request beyond quota → 429 + `Retry-After` | `rl_search_manual.txt` | within-limit і recovery after window → 200 | PASS |
| добрий | `RL-REG-01` (`RegisterRateLimitTests`) | abuse flow limited | публічний endpoint | quota → 429 | тест | within-limit, recovery → 201 | PASS |
| добрий | login limiter ЛР 3 (`LoginHardeningTests.RateLimit_…`) | abuse flow limited | seed | quota → 429, той самий вигляд для known/unknown | `login_limiter_manual.txt` | відновлення після вікна → 204 | PASS |
| добрий | `ERR-01/HDR-01` (`ErrorPathTests`) | безпечна 500, headers на error-шляху | Production host + порт 54399 | необроблена помилка | `unexpected_error_*.txt` | заголовки 200 збігаються | PASS |
| добрий | `CACHE-01`, `AUD-01` | no-store; audit event | login | — | `CacheControlTests`, `AuditTrailTests` | дозволений перехід `New→Triaged` | PASS |
| відмінний | `REG-CSRF-01` повна автоматизація | login/cookie → відмова без токена → стан незмінний → токен → успіх → стан змінений | `CsrfRegressionTests.REG_CSRF_01_Put_…` | (див. рядок вище) | `dotnet test --filter "FullyQualifiedName~CsrfRegression"` | PUT: title змінено на очікуване значення, description незмінний | PASS |

Вимога «відмова без токена → читання незмінного стану → видача токена → успішна зміна → читання зміненого стану» виконується в одному тесті `REG_CSRF_01_Put_WithoutOrInvalidToken_Rejected_ResourceUnchanged_ThenValidPairSucceeds` на реальному cookie-клієнті (`CookieClient.LoginAsync`) і реальному маршруті видачі токена; arrange ресурсу — напряму через DbContext, щоб не залежати від перевірюваного захисту.

## 8. Hardening controls

### 8.1 Таблиця периметра змінювальних маршрутів (етап 2, п. 3)

Джерела: `git grep -n -E "\.Map(Post|Put|Patch|Delete)\(" -- src` і `git grep -n -E "method: *.(POST|PUT|PATCH|DELETE)" -- src/SecureLab.Api/Client` (`perimeter_grep.txt`), OpenAPI (`openapi_mutations.txt`, 7 операцій), клієнтські виклики. Розбіжності: SC-02/03/04 є на сервері й в OpenAPI, але клієнтського виклику немає (**DISCREPANCY**: UI-кнопки для PUT/DELETE/triage відсутні) — маршрут усе одно захищено, бо CSRF оцінює HTTP endpoint, а не видимість інтерфейсу.

| ID | Method + route | Джерело / розбіжність | Яка зміна стану | Cookie authentication |
|---|---|---|---|---|
| SC-01 | `POST /api/incidents` | `Lab02Endpoints.cs`, клієнт `auth-lab.js`, `browser-lab.js`, OpenAPI | створення Incident | так |
| SC-02 | `PUT /api/incidents/{id}` | `IncidentMutationEndpoints.cs`, OpenAPI; **DISCREPANCY** (клієнта немає) | зміна title/description | так |
| SC-03 | `DELETE /api/incidents/{id}` | те саме; **DISCREPANCY** | видалення Incident | так |
| SC-04 | `POST /api/incidents/{id}/triage` | те саме; **DISCREPANCY** | перехід `New → Triaged` | так |
| SC-05 | `POST /api/auth/logout` | `Lab03Identity.cs`, клієнт `auth-lab.js` | завершення сеансу | так (cookie-периметр) |
| SC-06 | `POST /api/auth/login` | `Lab03Identity.cs`, клієнт | створення сеансу | **ні** (pre-auth) |
| SC-07 | `POST /api/auth/register` | `Lab03Identity.cs`, клієнт | створення акаунта | **ні** (public) |

| ID | Browser-accessible | Antiforgery validation | Regression ID |
|---|---|---|---|
| SC-01 | так | explicit: `AntiforgeryFilter` (`ValidateRequestAsync`), `.RequireAntiforgery()` | `REG-CSRF-01` (CREATE) |
| SC-02 | так (fetch/cross-site; UI відсутній) | explicit | `REG-CSRF-01` (PUT, повна автоматизація) |
| SC-03 | так | explicit | `REG-CSRF-01` (DELETE) |
| SC-04 | так | explicit | `REG-CSRF-01` (TRIAGE) |
| SC-05 | так | explicit, перевірка ДО `SignOutAsync` (фільтр виконується до handler) | `REG-CSRF-01` (LOGOUT) |
| SC-06 | так | **absent за рішенням**, `ExemptFromAntiforgery(reason)`; compensating: login limiter (IP+ім’я, IP), JSON-only (form-POST дає 415), CORS без дозволеного origin, `SameSite=Lax`; login-CSRF прийнято як **R-01** | `REG-CSRF-PERIMETER` (виняток лише для login/register, з причиною) |
| SC-07 | так | **absent за рішенням**; compensating: `RL-REG-01`, однакові відповіді, JSON-only | `REG-CSRF-PERIMETER`, `RL-REG-01` |

Периметр захищено автоматично: `CsrfPerimeterTests` читає `EndpointDataSource`, вимагає antiforgery-метадані або явне виключення з причиною для кожного POST/PUT/PATCH/DELETE і фіксує точний перелік (5 захищених + 2 виключення).

### 8.2 Контракт antiforgery (етап 2, п. 4)

Header: `X-CSRF-TOKEN`; маршрут видачі: `GET /api/antiforgery/token` (same-origin, `Cache-Control: no-store`, `Set-Cookie: SecureLab.Antiforgery`, cookie HttpOnly, SameSite=Strict, Secure у non-Development); перший токен після login видає новий principal (токен прив’язаний до identity). Клієнт: `Client/csrf.js` — токен лише в пам’яті сторінки, lazy перед першою mutation, очищення після login/logout, F5 → новий токен, без автоповтору POST. Спільний спосіб валідації — один `AntiforgeryFilter`; помилка → один `400` Problem Details (`Antiforgery token is missing or invalid.`). Усі змінювальні дії клієнта (форми register/login/incident/logout, `browser-lab`) пройдено в браузері: `client_flow.txt`.

### 8.3 Headers, cookie, CORS, CSP, HTTPS (етап 2, п. 1, 2, 6)

Фактичні значення по п’яти класах — `headers_dev.txt` (Development), `production_normal.txt` (Production, реальний сеанс), `unexpected_error_response.txt` (500).

| Клас | Profile/client | Method + route | Status | Фактичні значення | Ризик | Межа |
|---|---|---|---|---|---|---|
| статична сторінка | Development і Production, curl | `GET /` | 200 | `X-Content-Type-Options: nosniff`; `Referrer-Policy: no-referrer`; `Content-Security-Policy: default-src 'self'; script-src 'self'; …; frame-ancestors 'none'` | MIME sniffing, referrer leakage, injection, clickjacking | не замінює правильний `Content-Type` і safe DOM |
| успішний API | curl + сеанс alice | `GET /api/incidents/{own}` | 200 | ті самі + `Cache-Control: no-store` | кешування персональних даних | не замінює authorization |
| validation | той самий сеанс | `GET /api/incidents?status=Unknown` | 400 | ті самі + `no-store`, `application/problem+json` | — | — |
| unknown resource | той самий сеанс | `GET /api/incidents/9999…` | 404 | ті самі + `no-store` | — | не відрізняє чужий від неіснуючого (ЛР 4) |
| unexpected error | Production, порт 54399, curl | `POST /api/auth/login` | 500 | ті самі (виправлено F-02) + `Cache-Control: no-cache,no-store` від handler | втрата захисту на error-шляху | тіло: лише safe Problem Details + `traceId` |

- **CSP** (раніше була в ЛР 5, N/A не потрібне): `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`. Нова клієнтська частина (`csrf.js`) — зовнішній файл `'self'`, без inline. **Клієнт не зламано:** 0 CSP-порушень у Console при повному потоці (login → створення → F5 → створення → logout → `browser-lab`), `client_flow.txt`; 4 записи Console — очікувані 4xx (один 400 негативного запиту без токена і три 401 після logout). `frame-ancestors` задано окремо від `default-src` (не успадковується).
- **CORS (REG-CORS-01):** policy `Lab05BrowserProbe` реєструється лише в Development; exact origin `http://localhost:4173`, метод `GET`, header `X-Lab-Cors-Probe`, `AllowCredentials`; `reg_cors_http.txt`: 4173 → `ACAO: http://localhost:4173` + credentials, 4174 → жодного `Access-Control-*`, preflight `DELETE` для 4173 → дозволено лише `GET`. CORS не замінює authn/authz і не є захистом від CSRF (direct client із cookie проходить так само).
- **Cookie:** сеансова cookie `HttpOnly`, `SameSite=Lax`, `Secure` у Production (`SecurePolicy.Always`, `Secure=TRUE` у cookie jar Production-запуску), 2 год sliding; antiforgery cookie HttpOnly, `SameSite=Strict`. `SameSite` і CORS **не замінюють** antiforgery.
- **HTTPS/HSTS:** `launchSettings.json` має лише `http` на `http://localhost:5080`, `UseHttpsRedirection()`/`UseHsts()` у `Program.cs` відсутні, TLS не завершується застосунком. Local HTTP-профіль **не є доказом** production transport safety; HSTS не додавалося «для чек-ліста» (на loopback `ExcludedHosts` його й так пропускає). Реальний deployment, TLS termination і HSTS-відповідь **не тестувалися**; `AllowedHosts: "*"` — фільтрацію Host вимкнено (прийнято для локального стенда, R-03).

### 8.4 Rate limiting (етап 2, п. 7) і advanced variant

- **Login limiter (ЛР 3) — збережено й повторно перевірено** (`login_limiter_manual.txt`, `LoginHardeningTests`): ключі «IP|ім’я» і «IP»; 3 спроби в межах квоти → 401; наступна → 429 `Retry-After`; правильний пароль у вікні теж 429; вигадане й справжнє ім’я дають однакову форму; інший акаунт з того ж IP не заблоковано; після вікна легітимний вхід → 204.
- **Search limiter (новий):** policy `search`, fixed window, partition key = перевірений user id (`ClaimTypes.NameIdentifier`; невпізнаний запит ключується IP, але до цього місця не доходить — 401), квота/вікно з `RateLimiting:Search` (за замовчуванням 30/хв). Параметр пошуку — `q` з ЛР 2. Перевірено на **двох partitions** (alice, bob): квота alice вичерпана → 429 + `Retry-After`; bob → 200; маршрут без policy → 200; після вікна alice → 200; анонім → 401 (limiter стоїть після `UseAuthorization`, не замінює authentication); авторизація лишається окремою (чужий ресурс → 404 незалежно від квоти). `rl_search_manual.txt`, `SearchRateLimitTests`.
- **Register limiter** — F-04.
- **Advanced variant:** **Не входить до обраного рівня (не виконано).** Redis/рівноцінне розширення потребує попереднього погодження з викладачем (мета, межі, спосіб перевірки), якого немає. Підготовлено лише чернетку повідомлення (`docs/advanced-variant-proposal.md`), яка не надсилалась. Це єдиний пункт відмінного рівня, що лишається невиконаним; його не позначено PASS.

### 8.5 Змістові відмінності Development і Production

| Властивість | Development | Production-like | Доказ |
|---|---|---|---|
| `Hosting environment` | Development | Production (`--no-launch-profile`) | `production_normal.txt` |
| OpenAPI/Scalar | `/openapi/v1.json` 200, `/scalar` | `/openapi/v1.json` → 404 Problem Details; `/scalar/v1` → 200 `text/html` (**SPA fallback клієнта, не Scalar**) | `production_normal.txt` |
| CORS policy | зареєстровано | не реєструється (`Cors_Policy_IsNotRegisteredOutsideDevelopment`) | `BrowserSecurityTests` |
| `ApplyMigrationsOnStartup`/`AllowReset` | true/true | false (appsettings.json) | `production_reset_refused.txt` — `--reset-database` відмовлено `InvalidOperationException`, дані не змінено |
| Connection string | `appsettings.Development.json` (відкрите навчальне значення, 127.0.0.1) | лише зі змінної `ConnectionStrings__SecureLab` | `production_normal.txt` |
| Cookie `Secure` | `SameAsRequest` | `Always` | `CookieFlagsTests`, `Secure=TRUE` у jar |
| Помилка 500 | safe Problem Details | safe Problem Details (спільний `AddProblemDetails()+UseExceptionHandler()`) | `unexpected_error_response.txt` |

Протected 4xx у Production перевірено реальним сеансом (400 `Validation Problem Details`, 404 Problem Details — `production_normal.txt`); curl трактує `http://localhost` як secure context і надсилає `Secure` cookie, тому 401 не підміняв очікуваних 400/404. Це **не** доказ TLS.

## 9. Dependency and secret audit

### 9.1 Конфігурація і секрети (етап 1, п. 3)

| Path / збіг | Безпечний контекст | Рішення | Причина й межа |
|---|---|---|---|
| `infra/compose.yaml`: `POSTGRES_PASSWORD: local-study-password` | пароль локального контейнера PostgreSQL, порт `127.0.0.1:54329` | **accepted, study-only** | відкрите навчальне значення, README забороняє повторне використання; не поширюється на інші БД/середовища |
| `appsettings.Development.json`: connection string `…Password=local-study-password…` | той самий локальний стенд; лише Development | accepted, study-only | у Production не діє; реальний connection string — через `ConnectionStrings__SecureLab` |
| `SecureLabDesignTimeDbContextFactory.cs` | design-time для `dotnet ef`, той самий локальний стенд | accepted, study-only | — |
| `SeedUsers:Password` (`DbSeeder.cs`) | код читає ключ; значення задають поза Git | не секрет; джерело конфігурації | `SeedUsers__Password` лише в середовищі; у tracked-файлах немає |
| `Password` у DTO/`Lab03Identity.cs`/`auth-lab.html` | назви полів форми/DTO | не секрет | значення немає |
| `Microsoft.Extensions.Configuration.UserSecrets` / `packages.lock.json` | ідентифікатор пакета | не секрет | назва залежності |
| тести (`TestPassword.cs`, `…Tests.cs`) | читання `SeedUsers__Password`; вигадані значення (`NotARealPassword1!`, `Dummy-Password-For-Timing-1!`) | не секрет | синтетичні значення, не використовуються за межами тестів |

`.env.example` містить лише `POSTGRES_PORT=54329` (не секрет); `.gitignore` виключає `.env`, `.env.*` (крім `.env.example`), `appsettings.Local.json`, `*.pem`, `*.key`, `*.pfx`, dumps і `*.log` (`git check-ignore -v` підтверджує, exit code 0). Команди й повний вивід: `secrets_config_review.txt`, `git_grep_secrets.txt`; за методичкою `git grep -n -I -i -E 'password|passwd|secret|api.?key' -- .` дав сотні рядків (кількість — у файлі); кожну групу класифіковано за path-групою, жодного реального credential не виявлено. Просте видалення навчального значення з поточного файлу **недостатнє** для реального витоку: значення залишилося б в історії, clones, cache, log і fork — потрібна ротація credential; тут це відкрите навчальне значення, прив’язане до `127.0.0.1`, тому рішення `accepted, study-only`. Історію Git перевірено лише для конфігураційних файлів (див. розділ 2).

### 9.2 Залежності (етап 3, п. 1–3)

Команди: `dotnet --version` (10.0.302), `dotnet restore SecureLab.sln --locked-mode` (exit 0), `dotnet list SecureLab.sln package --vulnerable --include-transitive` (exit 0): для `SecureLab.Api` і `SecureLab.Api.Tests` джерело `api.nuget.org` не повідомило про відомі advisories для версій із `packages.lock.json` (`dependency_audit.txt`). Межа висновку: лише відомі на дату 2026-11-18 advisories цього джерела; досяжність коду й невідомі вразливості не оцінювались. Код завершення 0 не використано як доказ безпеки.

| Package | Version | Direct / transitive | Advisory / severity | Чи використовується relevant functionality | Доступне оновлення | Рішення і причина |
|---|---|---|---|---|---|---|
| Microsoft.OpenApi | 2.7.5 | direct (явний override) + transitive через Microsoft.AspNetCore.OpenApi (`dotnet nuget why`) | GHSA-v5pm-xwqc-g5wc у транзитивній лінії 2.x (виправлено явним pin на 2.7.5 ще в ЛР 1; коментар у `.csproj`) | OpenAPI генерація (лише Development) | 3.11.1 (major) | **keep**: поточна версія виправлена; blind major update не робиться — може змінити API/поведінку; перегляд після нової advisory |
| Scalar.AspNetCore | 2.16.16 | direct | немає | UI довідника, лише Development (`MapScalarApiReference` під `IsDevelopment`) | 3.0.1 (major) | keep; у Production endpoint відсутній (SPA fallback) |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.EntityFrameworkCore, …Relational, …Design, Microsoft.AspNetCore.OpenApi | 10.0.10 | direct | немає | так | 11.0.0 (major) | keep: patch-лінія 10.0.x; major міняє target framework |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | direct | немає | так | — | keep |
| Test packages (`Microsoft.AspNetCore.Mvc.Testing`, `xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`) | див. `.csproj` | direct (лише тестовий проєкт) | немає | не входять у production artifact | major | keep |

Усього пакетів у графі: 30 (API) і 61 (тести) (`dependency_audit_json.txt`); `dotnet list package --outdated` — тільки major-оновлення (`dependency_outdated.txt`), patch-оновлень немає. Жодне `NuGetAuditSuppress`/`WarningsNotAsErrors` не додавалося: придушувати нічого не було; після restore `packages.lock.json` не змінився (`git status` чистий). Оновлення залежностей у ЛР 6 **не виконувалось** (advisory немає) — отже й регресія після оновлення не потрібна.

### 9.3 Систематичний пошук у tracked-файлах (етап 3, п. 3)

Команди: `git status --short`, `git ls-files`, `git grep` (див. розділ 9.1) — по **поточних tracked-файлах**. Межі: історія, stash, інші clones і зовнішній CI **не перевірялися** (окрім `git log --all` для конфігурації); результат пошуку не надсилався сторонніх сервісам.

### 9.4 Журнали й audit (етап 3, п. 4)

Принцип: diagnostic log пояснює технічний стан (exception, failure, traceId); audit event — хто (`subject`), що (`action`), над чим (`resource`), результат (`result`), коли (`occurredAtUtc`), `traceId`. Сценарії (`log_scenarios_requests.txt`, sink `lr06-logs/api.log` поза репозиторієм, `log_sensitive_check.txt`):

| Сценарій | Що observable | Чого немає | Результат перевірки |
|---|---|---|---|
| successful login | `auth.login.succeeded userId=…` | password, cookie, token | 0 збігів |
| failed login | `auth.login.failed userName=…` (очищене значення) | password, тіло, чи існує акаунт | 0 збігів пароля |
| denied protected action | `authorization.denied subject action resource result occurredAtUtc traceId` | чужі дані, token | 0 збігів |
| validation error | коректний 400; застосунок нічого не логує про тіло | full request body | запису з тілом немає |
| unexpected server error | `fail: …ExceptionHandlerMiddleware` + EF-записи з тим самим `TraceId` як у `traceId` відповіді | stack trace у відповіді; secrets у тілі | зв’язок 1:1 (`unexpected_error_log_link.txt`) |
| privileged transition | `incident.status.changed subject action resource result=allowed from to occurredAtUtc traceId` | free-text `reason`, cookie | `SENSITIVE-FREE-TEXT-REASON`: 0 збігів |

Пошук у sink-і шести сценаріїв: `grep -c -F` значення `SeedUsers__Password` = 0; `NotARealPassword1!` = 0; `unreachable-study-value` = 0; `cookie:|set-cookie` = 0; `AQAAAA|X-CSRF-TOKEN|CfDJ8` = 0 (`log_sensitive_check.txt`). Висновок обмежено цими шістьма сценаріями та цим sink-ом; видалення значення з копії для звіту не доводить його відсутності в початковому журналі — перевірялося початковий файл. Log injection: `SafeForLog` (allowlist символів, обмеження довжини) для імені в login; повні user input не конкатенуються — structured logging. Мінімізація даних: система не збирає й не логує зайвих персональних даних (немає email/імені в подіях; subject — Guid).

## 10. Residual risks

| ID | Ризик | Статус | Compensating control | Відповідальний / наступна дія |
|---|---|---|---|---|
| R-01 | Login/register не захищені antiforgery (login-CSRF: жертва входить в акаунт атакувальника; account-creation) | прийнято (рішення за scope) | JSON-only (form-POST → 415), CORS без дозволеного origin, `SameSite=Lax`, login/register limiters, однакові відповіді | власник проєкту; розглянути anonymous bootstrap-токен для login |
| R-02 | In-memory rate limiters не спільні між кількома інстансами API; ключ IP за NAT/проксі спільний | прийнято | один інстанс у навчальному стенді | при масштабуванні — розподілене сховище з TTL (Redis) за погодженням |
| R-03 | Немає HTTPS/HSTS, `AllowedHosts: "*"`: реальний deployment не перевірявся | не перевірено | локальний HTTP, loopback | перед будь-яким deployment — TLS termination, HSTS, список Host |
| R-04 | Request token доступний JS власного origin (XSS) | прийнято | CSP `script-src 'self'`, безпечний DOM (ЛР 5), токен лише в пам’яті | регулярний REG-XSS-01 |
| R-05 | Історія Git, stash, clones, зовнішній CI не перевірялися | не перевірено | відомі навчальні значення | повний аудит історії лише за підозри витоку |
| R-06 | Advanced variant (Redis) не погоджено/не виконано | не входить до обраного рівня | — | узгодити з викладачем (`docs/advanced-variant-proposal.md`) |
| R-07 | Консольний audit sink без захисту від підміни/ретенції | прийнято | audit trail у БД (`IncidentStatusHistory`) | вивести в окремий sink за потреби |

«Нульового» ризику не задекларовано: кожен рядок має умову або компенсувальний control.

## 11. Conclusion

Лише фактичні результати: (1) нові тести були червоними на коді `v0.5.0` (22 із 24) і зелені після виправлень; (2) повний набір `dotnet test SecureLab.sln -c Release`: **127 passed, 0 failed** на tip гілки (`docs/evidence/lab-06/final_regression.txt`) і повторно на merge commit після злиття; (3) 5 знахідок виправлено з retest і positive regression; (4) REG-CSRF-01 автоматизовано повністю; (5) клієнт працює з CSP (0 порушень); (6) у логах шести сценаріїв секретів немає; (7) НЕ виконано: погоджений advanced variant (Redis/рівноцінне), перевірка реального HTTPS/HSTS, аудит усієї історії Git; Windows-команди комплекту `Production.ps1`/`Startup.ps1` не запускалися як є (еквівалентні команди виконано з Git Bash).

## 12. Evidence appendix

| ID | Що доводить | Де лежить / як повторити | Результат |
|---|---|---|---|
| EV-01 | нові тести червоні на `v0.5.0` | `docs/evidence/lab-06/red_v0.5.0.txt`; `git checkout lr06-vulnerable && dotnet test` | 22 Failed з 24 |
| EV-02 | `REG-CSRF-01` | `CsrfRegressionTests`; `csrf_manual_trace.txt` (передумова: reset, alice, свіжий incident) | 400 → стан незмінний → 200 → стан змінений |
| EV-03 | периметр SC-01…SC-07 | `perimeter_grep.txt`, `openapi_mutations.txt`, `CsrfPerimeterTests` | 5+2 |
| EV-04 | безпечна 500 + trace | `unexpected_error_response.txt`, `unexpected_error_log_link.txt`, `ErrorPathTests` | збіг TraceId |
| EV-05 | заголовки по класах | `headers_dev.txt`, `production_normal.txt` | див. 8.3 |
| EV-06 | CORS | `reg_cors_http.txt` | 4173 allowed / 4174 no CORS |
| EV-07 | rate limiting | `login_limiter_manual.txt`, `rl_search_manual.txt`, `SearchRateLimitTests`, `RegisterRateLimitTests` | 200×3 → 429 → recovery |
| EV-08 | залежності й секрети | `dependency_*.txt`, `secrets_config_review.txt`, `git_grep_secrets.txt` | немає advisories; секретів немає |
| EV-09 | журнали | `log_*.txt` | 0 збігів чутливих рядків |
| EV-10 | клієнт під CSP | `client_flow.txt`, `reg_xss_dom_test.txt`, `antiforgery_demo_*.txt` | 0 CSP-порушень; PASS |
| EV-11 | Production vs Development | `production_normal.txt`, `production_reset_refused.txt` | розділ 8.5 |
| EV-12 | audit | `AuditTrailTests`, `red_audit_before_fix.txt` | allowed + denied |

Передумови повторення для іншої людини: Docker Compose (`docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait`), `SeedUsers__Password` у середовищі, `dotnet run --project src/SecureLab.Api -- --reset-database`, `dotnet test SecureLab.sln -c Release`; ручні траси — користувачі alice/bob/morgan, ID створюється тестом.

## Декларація й журнал ШІ

**Використовувала генеративний ШІ** (з обов’язковою авторською адаптацією й інтерпретацією за правилом курсу). Сервіс: Claude (Anthropic, Claude Code, desktop app) — як помічник для написання коду, тестів, скриптів збирання доказів і чернеток документів. Мною (авторкою) перевірено: логіку захисту й межі висновків, результати кожного запуску (тести, HTTP-траси, браузер), відповідність вимогам методички, правильність commit/tag; тексти адаптовано до власного проєкту.

| Дата | Сервіс | Завдання/фрагмент | Авторська адаптація | Перевірка й результат |
|---|---|---|---|---|
| 2026-11-18 | Claude (Claude Code) | antiforgery filter, `csrf.js`, `REG-CSRF-01`, периметр-тест | узгоджено рішення login/register (виняток, R-01), адаптовано наявні тести до токена | `dotnet test` — 127 passed; червоні/зелені стани збережено окремими commit-ами |
| 2026-11-18 | Claude (Claude Code) | F-02 (порядок middleware), F-03 `Cache-Control`, F-04/search rate limit, F-05 audit | вибір політик, квот, partition key (user id / IP) | тести `ErrorPath`, `CacheControl`, `*RateLimit`, `AuditTrail`; ручні HTTP-траси |
| 2026-11-18 | Claude (Claude Code) | скрипти збору доказів, finding cards, security review, звіт | перегляд змісту й меж висновків; не виконане позначено як не виконане | перегляд файлів і журналів; значення секретів не потрапили в докази |
