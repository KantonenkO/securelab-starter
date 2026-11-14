# Звіт WEB ЛР 5. Безпека браузера: XSS, origin, CORS, CSP

Автор: Антоненко Катерина Юріївна, група КБ-42, КНУ імені Тараса Шевченка, ФІТ, кафедра кібербезпеки та захисту інформації. Дисципліна «Прикладні технології програмування в інформаційній безпеці», викладач Бабенко Ю.М. Спільний сценарій baseline 2-A. Рівень: **відмінний («Відмінно»)** (рівні накопичувальні). ОС: Windows 10 Pro 19045; браузер: Microsoft Edge 157.0 (Chromium).

## Для всіх рівнів

**DEL-01.** Карта: `docs/source-to-sink.md`; докази: `docs/browser-security-evidence.md` (+ `docs/evidence/lab-05/*.txt`).
До → причина → fix → retest → залишковий ризик: сторінка `/browser-lab.html` записувала збережений текст `Incident.Description` і `IncidentComment.Text` у `innerHTML`; harmless marker, збережений Alice, виконався в сеансі Morgan (stored XSS із клієнтським HTML-sink); першопричина — недовірене значення в HTML-контексті, а не слово `script`. Виправлення — safe DOM API (`textContent`, `createElement`, `replaceChildren`) без зміни збережених даних; той самий marker і той самий запис після fix — текст, дія не відбувається. Залишковий ризик: нові поля/sinks, URL/атрибути, сторонні scripts, неохоплені сторінки.

**DEL-02 (Git).** `release` = `lab-05-start-v1`, `base_commit` = `b1be2e3eefd2283f6e948aa9fedc20cc0ed26599`. Scaffold-commit `b5638e5`; **vulnerable `74d588e`**; проміжний fix sink `d42eb38`; **fixed commit і `v0.5.0`** — hash записано в розділі «Git-стан» нижче (вписується наступним docs-commit після тегу). Гілка `lab/5-browser-xss`; `main` fast-forward до fixed; `lab05.bundle` (перевірений `git bundle verify`); приватного remote немає.

**DEL-03.** Відповіді нижче; live-modification: можу показати source/sink основного Description, safe DOM, межу backend validation, коментар через DOM API, preflight, додати другий exact development origin.

## Додатково для Добре та Відмінно

Посилання: IncidentComment.Text і helper (`browser-lab.js`: `renderText`, `renderTextParagraphs`), preflight і cookie-дослід (`docs/browser-security-evidence.md`, розділ «Додатково для Добре та Відмінно»), CSP enforcement і headers (nosniff, Referrer-Policy, frame-ancestors).

**Review: XSS, CORS, CSRF, authorization — на власному flow.**

| Загроза/механізм | Що це на нашому flow | Що вирішує | Чого НЕ вирішує |
|---|---|---|---|
| **XSS** | marker у `description` виконався в сеансі Morgan, бо дані потрапили в `innerHTML` | safe DOM rendering у sink (виправлено); CSP — лише другий шар | не виправляється CORS, авторизацією чи HttpOnly (HttpOnly лише ховає cookie від JavaScript, але injected code усе одно виконується) |
| **CORS** | чи надасть браузер JavaScript сторінки з origin 4173/4174 відповідь API 5080 | allowlist exact origin `http://localhost:4173` (Development) | не захищає API від curl/Postman, не автентифікує, не авторизує, не замінює CSRF; запит із 4174 усе одно доходить до API (200), браузер лише не віддає response |
| **CSRF** | cookie сеансу браузер надсилає автоматично, тому state-changing POST з чужої сторінки міг би виконатись від імені Alice | окремий antiforgery/CSRF control — **ЛР 6** (SameSite=Lax лише частково) | CORS не зупиняє «простий» cross-origin POST; тому не замінює CSRF-захист |
| **Authorization (ЛР 4)** | чи має Morgan/Alice право прочитати конкретний інцидент (404/403/401) | серверне рішення handler-ів і query scoping | не робить безпечним те, як браузер інтерпретує відповідь: Morgan мав право читати інцидент, і саме тому stored XSS став вектором |

