# Звіт до лабораторної роботи № TODO

## 1. Ідентифікація стану

- Варіант: 2-A «Трекер інцидентів».
- Гілка: `TODO`.
- Фінальний тег: `TODO`.
- Commit hash: `TODO`.

## 2. Змінений маршрут

Опишіть маршрут від дії у браузері до SQL і назад. Наведіть лише ключові
файли та фрагменти, а не повний код.

## 3. Виконані зміни

`TODO`

## 4. Перевірка

| ID | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|
| T-01 | TODO | TODO | TODO | TODO | TODO |

## 5. Security-сценарій

Для робіт із навмисно вразливим станом зафіксуйте: гіпотезу, стан «до»,
мінімальний локальний PoC, спостереження, першопричину, виправлення, повторну
перевірку, позитивну регресію та залишковий ризик.

## 6. Висновок

Сформулюйте висновок із фактичного результату перевірки.

---

## ЛР 03. Автентифікація та безпечна робота з обліковими даними

Автор: Антоненко Катерина Юріївна, група КБ-42, КНУ імені Тараса Шевченка, ФІТ, кафедра кібербезпеки та захисту інформації.
Дисципліна: «Прикладні технології програмування в інформаційній безпеці». Викладач: Бабенко Ю.М.

### Відтворення

| Пункт | Значення |
|---|---|
| Обраний рівень | відмінний (A-01…A-07, S-01, T-01…T-03, поглиблення: threat analysis, втрата чинності сеансу, готовність hasher до оновлення, усунення додаткового дефекту) |
| Baseline / base tag | 2-A «Трекер інцидентів»; v0.2.0 (c585d9f) |
| Scaffold | release `lab-03-start-v1`, base_commit `c585d9f2ffd57c2f29d37de10fc4f69b9cc6a5ba` (файл `.scaffolds/lab-03.json`) |
| Гілка / тег | `lab/3-authentication`; анотований тег `v0.3.0` (див. `git show v0.3.0`) |
| Середовище | Windows 10 Pro 19045, .NET SDK 10.0.302, PostgreSQL у Docker Compose (порт 54329) |
| Команди | `dotnet build SecureLab.sln -c Release`; `dotnet run --project src/SecureLab.Api -- --reset-database`; `dotnet test tests/SecureLab.Api.Tests -c Release` |
| Результат | build без попереджень і помилок; 62 тести з 62 пройдено; два reset поспіль (Reset-Twice.ps1), після кожного 62/62, код завершення 0 |
| Пароль навчальних акаунтів | змінна середовища `SeedUsers__Password`, у Git, звіт і логи не потрапляє |

### Власна реалізація і межі

