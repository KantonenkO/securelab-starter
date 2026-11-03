# Перевірка перекладу EF Core / Npgsql без БД

Це нейтральний приклад про книжки, не готове рішення severity-summary. Потрібен сумісний .NET 10 SDK. У цій папці виконайте `dotnet restore`, потім `dotnet run`.

Очікування: три EXPECTED TRANSLATION ERROR (сортування/фільтр після record і сортування після групового record); два SQL OK із SQL; MEMORY: A,B,C. Для SQL OK видно WHERE/ORDER BY, а для груп — GROUP BY/ORDER BY. Kind зберігався б текстом; SQL упорядковує назви, а не критичність enum.

ToQueryString не відкриває з'єднання і не виконує SQL. Цей приклад не перевіряє дані, PostgreSQL чи HTTP. Dummy connection string не містить пароля й потрібен лише для вибору провайдера. Якщо у майбутній версії провайдер почне перекладати певний вираз, проба навмисно зупиниться: звірте версії з .csproj і реальний SQL. Не встановлюйте довільні інші версії для обходу помилки starter.

Документація: https://learn.microsoft.com/en-us/ef/core/querying/client-eval