Пояснення policy: CSP зроблено у спільній middleware, без `unsafe-inline` і зовнішніх джерел; `frame-ancestors 'none'` (embedding не потрібне); `nosniff` і `no-referrer` — baseline, перевірені на відповідях. Нічого з цього не замінює safe sink.

## Додатково для Відмінно

DOM-тест `tests/browser/lab05-dom.js`: FAIL на vulnerable `74d588e` («текст у DOM відрізняється від збереженого»), PASS на fixed; перевіряє Description(marker), 5 позитивних рядків, кожен `comments[].text`, повторний render без дублювання. CSP: спершу report-only (порушення лише логуються), потім enforcement (блокує); показано, що CSP маскує невиправлений sink (vulnerable JS під enforcement: marker `null`, але `<img>` створено, DOM-тест FAIL). `safe-url.js` перевірено в Console.

**Різні правила безпечного виведення для чотирьох контекстів.**

- **Текстовий вузол** (вміст елемента): достатньо записувати дані як `textContent`/`createTextNode` — браузер не розбирає їх як HTML. Перелік заборонених символів не потрібен, `<` і `>` — звичайні дані.
- **Значення атрибута:** `setAttribute`/властивість елемента, значення не вставляють у HTML-рядок; неприпустимі атрибути-обробники (`onclick`, `onerror`) з даних не створюють; для `class`, `id`, `data-*` — лише відомі значення з allowlist.
- **URL (`href`, `src`):** `textContent` тут не допоможе: небезпечна **схема** (`javascript:`). Розбір робить сам браузер (`new URL`, не regular expression), дозволені лише `http:`/`https:`, інакше посилання не створюють (`null`); для продукту потрібен ще allowlist hosts. Перевірено: `javascript:alert(1)` і `" JaVaScRiPt:alert(1)"` дають `null`.
- **JavaScript:** рядки з даних ніколи не передають у `eval`, `new Function`, `setTimeout(string)` чи inline-обробники; код і дані розділяють, події реєструють через `addEventListener`.

**Залишковий ризик third-party scripts.** У дослідженому flow (`/`, `/browser-lab.html`, `/auth-lab.html`) сторонніх scripts немає, усі — власні (`script-src 'self'`), тож цей ризик на цьому flow не виникає. Але: Development-довідник `/scalar/v1` постачає сторонній UI package з власними inline/зовнішніми ресурсами й винесений з CSP (виняток лише в Development); будь-який майбутній сторонній script виконується з правами сторінки й може читати DOM/дані й надсилати їх у мережу, тож потребує окремого перегляду (SRI, фіксація версії, allowlist у `script-src`) — це тема ЛР 6.

## Відповіді на контрольні питання

