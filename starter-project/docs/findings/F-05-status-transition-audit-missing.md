# F-05. Немає audit event для дозволеного привілейованого переходу Incident.Status

**Статус:** Fixed  
**Актив:** A4 привілейований перехід статусу, A5 audit events  
**Компонент:** `IncidentMutationEndpoints.TriageAsync`, `Application/Authorization/AuditLog.cs`  
**Пріоритет:** P3

## Передумови і межі огляду

Огляд журналів і audit trail (етап 3, п. 4). Відмова привілейованої дії логувалась (`authorization.denied`, ЛР 4), дозволений перехід `New → Triaged` — ні (лише рядок у таблиці історії БД). Команда: `dotnet test --filter AuditTrail` до виправлення.

## Відтворення

1. morgan (Analyst) виконує `POST /api/incidents/{id}/triage` для запису зі статусом `New` → `200`.
2. alice (Reporter) пробує те саме → `403` і подія `authorization.denied …`.
3. Очікувано: **обидва** результати мають audit event із subject, action, resource, result, time. Фактично: подія є лише для відмови.

## Вплив і ймовірність

**Статус перевірки:** спостережено локально (`AUD_01…` червоний до виправлення, `docs/evidence/lab-06/red_audit_before_fix.txt`). Impact: Low (немає операційного сліду «хто й коли виконав дозволений перехід» у журналі подій; є лише БД), likelihood: Medium (кожен перехід). P3.

## Першопричина

Audit був реалізований лише для denial-гілки; успішна гілка після `SaveChangesAsync` ніяких подій не писала.

## Виправлення

Fixed commit `180096f`: `AuditLog.StatusChanged` — подія `incident.status.changed` із `subject`, `action`, `resource`, `result=allowed`, `from`/`to`, `occurredAtUtc`, `traceId`; пишеться лише після успішного збереження. Free-text `reason`, cookie, токени й тіло запиту в подію не потрапляють (за принципом мінімізації даних).

## Retest

`AuditTrailTests.AUD_01…`: дозволений перехід (morgan) і відмова (alice) мають події з усіма полями; БД показує `Triaged` лише для дозволеного запису; у жодному рядку немає тексту причини, пароля, імені cookie чи antiforgery. Ручний прогін: `docs/evidence/lab-06/log_sensitive_check.txt` (рядки `incident.status.changed` і `authorization.denied`).

## Positive regression

Перехід працює як раніше (`AuthorizationTests`: `New → Triaged`, повторний → `409`); denial-подія не змінилася (`A09…`).

## Залишковий ризик

Журнал — консольний sink без захисту від підміни/ретенції (audit trail у БД окремий); час береться з годинника сервера.

## Доказ

`AuditTrailTests`, `red_audit_before_fix.txt`, `log_sensitive_check.txt`.