- **DTO boundary.** `RegisterRequest` (userName, email, displayName, password) із `[JsonUnmappedMemberHandling(Disallow)]`: зайві поля `role`, `id`, `ownerUserId` дають 400 і не створюють акаунт. `RegisterResponse` має лише `id`, `userName`, `displayName`. `MeResponse` — `id`, `userName`, `displayName`, `roles`; email, hash і stamp не повертаються. DTO створення інциденту не містить власника; зайве `ownerUserId` ігнорується (політика за замовчуванням, глобально не змінювалась).
- **Звідки береться id.** `/api/me` і `POST /api/incidents` читають `ClaimTypes.NameIdentifier` із `HttpContext.User`, який заповнює cookie authentication handler. Header `X-Demo-UserId` ігнорується повністю. Захист — `.RequireAuthorization()` на endpoint.
- **Cookie options.** `HttpOnly`, `SameSite=Lax`, `Secure=SameAsRequest` у Development (локальний HTTP) та `Always` у production, `Path=/`, ковзний строк 2 години. Local HTTP-trace не доводить `Secure`: це перевіряє тест через `https://localhost` та окремий тест у справжньому середовищі `Production` (`Secure` додається навіть до http-запиту) — `CookieFlagsTests`.
- **Login.** Пароль перевіряє `SignInManager`/`PasswordHasher`; невідомий користувач і хибний пароль мають однаковий 401 (тип, title, status, detail). Для невідомого імені виконується фіктивна перевірка хешу, щоб вирівняти час відповіді.
- **Duplicate-контракт.** Дублікат імені або email → `409` без уточнення, що саме зайняте; політика пароля, довжина, формат → `400` із ключами полів, без значень із запиту.
- **Rate limit входу (добрий).** `LoginThrottle`: ключ «IP + ім'я з запиту», 5 спроб за 60 с; другий лічильник на IP — 30 спроб за 60 с. Перевищення → `429` із `Retry-After`. Ключ формується з даних запиту, а не з наявності акаунта, тож 429 не розкриває існування користувача. Відновлення після вікна перевірено тестом.
- **Події автентифікації.** `auth.register.succeeded/rejected`, `auth.login.succeeded/failed/throttled`, `auth.logout`. Навмисно виключено: пароль, cookie, hash, stamp, token, `Authorization`, тіло запиту; ім'я користувача при невдалому вході логується лише після allowlist-перевірки (інакше `(invalid-characters)`).
- **CSRF-борг.** Захист спирається на `SameSite=Lax`; окремого anti-forgery токена немає (канонічний flow — ЛР 6). Logout і create — POST, cookie не надсилається у крос-сайтових POST через `Lax`.
- **Endpoint-и, що лишаються каналом перебору.** `POST /api/auth/register` (409 розкриває, що обліковий запис існує — компроміс заради зрозумілого UX, обмежено лише загальним повідомленням); `POST /api/auth/login` (однаковий 401, ліміт 429 за ключем IP+ім'я і за IP).

### Докази (фактично)

| ID | Рівень | Фактично | Безпечний доказ |
|---|---|---|---|
| T-01 | достатній | `201`, `Location: /api/me`, у тілі лише `id`, `userName`, `displayName`; новим акаунтом можна увійти, роль Reporter | `RegisterTests.T01_…`; браузер: рис. register |
| T-02 | достатній | дублікат → `409` (однакова форма для імені й email); слабкий пароль, хибний email, 121 символ `displayName`, зайві `role/id` → `400`, акаунт не створюється, пароль не повертається | `RegisterTests.T02_…`, `ExtraFields_…` |
| T-03 | достатній | `identity_users` має стовпець `PasswordHash` (text), стовпця з відкритим паролем немає; значення hash не показувались | `\d identity_users` без даних |
| A-01 | достатній | анонімний `POST /api/incidents` → `401`, без `Location`, без `WWW-Authenticate`, `application/problem+json` | `AuthSecurityTests.A01_…` |
| A-02 | достатній | login alice → `/api/me` повертає id Alice; рівно поля `displayName`, `id`, `roles`, `userName` | `A02_…` |
| A-03 | добрий | cookie Alice + `X-Demo-UserId: Bob` → відповідь Alice; без cookie з тим header → `401` | `A03_…` |
| A-04 | добрий | create від Bob без owner → owner у БД = Bob | `A04_…` |
| A-05 | достатній | create від Bob з `ownerUserId` Alice → `201`, owner у БД = Bob | `A05_…`; до виправлення (scaffold) owner був alice |
| A-06 | добрий | logout → `204`, основна cookie видаляється; повторний `/api/auth/session-check` та `/api/me` → `401` | `A06_…` |
| A-07 | добрий | unknown і wrong password → однаковий `401` (тип, title, status, detail; traceId не порівнюється), без Set-Cookie | `A07_…` |
| S-01 | достатній | у `/api/me`, register і login-відмові немає password, passwordHash, securityStamp, token, cookie, email; логи без credentials | `S01_…`, `AuthEvents_…` |
| Duplicate | добрий | послідовний 409 | `T02_Duplicate…` |
| Cookie flags | добрий | HTTPS і Production-профіль: HttpOnly, Secure, SameSite=Lax; HTTP у Development: без Secure | `CookieFlagsTests` |
| Rate limit і події | добрий | 5 спроб → 401, 6-та → 429 (і для існуючого, і для вигаданого імені, однакова форма); інше ім'я з тієї ж адреси не заблоковане; відновлення після вікна | `LoginHardeningTests.RateLimit_…`, `AuthEvents_…` |
| Browser flow | добрий | register → login → профіль → create → logout; після logout `/api/me` → 401; `localStorage`/`sessionStorage` порожні, JS не бачить cookie; Console: 0 повідомлень (перехоплення `console.*` і uncaught), пароля в DOM/журналі немає |
| Єдиний user store | чекліст | тест `OnlyApplicationUserIsUserStore_…`: `StudyUser` не в EF, FK на `identity_users`, таблиці `study_users` немає | `Client/auth-lab.*`, знімки у звіті |

#### Поглиблення (відмінний рівень)

1. **Втрата чинності сеансу після серверної події.** Після `ChangePasswordAsync` оновлюється `SecurityStamp`; стара cookie перестає діяти (`SecurityStampChange_InvalidatesOldCookie`: `/api/me` 200 → 401). Інтервал перевірки stamp — 1 хв (за замовчуванням 30 хв), строк cookie — 2 год ковзний.
2. **Готовність hasher до оновлення параметрів.** Hash із 10 000 ітерацій PBKDF2 при поточних налаштуваннях дає `SuccessRehashNeeded`, поточний — `Success`, хибний пароль — `Failed` (`PasswordHasher_ReportsRehashNeeded…`). `PasswordSignInAsync` при `SuccessRehashNeeded` сам оновлює hash.
3. **Додатковий дефект.** Для невідомого імені `SignInManager.PasswordSignInAsync` повертає відмову без хешування. Виміряно (60 запитів кожного виду): на scaffold медіана 7,4 мс для невідомого імені проти 190,3 мс для відомого з хибним паролем; після виправлення (фіктивна перевірка хешу) 171,5 і 171,3 мс.
4. **Threat analysis login flow.** Активи: облікові записи, cookie сеансу, інциденти. Entry points: `/api/auth/register`, `/api/auth/login`, `/api/auth/logout`, захищені `/api/me` і `POST /api/incidents`. Trust boundaries: браузер → сервер (усі дані клієнта недовірені, id береться лише з principal), сервер → БД (параметризовані запити EF, hash у `identity_users`). Реалістична загроза: підбір пароля до відомого імені (credential stuffing). Контрзахід: стандартний hasher із сіллю й вартістю, однаковий 401, rate limit IP+ім'я і окремо IP, події без credentials. Залишковий ризик: розподілена атака з багатьох IP та небезпека штучного блокування по ключу IP+ім'я; немає MFA, lockout акаунта і anti-forgery токена; локальна реалізація не є промисловою.

### Декларація використання ШІ

ШІ (Claude, Anthropic) використано для підготовки коду, тестів і тексту звіту за методичкою; код і результати перевірено власним запуском (build, 62 тести, два reset, ручні HTTP-запити та браузерний сценарій). Автор відповідає за перегляд і розуміння змін та має пояснити їх при захисті.

| Дата | Сервіс/модель | Завдання | Використано | Адаптація | Перевірка |
|---|---|---|---|---|---|
| 2026-11-10 | Claude (Sonnet) | реалізація register/login/me/logout, захисту create, тестів A/T/S, rate limit, звіту | код і текст | узгоджено з baseline 2-A та методичкою | build, 62/62 тестів, два reset, HTTP- та браузерні перевірки |
