# Матриця контролю доступу (ЛР 04, baseline 2-A)

Ресурс — `Incident` (власник `Incident.OwnerUserId`); дочірні ресурси — `IncidentComment`
(`GET /api/incident-comments/{id}`) і `IncidentStatusHistory`. Ролі: Reporter, Analyst, Administrator.
Роль і id беруться **лише** з перевіреного principal (`HttpContext.User`), після login. Права, яких
предметне правило (`docs/variant-02-a.md`) прямо не називає, роль **не має**; Administrator не має
автоматичного обходу ownership.

## Політика відмов

| Ситуація | Статус | Обґрунтування |
|---|---|---|
| немає автентифікації | **401** | challenge cookie-схеми; не HTML redirect |
| authenticated, **чужий** (невидимий) ресурс | **404** | відповідь тотожна відповіді для неіснуючого id: наявність чужого ресурсу не розкривається |
| authenticated, ресурс **видимий**, але дія заборонена (owner виконує привілейовану дію; delete не-New; Analyst змінює вміст) | **403** | суб'єкт знає про ресурс, тому розкриття немає; відмова пояснює, що саме не дозволено |
| дозволена роль, але недопустимий поточний стан (Analyst, інцидент не `New`) | **409** | помилка переходу, а не доступу |
| зайве/заборонене поле у DTO (overposting) | **400** | контракт A-08b: `[JsonUnmappedMemberHandling(Disallow)]`, стан не змінюється |

Єдина політика для всього периметра (details, update, delete, triage, child, search, summary): чужий
ресурс → **404** із тілом, що не містить id і відрізняється від «неіснуючого» лише `traceId`.

## Матриця (одна таблиця)

| Дія | Рівень | Анонім | Reporter | Analyst | Administrator | Власник має значення? | Стан реалізації |
|---|---|---|---|---|---|---|---|
| Перегляд списку `GET /api/incidents` | добрий | deny, 401 | лише власні (query scoping) | усі (читання для аналізу) | лише власні (прав «на все» немає) | так, для Reporter | `IncidentQueries.GetListAsync`, тест A-05 |
| Пошук `GET /api/incidents/search` | відмінний (A-11) | deny, 401 | лише власні | усі | лише власні | так | `Lab02Endpoints` (scoping до сортування й `Take(50)`), A-11 |
| Зведення `GET /api/incidents/severity-summary` | відмінний (A-11) | deny, 401 | лише власні (агрегація тільки по власних) | усі | лише власні | так | `IncidentQueries.GetSeveritySummaryAsync`, A-11 |
| Перегляд деталей `GET /api/incidents/{id}` | достатній | deny, 401 | власний — allow; чужий — deny, 404 | allow для аналізу (будь-який інцидент, включно з internal-коментарями) | власний — allow; чужий — deny, 404 | так | `IncidentAuthorizationHandler` (Read), A-02, A-03, A-03R, A-10 |
| Створення інциденту `POST /api/incidents` | успадкований baseline | deny, 401 | allow for self (owner = principal) | allow (owner = principal) | allow (owner = principal) | сервер призначає власника | ЛР 3, A-04/A-05 ЛР 3 |
| Update content `PUT /api/incidents/{id}` | добрий | deny, 401 | власний — allow лише `title`, `description`; чужий — deny, 404 | deny, 403 (читання не дає права зміни вмісту) | чужий — deny, 404 | так | handler (Update), DTO `UpdateIncidentRequest`, A-04, A-08b |
| Delete `DELETE /api/incidents/{id}` | добрий | deny, 401 | власний у статусі `New` — allow; власний не `New` — deny, 403; чужий — deny, 404 | deny, 403 | чужий — deny, 404 | так | handler (Delete), A-04 |
| Зміна status (transition) `POST /api/incidents/{id}/triage` | добрий | deny, 401 | owner — deny, 403; чужий — deny, 404 (обидва з audit) | allow лише `New → Triaged`; інакше 409 | deny, 404 (не бачить інциденту) | ні; роль + поточний статус | handler (Triage), A-06, A-07, A-09 |
| Перегляд internal comment `GET /api/incident-comments/{id}` | добрий | deny, 401 | deny: internal — 404; коментар чужого інциденту — 404; публічний коментар власного — allow | allow після авторизації до parent incident | deny, 404 | ні для internal-правила; доступ до parent перевіряється окремо | `IncidentCommentAuthorizationHandler`, A-12 |
| Export/audit | відмінний (лише якщо flow існує) | — | — | — | — | — | не застосовується: окремого export/audit endpoint у проєкті немає |

### Додаткові умови словами

* **Reporter** створює й редагує лише власний запис; змінювати `severity`/`status`, `OwnerUserId` чи ролі
  не може ні через PUT, ні через transition.
* **Analyst** читає будь-який інцидент і його internal-коментарі та виконує **лише** `New → Triaged`;
  це не дозвіл змінювати вміст, видаляти чи довільно міняти статус.
* **Administrator** не має додаткових прав на `Incident`: окремо опублікованих для цієї області немає,
  тому за відсутності явного дозволу — deny.
* **Child resource, через який можна непрямо отримати дані головного ресурсу:** `IncidentComment` за
  прямим id. Рішення приймається через parent (`incident.OwnerUserId` / роль) + прапорець `IsInternal`.
* Матриця читається разом із тестами: кожен рядок має стійкий ID сценарію (A-01 … A-12).

## Централізація правила

Рішення для окремого ресурсу — у `IncidentAccessRules` (одна функція на операцію) і двох handler-ах
`IAuthorizationHandler` (`Incident`, `IncidentComment`). Endpoint-и викликають `IAuthorizationService`
і не містять вбудованих умов owner/role. Query scoping (list/search/summary) використовує ті самі
предикати видимості (`IncidentAccessRules.Visible`), тож правило не розходиться між шляхами.
