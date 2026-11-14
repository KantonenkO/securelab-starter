# Докази безпеки браузера (DEL-01, ЛР 5)

Рівень: **відмінний («Відмінно»)**. Автор: Антоненко Катерина Юріївна, КБ-42. Усі докази зібрано на локальному навчальному стенді зі штучними обліковими записами; паролі, cookie/token values і export browser storage сюди не потрапляють. Вихідні тексти спостережень — `docs/evidence/lab-05/*.txt` (без секретів).

## Стенд і Git-стан (Для всіх)

- ОС: Windows 10 Pro 19045; браузер: Microsoft Edge 157.0 (Chromium; керувався через DevTools-протокол, знімки — зі справжньої сторінки); .NET SDK 10.0.302; PostgreSQL у Docker Compose.
- API/клієнт: `http://localhost:5080`; probes: `http://localhost:4173` (дозволений) і `http://localhost:4174` (контроль).
- Scaffold: release `lab-05-start-v1`; `base_commit` = `entry_parent` = `b1be2e3eefd2283f6e948aa9fedc20cc0ed26599` (v0.4.0).
- Hashes: scaffold-commit `b5638e5`; **vulnerable commit `74d588e`** (scaffold + seed marker-коментар); **fixed commit `8d1ad57092294a63710885a0e7fcdd14a04652bb` = тег `v0.5.0`**. Окремі проміжні commit: `d42eb38` (safe DOM sink).
- Передавання: перевірений стан → локальна `main` (fast-forward) і `lab05.bundle`; приватного remote для ЛР 5 немає.
- Механізм сеансу (Application → Storage → Cookies → `http://localhost:5080`, без Value): `.AspNetCore.Identity.Application`; Domain `localhost` (host-only); Path `/`; HttpOnly `true`; SameSite `Lax`; Secure `false` (локальний HTTP, у production — Always); session cookie; `document.cookie` для JavaScript порожній; localStorage і sessionStorage — 0 записів (`out/cookie_meta.txt`).

## Гіпотеза й REG-XSS-01 (Для всіх)

**Карта Description:** `docs/source-to-sink.md` (flow 1). **Гіпотеза:** значення з поля `description` форми зберігається в `incidents.description`, повертається через `IncidentDetailsResponse.Description` і передається в `#description.innerHTML` як HTML-контекст; тому за умови, що текст містить тег із inline-обробником, браузер інтерпретує дані не як звичайний текст.

**Передумови, локальний користувач, marker, команди:** commit; reset — `dotnet run --project src/SecureLab.Api -- --reset-database`; запуск — `dotnet run --project src/SecureLab.Api --launch-profile http`; браузер `http://localhost:5080`; акаунти alice (автор) і morgan (Analyst, глядач), bob (контроль); пароль — змінна середовища `SeedUsers__Password`. Marker — єдиний рядок `resources/lab05-marker.txt`:

`<img src=x onerror="document.querySelector('#lab05-marker-target').setAttribute('data-lab05-marker','executed')">`

| Спостереження | До fix (74d588e) | Після fix | Посилання на доказ |
|---|---|---|---|
| Значення в БД/API | `GET /api/incidents/{id}` повертає `description` рівно marker (113 символів); `POST` payload збігається | те саме значення; старий запис (створений на vulnerable-стані) після fix: значення збігається побайтово | `alice_before.txt`, `alice_after.txt`, `api_description_*.txt`; тест `Description_WithHtmlLikeMarker_IsCreatedAndReturnedByteForByte` |
| DOM основного Description | в `#description` елемент `<img src="x" onerror="…">`; текстового вузла немає | один текстовий вузол, `childElementCount = 0`, `textContent` = marker | `a_before_2_description_marker.png`, `a_after_2_description_marker.png` |
| Дія marker | `#lab05-marker-target[data-lab05-marker]` = `executed`; у Network запит `GET /x` | `null`; запиту `/x` немає | `alice_before.txt`, `alice_after.txt` |
| Апостроф/лапки, `5 < 7`, Unicode, HTML-like рядок | рядок `<b>жирний</b> текст` показано як жирний (елемент `<b>`, DOM text ≠ вхід); решта збігаються | усі 5 рядків: `DOM.textContent` = вхід, елементів 0 | `alice_before.txt` (рядки `[html-like-b]`), `alice_after.txt`, `a_*_3_html_like_b.png` |
| Довжина за правилами ЛР 2 (4000) | текст 3900 символів відображається, API == вхід | так само, без помилок | `[long-3900]` |
| Вікно Morgan: GET і перевірка marker; контроль Bob | Morgan: GET 200, marker `executed`; Bob: GET 404, marker `null` | Morgan: GET 200, marker `null`, текст видно; Bob: GET 404, `null` | `viewer_morgan_*.txt`, `viewer_bob_*.txt`, `b_*_morgan.png` |