1. **Чому value з БД може залишатися недовіреним?** Збереження не змінює походження: це раніше введений користувачем текст. База лише зберігає його, а небезпека виникає на межі, де браузер його інтерпретує. Тому 200 від сервера й validation не роблять значення довіреним.
2. **Source, transformation, sink у вашому flow.** Source — textarea `description`; transformations — `fetch` → DTO → validation/`Trim` → БД → `IncidentDetailsResponse` → JSON → `load()`; sink — `#description.innerHTML` (після fix `textContent`).
3. **Stored vs reflected vs DOM-based XSS.** Stored — значення збережено й виконується під час пізнішого перегляду (наш випадок); reflected — повертається з поточного запиту (в роботі лише пояснено); DOM-based — небезпечне перетворення відбувається в клієнтському коді (джерело, напр., `location.hash`). Ознаки не взаємовиключні: наш потік — stored із клієнтським sink.
4. **Чому блокування `<script>` не усуває XSS?** Виконання дає й `<img onerror>`, `<svg onload>`, атрибути й URL; blacklist залежить від одного запису payload, а HTML-парсер усе одно отримує недовірений HTML.
5. **Коли textContent, а коли sanitizer?** Для звичайного тексту (наш випадок) — `textContent`. Sanitizer потрібен лише за продуктової вимоги приймати обмежений HTML, і тоді це підтримувана бібліотека (DOMPurify) з allowlist, а не regex.
6. **Чому sanitizer не пишуть regular expression?** HTML — не регулярна мова; парсери браузерів прощають і нормалізують вхід, тож regex пропускає мутації; потрібен парсер того ж типу.
7. **Три компоненти origin.** Схема, host і port: `http://localhost:5080`, `…:4173`, `…:4174` — три різні origins (той самий site).
8. **Що браузер надсилає в preflight і хто його ініціює?** Браузер (не код сторінки) надсилає `OPTIONS` з `Origin`, `Access-Control-Request-Method` і `Access-Control-Request-Headers` (у досліді `x-lab-cors-probe`); actual-запит іде лише після відповіді з відповідними `Access-Control-Allow-*`.
9. **Хто оголошує і хто застосовує CORS?** Оголошує API (відповідні headers: `ACAO`, `ACAM`, `ACAH`, `ACAC`); застосовує браузер: він вирішує, чи віддати response JavaScript. Сервер сам нічого не блокує (запит з 4174 дійшов до API).
10. **Чому curl читає response, а script отримує CORS error?** Same-origin policy/CORS — правило браузера для web content; curl не веб-сторінка. Direct client усе одно проходить authentication/authorization (anonymous → 401).
11. **Чому CORS не authorization і не замінює CSRF?** CORS керує читанням cross-origin response браузером, а не правом на дію; cookie й «простий» POST можуть бути надіслані без preflight; CSRF потребує окремого antiforgery (ЛР 6).
12. **Чому wildcard origin не поєднують із credentials?** Відповідь із credentials має називати конкретний origin; `*` разом із `AllowCredentials` дозволяв би будь-якому сайту читати відповіді від імені користувача. ASP.NET Core відхиляє таку комбінацію ще під час запуску; відбиття довільного `Origin` рівнозначне «дозволено всім».
13. **Межа CSP.** CSP — defense in depth: обмежує джерела й дії (inline-обробник marker під enforcement блокується), але не виправляє sink: у `<img>` залишається небезпечний вміст, а DOM-тест FAIL (`csp_mask.txt`). Safe DOM rendering обов'язковий.
14. **Який risk зменшують nosniff, Referrer-Policy, frame-ancestors?** nosniff — MIME sniffing; Referrer-Policy — витік адреси сторінки через `Referer`; frame-ancestors — embedding і clickjacking.

## Декларація використання ШІ

Використано генеративний ШІ (Claude, Anthropic) для виконання роботи за методичкою: складання карт і документів, коду safe-helper-а, CORS/CSP-змін, тестів, сценаріїв DevTools-автоматизації та знімків. Мною перевірено й можу пояснити: що саме змінено в `browser-lab.js` і чому `innerHTML` → `textContent`; чому CORS не зупиняє curl; чому CSP не замінює fix sink. Фактично виконані перевірки: `dotnet build`, `dotnet test` (99 з 99), два reset, DOM-тест на vulnerable (FAIL) і fixed (PASS), CORS-дослід із двох origin, CSP report-only → enforcement, REG-XSS-01 і REG-CORS-01, `git bundle verify`.

## Межі перевірки

Перевірено фактично на локальному стенді: Edge 157 (Chromium) під керуванням DevTools-протоколу (вікна A/B/Bob — окремі процеси з окремим сховищем cookie); Firefox/Safari не перевірялись. Знімки екрана — сторінки застосунку; Network/Console подано текстом із подій DevTools, а не знімками панелей. Поза стендом: production-архітектура CORS, сторонні scripts, повний CSRF-захист (ЛР 6), приватний remote.

## Git-стан

Fixed commit: `8d1ad57092294a63710885a0e7fcdd14a04652bb` (= тег `v0.5.0`, анотований).
Vulnerable commit: `74d588e8b0f9bbbec82afe6667955f8c5bc9af34`; scaffold: `b5638e582e4a24dec69d92a500ef9f842d3dc4b7`; проміжні: `d42eb38` (safe DOM sink), `2cb0a59` (CORS/CSP/тести).
Після тегу — лише цей docs-commit (хеш у звіті); код не змінювався. Перевірено: build без помилок і попереджень; 99 тестів із 99; два reset поспіль (по 99/99, код 0).
