# Власна карта source-to-sink (ЛР 5)

Автор: Антоненко Катерина Юріївна, КБ-42. Рівень: відмінний. Стан вимірювань: vulnerable commit `74d588e` (scaffold `b5638e5` + seed `74d588e`), fixed — див. `docs/browser-security-evidence.md`.

## Flow 1. Incident.Description (обов'язковий для всіх рівнів)

Вхід/Guid запису: harmless marker (`resources/lab05-marker.txt`) створений Alice через форму `/browser-lab.html`; приклад Guid у вікні A — `3b588aae-3ce6-4aec-a734-81e3c5ce4d19` (reset його видаляє; нова процедура створює новий Guid).

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `Client/browser-lab.html`, форма `#edit`, `<textarea name="description">` | значення, яке вводить користувач (marker із `lab05-marker.txt`) | контрольоване користувачем; поле форми |
| Request | `Client/browser-lab.js`, обробник `submit` → `fetch("/api/incidents", {method:"POST"})` | JSON `{"title":…,"description":<marker>,"severity":"Low","occurredAtUtc":…}` | Network: `POST /api/incidents` 201; у payload `description` дорівнює marker; контрольоване користувачем |
| Validation/mapping | `Scaffolding/Lab02Endpoints.cs`, `POST /api/incidents` (`CreateIncidentRequest`, правила ЛР 2: required, довжина ≤ 4000, cross-field для High/Critical) | `Description` обрізається `Trim()`, owner береться з principal (ЛР 3/4) | validation не робить значення довіреним: символи `< >` не заборонені, бо Description — звичайний текст |
| Storage | `Data/Entities/Incident.cs`, `Incident.Description` → колонка `description` PostgreSQL (`incidents`) | те саме Unicode-значення | збережене значення користувача; тест `Description_WithHtmlLikeMarker_IsCreatedAndReturnedByteForByte` |
| Response | `Presentation/Contracts/IncidentResponses.cs`, `IncidentDetailsResponse.Description`; `Application/Incidents/IncidentQueries.cs`, `GetDetailsAsync` | JSON `description` у `GET /api/incidents/{id}` (лапки екрановано як `\"` — це запис JSON) | Network: GET 200; недовірене для відображення браузером; авторизація (ЛР 4) перевіряє право, не DOM |
| Client transform | `Client/browser-lab.js`, `load()` | `current.description` (до fix: без перетворення) | залежить від реалізації |
| Sink/context | `Client/browser-lab.js`, `load()`: **було** `document.querySelector("#description").innerHTML = current.description` | HTML-контекст: браузерний HTML-парсер розбирає значення | **небезпечний** до fix (stored XSS); **після fix** `renderText(element, value)` → `element.textContent = value`: контекст текстового вузла |

Гіпотеза (CP-01): значення з поля `description` форми зберігається в `incidents.description`, повертається через `IncidentDetailsResponse.Description` і передається у sink `#description.innerHTML` як HTML-контекст; тому за умови, що текст містить тег, браузер інтерпретує дані не як звичайний текст, а як елемент `<img>` із inline-обробником.

Класифікація: **stored XSS** (значення збережене в БД і виконується під час пізнішого перегляду іншим користувачем) із **DOM-based** місцем перетворення (client-side `innerHTML`). Reflected XSS у scope не входить; пояснено в `docs/report-lab-05.md`.

## Flow 2. IncidentComment.Text (добрий і відмінний рівні)