**Finding.** Актив: browser session і дані, доступні від імені користувача (сеанс Analyst Morgan). Передумови: локальний тестовий account Alice контролює текстове поле. Першопричина: недовірене збережене значення `Incident.Description` (і `IncidentComment.Text`) потрапляє в HTML-sink `innerHTML` клієнта `browser-lab.js`. Вплив на стенді: marker виконав лише безпечну локальну дію (атрибут на `#lab05-marker-target`) в сеансі Morgan — stored XSS. Виправлення (safe DOM diff): `innerHTML` → `textContent` через єдиний helper `renderText`/`renderTextParagraphs` (`createElement` + `textContent`, `replaceChildren`); дані не змінювались, `<`/`>` на вході не відхиляються. Retest: той самий marker і той самий запис більше не інтерпретуються як код; DOM-тест PASS. Залишковий ризик: інші sinks/поля, які з'являться пізніше (`Title`, нові сторінки), сторонні scripts, контексти URL/атрибута, неохоплені сторінки; CSP — лише defense in depth.

**REG-XSS-01** (повторюваний ручний/автоматизований): передумови — commit hash, reset, `alice`/`morgan`, origin 5080. Вхід — marker `resources/lab05-marker.txt`. Кроки: (1) reset і запуск API; (2) alice → `/auth-lab.html` → `/browser-lab.html`; перевірка `document.querySelector("#lab05-marker-target").getAttribute("data-lab05-marker")` → `null`; (3) у «Опис нового інциденту» вставити marker → «Створити й прочитати»; (4) у вікні morgan (окремий сеанс) відкрити той самий Guid → «Прочитати»; (5) повторити перевірку; (6) автоматично — вставити `tests/browser/lab05-dom.js` у Console. Очікування: marker `null`; DOM — текстовий вузол; DOM-тест PASS; нормальний текст відображається. Негативний контроль: на vulnerable commit marker = `executed`, DOM-тест FAIL.

## CORS і REG-CORS-01 (Для всіх)

Прогноз (Етап 3, крок 1; заповнено до зміни policy):

| Сценарій | Чи доходить request до API? | Чи може JavaScript прочитати response? | Чому |
|---|---|---|---|
| same-origin 5080 → API | так | так | same-origin; CORS не потрібен |
| simple GET 4173 → API | так | ні без CORS-дозволу | same-origin policy браузера + CORS |
| simple GET 4174 → API | так | ні | origin не в allowlist |
| curl/.http → API | так | не застосовується | не browser JavaScript |
| preflighted 4173 → API | спершу OPTIONS, actual GET залежить від відповіді | ні без успішного preflight | preflight і CORS |

Команди запуску probes (з кореня проєкту): `py -3 -m http.server 4173 --bind localhost --directory demonstrations/cors-probe` і `py -3 -m http.server 4174 --bind localhost --directory demonstrations/cors-probe`. Policy `Lab05BrowserProbe` — **лише Development**, один exact origin `http://localhost:4173`, методи `GET`, header `X-Lab-Cors-Probe`, `AllowCredentials()` лише для цього origin; `4174` — негативний контроль; штатна same-origin топологія — `http://localhost:5080`. У Production policy не реєструється й не застосовується (тест `Cors_Policy_IsNotRegisteredOutsideDevelopment`).

