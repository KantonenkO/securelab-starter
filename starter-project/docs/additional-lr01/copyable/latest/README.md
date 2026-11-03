# Тренування /latest — фрагменти, а не заміна файлів

Працюйте лише на чистому starter до власного етапу 3. Приклад не комітять і не здають. Виконайте його, якщо ще не змінювали наскрізний ASP.NET Core/browser маршрут. Файли 01–08 вставляють за кроками розділу «Розібраний приклад перед етапом 3» методички; вони не є повними файлами starter.

1. `01-response.cs` — у кінець IncidentResponses.cs.
2. `02-query.cs` — усередину класу IncidentQueries після GetDetailsAsync.
3. `03-route.cs` — перед return endpoints у MapIncidentEndpoints.
4. `04-endpoint.cs` — усередину класу IncidentEndpoints.
5. `05-server-check.txt` — після перезапуску API, з кореня starter. У Windows curl.exe. Очікуваний JSON — `06-response.json`; статус 200, application/json.
6. `07-markup.html` — перед </main> у Client/index.html.
7. `08-client.js` — перед реєстрацією submit у Client/app.js. Оновіть браузер; кнопка створює GET /api/incidents/latest і показує title та createdAtUtc як текст.
8. `09-restore.txt` — тільки після перегляду diff й підтвердження, що п’ять файлів містять лише тренувальні зміни. Зупиніть API. Після restore git status чистий.

Фінальне severity-summary реалізуйте самостійно за контрактом етапу 3. Цей приклад його не реалізує.