Джерело: seed-коментар `30000000-0000-0000-0000-000000000005` (автор Morgan, до інциденту Alice `…0001`, `IsInternal = false`), доданий у `Data/DbSeeder.cs`; у продукті source — будь-який автор коментаря. Endpoint створення коментаря в API немає.

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `Data/DbSeeder.cs`, `SeedAsync`, константа `Lab05Marker` | marker як звичайний текст коментаря | seed (у продукті — контрольоване користувачем) |
| Request | `GET /api/incidents/20000000-0000-0000-0000-000000000001` (body відсутнє) | Guid лише обирає запис | Network GET 200 |
| Validation/mapping | `Application/Incidents/IncidentQueries.cs`, `GetDetailsAsync` → `IncidentCommentResponse(Id, AuthorDisplayName, Text, CreatedAtUtc)`; фільтр `includeInternalComments` (ЛР 4) | `Text` без змін | seed не проходить HTML-очищення; internal-коментарі Reporter не бачить |
| Storage | `IncidentComment.Text` → `incident_comments.text` | те саме значення | збережене значення користувача |
| Response | `IncidentDetailsResponse.Comments[].Text` | JSON `comments[].text` | недовірене для браузера |
| Client transform | `browser-lab.js`, `load()`: `current.comments ?? []` | масив коментарів | — |
| Sink/context | **було** `paragraph.innerHTML = comment.text` у циклі; **після fix** `renderTextParagraphs(container, texts)` → `renderText(p, text)` → `textContent` | HTML-контекст → текстовий вузол | **небезпечний** до fix; безпечний після fix |

Результат до fix: `#comments` містить `<p><img src="x" onerror="…"></p>`, marker `executed`, запит `GET /x` у Network. Після fix: `<p>&lt;img src=x onerror=…&gt;</p>` (запис тексту в HTML), елементів у `p` — 0, marker `null`, запиту `/x` немає.

## Інвентаризація DOM sinks у власному клієнті (`src/SecureLab.Api/Client/`)

| Місце | Дані, що потрапляють у sink | Категорія даних | Контекст | Рішення |
|---|---|---|---|---|
| `browser-lab.js` `load()`: `#description` (**було** `innerHTML`) | `current.description` | збережене значення користувача | HTML-парсер | замінено на `renderText` → `textContent` (єдиний helper) |
| `browser-lab.js` `load()`: `#comments` (**було** `paragraph.innerHTML`) | `comment.text` | збережене значення користувача | HTML-парсер | замінено на `renderTextParagraphs` → `createElement("p")` + `textContent` |
| `browser-lab.js`: `#title` | `current.title` | збережене значення користувача | текстовий вузол | `textContent` (вже було безпечно), тепер через helper |
| `browser-lab.js`: `elements.description.value = …` | `current.description` | збережене значення користувача | значення поля форми (не markup) | допустимо: властивість `value` не розбирає HTML |
| `browser-lab.js`: `status.textContent` | код статусу відповіді | server-generated | текстовий вузол | допустимо |
| `app.js` (основний клієнт): `createTextElement`, `textContent`, `document.createTextNode` | title, description, імена авторів, тексти коментарів | збережене значення користувача | текстовий вузол | безпечно (перевірено; `innerHTML` немає) |
| `auth-lab.js`: `textContent`, `createElement("dt"/"dd")`, `dataset.state` | профіль, статус, повідомлення відповіді | server-generated / користувач | текстовий вузол | безпечно |
| `.html`: inline `<style>` в `auth-lab.html` | code-owned CSS | trusted constant | style | **винесено** у `auth-lab.css`, щоб CSP не потребувала `unsafe-inline` |
| inline event handlers, `eval`, `new Function`, `document.write`, `insertAdjacentHTML`, `outerHTML` | — | — | — | відсутні (тест `ClientScripts_DoNotUseDangerousHtmlOrCodeSinks`) |
| dynamic URL (`href`/`src`) з даних | — | — | URL | у клієнті відсутні; для можливого посилання — політика `safeHttpUrl` (дозволені `http:`/`https:`, парсинг `new URL`, не regex), перевірена в Console |

Сторонні бібліотеки й згенерований вивід до переліку власних sinks не входять. У досліджуваному flow (`/browser-lab.html`, `/`, `/auth-lab.html`) сторонніх scripts немає; сторінка Development-довідника `/scalar/v1` (стороннє UI package) винесена з CSP як задокументований виняток.

## Повторний render і додаткова перевірка (відмінний)

`renderTextParagraphs` будує `DocumentFragment` і викликає `container.replaceChildren(fragment)`: повторний `load()` повністю замінює вміст, дублювання коментарів немає (DOM-тест: 2 елементи `p` після першого й після третього виклику).