| Клієнт / простий запит | До policy | Fixed state | Status, ACAO, Console / доказ |
|---|---|---|---|
| Same-origin 5080 | 200 | 200 | CORS не потрібен |
| Allowed 4173 | `TypeError: Failed to fetch`; GET 200 дійшов до API, але без ACAO (`MissingAllowOriginHeader`) | `{status:200, body:{"status":"ready"}}`; `ACAO: http://localhost:4173` | `cors_before.txt`, `cors_policy.txt` |
| Disallowed 4174 | `Failed to fetch`; Console: «No 'Access-Control-Allow-Origin' header…» | так само: GET 200 дійшов до API, response браузер не віддав script; ACAO немає | `cors_policy.txt` |
| Direct HTTP / curl | 200 | 200; для Origin 4173 є ACAO, для 4174 — жодного `Access-Control-*` | `reg_cors_http.txt` |

**REG-CORS-01.** Browser evidence: probe 4173/4174 → абсолютний URL API з очікуваним `Origin` у Network (`cors_policy.txt`). HTTP evidence (Development fixed state): `curl.exe -sS -D - -o NUL -H "Origin: http://localhost:4173" "http://localhost:5080/health"` → `HTTP/1.1 200`, `Access-Control-Allow-Origin: http://localhost:4173`, `Access-Control-Allow-Credentials: true`, nosniff, no-referrer; для `Origin: http://localhost:4174` — `200` і жодного `Access-Control-*` (`out/reg_cors_http.txt`; автоматизовано в `BrowserSecurityTests`). Перевірка порядку: з 4173 `fetch(".../api/incidents")` без cookie → число **401**, у Network відповідь містить `ACAO` (UseCors до UseAuthorization). Пояснення: curl читає відповідь, бо same-origin policy — правило браузера для web content; direct client усе одно проходить authentication/authorization API; CORS не зупиняє request.

## Додатково для Добре та Відмінно

**Другий flow IncidentComment.Text.** Seed-коментар `30000000-0000-0000-0000-000000000005` (Morgan → інцидент Alice, публічний). До fix: у `#comments` `<p><img …></p>`, marker `executed` уже для seed-інциденту `…0001`; після fix — текст, елементів у `p` 0, marker `null` (`alice_before.txt` / `alice_after.txt`, `a_*_1_seed_comments.png`). **Єдиний safe helper:** `renderText` (елемент ← `textContent`) і `renderTextParagraphs` (fragment + `replaceChildren`) у `browser-lab.js`; викликаються для Description, Title і comments; джерело принципу — `createTextElement` в `app.js`.

**Preflight** (Network, `cors_policy.txt`): з 4173 — `OPTIONS 204` (`Origin: http://localhost:4173`, `Access-Control-Request-Headers: x-lab-cors-probe`; відповідь: `ACAO: http://localhost:4173`, `ACAM: GET`, `ACAH: X-Lab-Cors-Probe`), потім actual `GET 200`; з 4174 — `OPTIONS 204` без `Access-Control-*`, actual GET не надіслано (`PreflightMissingAllowOriginHeader`). Preflight ініціює браузер, не JavaScript-код; це не login і не authorization.

**Cookie-дослід** (`cors_creds.txt`, `cors_anon.txt`, `cors_nocreds.txt`): Alice ввійшла (`Login: 204`); з 4173 кнопка «GET» (`credentials: "include"`) → `200` і список інцидентів, у Network `[Cookie надіслано]`, `ACAO: http://localhost:4173`, `ACAC: true`; з 4174 → «Браузер не надав response JavaScript», запит із cookie **виконано** (GET 200), response не показано; негативний контроль без `AllowCredentials()` → `The value of the 'Access-Control-Allow-Credentials' header in the response is '' which must be 'true'…` (`InvalidAllowCredentials`); без входу з 4173 → `401` із CORS-заголовками. Кінцевий стан: `AllowCredentials()` — лише в Development policy для 4173. Cookie надсилається з 4173 на 5080, бо порт не входить до site (той самий site, різні origins), тож це не доказ cross-site поведінки SameSite. Review XSS/CORS/CSRF/authorization — `docs/report-lab-05.md`.

CSP — таблиця ресурсів за фактичним Network (три сторінки flow: `/`, `/browser-lab.html`, `/auth-lab.html`):

