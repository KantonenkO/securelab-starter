# F-02. Заголовки безпеки зникають на відповіді 500 (error-path gap)

**Статус:** Fixed  
**Актив:** A6 (внутрішні деталі) / browser defense in depth (CSP, nosniff, Referrer-Policy)  
**Компонент:** `Program.cs` (порядок middleware: security headers ↔ `UseExceptionHandler`)  
**Пріоритет:** P2

## Передумови і межі огляду

Production-like профіль (`--no-launch-profile`, `ASPNETCORE_ENVIRONMENT=Production`), локальна БД, навмисно недосяжний порт `54399` (локальний, чужі host-и не зачіпаються). Commit тестів: `12cd7af`. Клієнт: HttpClient у тестовому host-і та `curl`; публічний `POST /api/auth/login` з вигаданим паролем.

## Відтворення

1. Запустити застосунок у Production з connection string на порт 54399.
2. `POST /api/auth/login` з тілом `{"userName":"alice","password":"<вигаданий>"}`.
3. Очікувано: `500 application/problem+json` без внутрішніх деталей **і** ті самі заголовки, що на 200 (`X-Content-Type-Options`, `Referrer-Policy`, `Content-Security-Policy`). Фактично (v0.5.0): тіло безпечне, але **усі три заголовки відсутні**.

## Вплив і ймовірність

**Статус перевірки:** спостережено локально (тест `ERR_01_HDR_01…` червоний на `12cd7af`).

Відповідь про помилку лишається без CSP/nosniff/Referrer-Policy: послаблюється defense in depth саме для відповідей, які формує error handler. Impact: Low (немає прямого витоку), likelihood: Medium (500 досяжна анонімно при збої БД). Прямого впливу на дані немає, тому P2.

## Першопричина

Middleware заголовків виставляв їх **до** `next()` і стояв **після** `UseExceptionHandler`. Під час необробленого винятку `ExceptionHandlerMiddleware` очищає вже встановлені заголовки перед записом помилки, тож для 500 вони втрачалися; на 200 і 4xx без винятку все було нормально, через що збій був непомітним.

## Виправлення

Fixed commit `49a5fad`: заголовки встановлюються в `Response.OnStarting` (спрацьовує вже після очищення handler-ом), а middleware стоїть **перед** `UseExceptionHandler` — другий паралельний catch-all не додавався, порядок конвеєра змінено мінімально.

## Retest

`ERR_01_HDR_01_UnexpectedError_IsSafe_HasSecurityHeaders_AndTraceMatchesLog` (Production-host із недосяжною БД): 500, `application/problem+json`, у тілі немає `Npgsql`/`54399`/`unreachable`/`Exception`/`Host=`/`Password`/stack trace; три заголовки присутні й збігаються зі значеннями на 200; середній сегмент `traceId` дорівнює `TraceId` запису `ExceptionHandlerMiddleware` у журналі. Ручний прогін: `docs/evidence/lab-06/unexpected_error_response.txt`, `unexpected_error_log_link.txt`.

## Positive regression

Заголовки на 200/400/404 не змінилися (`docs/evidence/lab-06/headers_dev.txt`, `production_normal.txt`); CSP не ламає клієнта (`client_flow.txt`: 0 порушень); `/scalar` у Development лишається єдиним документованим виключенням (`BrowserSecurityTests`).

## Залишковий ризик

Заголовки на відповідях, що формує reverse proxy/Kestrel до застосунку (напр. 400 від самого Kestrel на некоректний запит), не перевірялися; HSTS/HTTPS — поза межами (немає реального TLS-host-а).

## Доказ

Тест `ErrorPathTests`; evidence: `red_v0.5.0.txt` (до), `unexpected_error_response.txt`, `unexpected_error_log_link.txt` (після).