| Директива CSP | Фактичні ресурси застосунку | Обране джерело / причина |
|---|---|---|
| default-src | fallback для всіх типів | `'self'` |
| script-src | `/app.js`, `/browser-lab.js`, `/auth-lab.js` (власні файли; inline scripts і handlers відсутні) | `'self'` (без `unsafe-inline`) |
| style-src | `/styles.css`, `/auth-lab.css` (inline `<style>` винесено у файл) | `'self'` |
| connect-src | `fetch` до `/api/*` на тому самому origin; navigation | `'self'`; навчальні origins 4173/4174 не додано — вони лише CORS-стенд |
| img-src | зображень немає | `'self'` |
| frame-ancestors | embedding не потрібне | `'none'` (немає fallback до `default-src`, вказано явно) |
| додатково | `object-src 'none'`, `base-uri 'self'`, `form-action 'self'` | закривають plugin-вміст, зміну base URL і відправлення форм назовні |

Місце зміни: наявна middleware response headers у `Program.cs` після `UseStatusCodePages`, до `UseDefaultFiles/UseStaticFiles`; виняток для `/scalar` (Development-довідник на стороннім UI package) — лише в Development. Enforcement: документ `/` → 200, один header `Content-Security-Policy` (`out/csp_headers_enforce.txt`, `out/csp_enforce.txt`); три сторінки flow без повідомлень CSP; кероване порушення `new Image().src = "http://localhost:4173/x.png"` → «Loading the image … violates … "img-src 'self'". The action has been blocked.», у Network `blockedReason=csp`. Для «Добре» докази report-only не обов'язкові (є для «Відмінно» нижче).

| Header / control | Фактичне значення | Ризик і межа захисту / policy rationale |
|---|---|---|
| X-Content-Type-Options | `nosniff` (baseline; відповіді `/`, `/browser-lab.html`, `/health`, OPTIONS) | зменшує MIME sniffing (браузер не «вгадує» тип); не замінює правильний `Content-Type` і validation |
| Referrer-Policy | `no-referrer` (baseline) | не передає referrer на інші ресурси; клієнту не потрібні referrer-залежні переходи, посилання на зовнішні сайти немає; не замінює заборону секретів у URL |
| frame-ancestors | `'none'` (у CSP) | запобігає embedding/clickjacking-контексту; не замінює authorization чи XSS-fix; X-Frame-Options не додано, щоб не було суперечливих значень |

## Додатково для Відмінно

**DOM-тест:** файл `tests/browser/lab05-dom.js` (запуск у Console `/browser-lab.html` після входу Alice; у моєму прогоні — через DevTools-протокол `Runtime.evaluate` у Edge 157). На vulnerable commit `74d588e`: **FAIL** — «Description: текст у DOM відрізняється від збереженого» (`out/domtest_vulnerable.txt`); на fixed: **PASS** — Description(marker), 5 позитивних рядків, comments (кожен `comments[].text` у `p`, елементів 0, повторний render ×3 без дублювання, marker не виконано) (`out/domtest_fixed.txt`). Тест падає на небезпечному sink незалежно від CSP: під enforcement vulnerable-JS marker не виконується, але DOM-тест усе одно FAIL (`out/csp_mask.txt`: marker `null`, `<img>` створено) — CSP маскує, а не виправляє sink.

**CSP report-only → enforcement** (`out/csp_headers_report.txt`, `csp_report.txt`, `csp_headers_enforce.txt`, `csp_enforce.txt`): спершу `Content-Security-Policy-Report-Only` (три сторінки без порушень; `new Image()` → «violates … img-src 'self'. The policy is report-only, so the violation has been logged but no further action has been taken.»), потім enforcement (замість, а не поруч: один header; «The action has been blocked.»; Network `blockedReason=csp`). Legitimate flow під enforcement: login Alice, список, деталі, create+read, DOM-тест PASS.

**URL/атрибут (`safe-url.js`, `out/safe_url.txt`):** `https://example.com/a` і `/incidents/1` → абсолютні URL; `javascript:alert(1)`, `" JaVaScRiPt:alert(1)"` і `http://[bad` → `null`; `href` створеного посилання `http://localhost:5080/incidents/1`, `onclick` → `null`. Різні правила виведення (text node / attribute / URL / JavaScript) і ризик third-party scripts — `docs/report-lab-05.md`.

Додаткових DEL, навчальних демо або нового журналу не подано. Невиконаним лишається лише приватний remote (передавання через `main` і `lab05.bundle`).
