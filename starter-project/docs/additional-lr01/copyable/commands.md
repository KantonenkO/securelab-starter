# Готові команди WEB ЛР1

Виконуйте лише потрібний блок після читання передумов у відповідному розділі методички. Starter-команди — у корені starter; мости — у своїх папках. Не виконуйте весь файл. Замініть REPOSITORY_URL на погоджену адресу, REPOSITORY_DIRECTORY — на назву або шлях до створеного каталогу; лапки збережіть. VULNERABLE_SHA/FIXED_SHA у прикладах інших робіт замінюють власними hashes; у ЛР 1 окремий vulnerable commit не потрібен. Не змішуйте Bash і PowerShell; пояснення поза fences не вводять у термінал.

## Розділ 3. LINQ без БД: бачимо результат кожного кроку

```sh
dotnet new console -n LinqDemo -f net10.0
cd LinqDemo
```

## Розділ 4, етап 1, крок 1.2 А. Клонування приватного репозиторію

```bash
git clone "REPOSITORY_URL"
cd "REPOSITORY_DIRECTORY"
git switch -c lab/1-system
git status
```

## Розділ 4, етап 1, крок 1.2 Б. Локальний репозиторій із ZIP

```bash
cd starter-project
git init -b main
git add .
git status
git commit -m "Import SecureLab starter baseline"
git switch -c lab/1-system
git branch --show-current
```

## Розділ 4, етап 1, крок 1.2 Б. Додати приватний remote

```bash
git remote add origin "REPOSITORY_URL"
```

## Розділ 4, етап 1, крок 1.3. Перевірка SDK у корені проєкту

```bash
dotnet --version
```

## Розділ 4, етап 1, крок 2. Перевірте локальну конфігурацію та порти

```bash
git check-ignore -v infra/.env
```

## Розділ 4, етап 1, крок 2. Перевірте локальну конфігурацію та порти

```powershell
netstat -ano | findstr ":5080 :54329"
```

## Розділ 4, етап 1, крок 2. Перевірте локальну конфігурацію та порти

```bash
lsof -nP -iTCP:5080 -sTCP:LISTEN
lsof -nP -iTCP:54329 -sTCP:LISTEN
```

## Розділ 4, етап 1, крок 2. Запуск API з іншим портом PostgreSQL (Bash)

```bash
connection='Host=127.0.0.1;Port=54330;Database=securelab;'
connection+='Username=securelab;Password=local-study-password;'
connection+='Include Error Detail=false'
ConnectionStrings__SecureLab="$connection" \
  dotnet run --project src/SecureLab.Api
unset connection
```

## Розділ 4, етап 1, крок 2. Запуск API з іншим портом PostgreSQL (PowerShell)

```powershell
$env:ConnectionStrings__SecureLab = (
  'Host=127.0.0.1;Port=54330;Database=securelab;' +
  'Username=securelab;Password=local-study-password;' +
  'Include Error Detail=false'
)
try {
  dotnet run --project src/SecureLab.Api
} finally {
  Remove-Item Env:ConnectionStrings__SecureLab
}
```

## Розділ 4, етап 1, крок 3. Запустіть PostgreSQL

```bash
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
docker compose --env-file infra/.env.example -f infra/compose.yaml ps
```

## Розділ 4, етап 1, крок 3. Запустіть PostgreSQL

```bash
docker compose --env-file infra/.env.example -f infra/compose.yaml logs postgres
```

## Розділ 4, етап 1, крок 4. Відновіть залежності та запустіть API

```bash
dotnet tool restore
dotnet restore SecureLab.sln
dotnet run --project src/SecureLab.Api
```

## Розділ 4, етап 1, крок 5. Перевірте базову поведінку

```bash
curl -i "http://localhost:5080/health"
curl -i "http://localhost:5080/api/incidents"
curl -i "http://localhost:5080/api/incidents?status=Triaged"
curl -i "http://localhost:5080/api/incidents?status=Resolved"
curl -i "http://localhost:5080/api/incidents/99999999-9999-9999-9999-999999999999"
curl -i "http://localhost:5080/api/incidents?status=Unknown"
curl -i "http://localhost:5080/api/incidents/severity-summary"
```

## Розділ 4, етап 1, крок 6. Відновіть відомий стан

```bash
dotnet run --no-build --project src/SecureLab.Api -- --reset-database
```

## Розділ 4, етап 1, крок 7. Зупиніть стенд після роботи

```bash
docker compose --env-file infra/.env.example -f infra/compose.yaml down
```

## Розділ 4, етап 2, крок 4. Підтвердьте дані read-only запитом до PostgreSQL (Bash)

```bash
sql='SELECT id, title, description, severity, status '
sql+='FROM incidents WHERE id = '
sql+="'20000000-0000-0000-0000-000000000003';"
docker compose --env-file infra/.env.example \
  -f infra/compose.yaml exec -T postgres \
  psql -U securelab -d securelab -c "$sql"
unset sql
```

## Розділ 4, етап 2, крок 4. Підтвердьте дані read-only запитом до PostgreSQL (PowerShell)

```powershell
$sql = 'SELECT id, title, description, severity, status ' +
  'FROM incidents WHERE id = ' +
  "'20000000-0000-0000-0000-000000000003';"
docker compose --env-file infra/.env.example `
  -f infra/compose.yaml exec -T postgres `
  psql -U securelab -d securelab -c $sql
Remove-Variable sql
```

## Розділ 4. Розібраний приклад перед етапом 3: одна зміна крізь п’ять файлів

```bash
curl -i "http://localhost:5080/api/incidents/latest"
```

## Розділ 4. Розібраний приклад перед етапом 3: одна зміна крізь п’ять файлів

```bash
git diff --stat
git diff
git restore -- src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs
git restore -- src/SecureLab.Api/Application/Incidents/IncidentQueries.cs
git restore -- src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs
git restore -- src/SecureLab.Api/Client/index.html
git restore -- src/SecureLab.Api/Client/app.js
git status
```

## Розділ 6. Автоматизована перевірка

```bash
dotnet run --no-build --project src/SecureLab.Api -- --reset-database
```

## Розділ 6. Автоматизована перевірка

```bash
dotnet build src/SecureLab.Api/SecureLab.Api.csproj --configuration Release
dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release
```

## Розділ 6. Автоматизована перевірка

```bash
bash scripts/test.sh
```

## Розділ 6. Автоматизована перевірка

```powershell
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release
```

## Розділ 6. Безпечний Git-цикл

```bash
git status
git diff
git diff --staged
```

## Розділ 6. Безпечний Git-цикл

```bash
git add src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs
git add src/SecureLab.Api/Application/Incidents/IncidentQueries.cs
git add src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs
git add src/SecureLab.Api/Client/index.html src/SecureLab.Api/Client/app.js
git add tests/http/incidents.http docs/architecture.md
git status
```

## Розділ 6. Безпечний Git-цикл

```bash
git commit -m "Add incident severity summary flow"
```

## Розділ 6. Безпечний Git-цикл

```bash
git status
git log -1 --oneline
```

## Розділ 6. Безпечний Git-цикл

```bash
git switch main
git merge --no-ff lab/1-system
git status
```

## Розділ 6. Безпечний Git-цикл

```bash
git rev-parse HEAD
git tag -a v0.1.0 -m "Laboratory work 1 complete"
git show v0.1.0 --no-patch
git rev-list -n 1 v0.1.0
```

## Розділ 6. Безпечний Git-цикл

```bash
git push -u origin lab/1-system
git push origin main
git push origin v0.1.0
```

## Розділ 6. Що саме завантажити в Moodle

```bash
git status
git ls-files
git log --all --name-only --format=oneline
git bundle create ../securelab-lr01.bundle --branches --tags HEAD
git bundle verify ../securelab-lr01.bundle
git bundle list-heads ../securelab-lr01.bundle
```

## Розділ 6. Що саме завантажити в Moodle

```bash
git clone securelab-lr01.bundle lr01-check
git -C lr01-check tag --list
git -C lr01-check rev-list -n 1 v0.1.0
```

## Розділ 10. Перевірений приклад (запуск мосту HTML → DOM → fetch)

```bash
cd one-time-examples/bridge-browser-fetch-dom
dotnet restore
dotnet build --no-restore
dotnet run --no-build --urls http://127.0.0.1:5078
```

## Розділ 9. Цикл роботи: restore → build → run → запит

```bash
cd one-time-examples/bridge-csharp-minimal-api
dotnet --version
dotnet restore
dotnet build --no-restore
dotnet run --no-build --urls http://127.0.0.1:5077
```

## Розділ 9. Цикл роботи: restore → build → run → запит

```bash
curl -i "http://127.0.0.1:5077/hello"
```

## Розділ 9. Цикл роботи: restore → build → run → запит

```powershell
curl.exe -i "http://127.0.0.1:5077/hello"
```

## Розділ 9. Чому ці три конструкції подано разом

```bash
curl -i "http://127.0.0.1:5077/incidents?owner=olena"
curl -i "http://127.0.0.1:5077/incidents?owner=unknown"
curl -i "http://127.0.0.1:5077/incidents?owner=maksym"
curl -i "http://127.0.0.1:5077/incidents"
```

## Розділ 9. Конструкції C#, які ви зустрінете у starter

```bash
dotnet new console -n ReadDemo -f net10.0
cd ReadDemo
```

## Розділ 8 (додаток А). Перевірений приклад

```bash
dotnet run --no-build --configuration Release \
  --project src/SecureLab.Api -- --reset-database
```

## Розділ 8 (додаток А). Перевірений приклад

```bash
curl -i 'http://localhost:5080/api/incidents?status=Triaged' \
  -H 'Accept: application/json'
```

## Розділ 8 (додаток А). Перевірений приклад

```bash
docker compose --env-file infra/.env.example -f infra/compose.yaml \
  exec -T postgres psql -U securelab -d securelab \
  -c "SELECT id, title, severity, status FROM incidents WHERE status = 'Triaged';"
```

## Розділ 11. 2. Одноразове налаштування

```bash
git --version
```

## Розділ 11. 2. Одноразове налаштування

```bash
git config --global user.name "Ім'я Прізвище"
git config --global user.email "student@example.edu"
```

## Розділ 11. 3. Початок лабораторної роботи

```bash
git clone "REPOSITORY_URL"
cd "REPOSITORY_DIRECTORY"
```

## Розділ 11. 3. Початок лабораторної роботи

```bash
git switch main
git pull --ff-only
```

## Розділ 11. 3. Початок лабораторної роботи

```bash
git switch -c lab/1-system
```

## Розділ 11. 4. Щоденний безпечний цикл

```bash
git status
```

## Розділ 11. 4. Щоденний безпечний цикл

```bash
git diff
git diff --staged
```

## Розділ 11. 4. Щоденний безпечний цикл

```bash
git add path/to/file
git status
```

## Розділ 11. 4. Щоденний безпечний цикл

```bash
git commit -m "Implement server-side validation for create request"
```

## Розділ 11. 5. Стани «до» і «після» виправлення

```bash
git add path/to/relevant/files
git commit -m "Reproduce controlled SQL injection scenario"
git rev-parse HEAD
```

## Розділ 11. 5. Стани «до» і «після» виправлення

```bash
git add path/to/relevant/files
git commit -m "Parameterize search query and add regression test"
git rev-parse HEAD
```

## Розділ 11. 6. Перевірка секретів перед commit

```bash
git diff --staged
```

## Розділ 11. 7. Фінальний тег лабораторної роботи

```bash
git status
git log --oneline -5
```

## Розділ 11. 7. Фінальний тег лабораторної роботи

```bash
git tag -a v0.2.0 -m "Laboratory work 2 completed"
```

## Розділ 11. 7. Фінальний тег лабораторної роботи

```bash
git show --no-patch v0.2.0
```

## Розділ 11. 7. Фінальний тег лабораторної роботи

```bash
git push -u origin lab/2-input-sqli
git push origin v0.2.0
```

## Розділ 11. 8. Команди для самоперевірки

```bash
git status
git branch --show-current
git log --oneline --decorate -10
git tag --list
git show --no-patch v0.1.0
git rev-parse HEAD
```

## Розділ 11. 8. Команди для самоперевірки

```bash
git diff VULNERABLE_SHA..FIXED_SHA
```

## Розділ 13. Відновлення та складання

```bash
cd one-time-examples/bridge-browser-fetch-dom
dotnet restore
dotnet build --no-restore
```

## Розділ 13. Запуск

```bash
dotnet run --no-build --urls http://127.0.0.1:5078
```

## Розділ 13. Що перевірити в браузері

```bash
curl -i -X POST http://127.0.0.1:5078/api/greetings \
  -H 'Content-Type: application/json' \
  -d '{"name":"Олена"}'
```

## Розділ 13. Що перевірити в браузері

```powershell
Invoke-RestMethod -Method Post `
  -Uri http://127.0.0.1:5078/api/greetings `
  -ContentType 'application/json' `
  -Body '{"name":"Олена"}'
```

## Розділ 12. Відновлення та складання

```bash
cd one-time-examples/bridge-csharp-minimal-api
dotnet --version
dotnet restore
dotnet build --no-restore
```

## Розділ 12. Запуск

```bash
dotnet run --no-build --urls http://127.0.0.1:5077
```

## Розділ 12. Запити в macOS або Linux

```bash
curl -i "http://127.0.0.1:5077/hello"
curl -i "http://127.0.0.1:5077/incidents?owner=olena"
curl -i "http://127.0.0.1:5077/incidents?owner=unknown"
curl -i "http://127.0.0.1:5077/incidents?owner=maksym"
curl -i "http://127.0.0.1:5077/incidents"
curl -i "http://127.0.0.1:5077/delayed-hello"
```

## Розділ 12. Запити у Windows

```powershell
curl.exe -i "http://127.0.0.1:5077/hello"
curl.exe -i "http://127.0.0.1:5077/incidents?owner=olena"
curl.exe -i "http://127.0.0.1:5077/incidents?owner=unknown"
curl.exe -i "http://127.0.0.1:5077/incidents?owner=maksym"
curl.exe -i "http://127.0.0.1:5077/incidents"
curl.exe -i "http://127.0.0.1:5077/delayed-hello"
```

## Розділ 15. 1. Передумови

```bash
dotnet --version
docker --version
docker compose version
```

## Розділ 15. 2. Перший запуск

```bash
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
dotnet tool restore
dotnet restore SecureLab.sln
dotnet run --project src/SecureLab.Api
```

## Розділ 15. 2. Перший запуск

```bash
docker compose --env-file infra/.env.example -f infra/compose.yaml down
```

## Розділ 15. 3. Відомий початковий стан

```bash
dotnet run --project src/SecureLab.Api -- --reset-database
```

## Розділ 15. 4. Перевірка

```bash
dotnet build src/SecureLab.Api/SecureLab.Api.csproj --configuration Release
dotnet test tests/SecureLab.Api.Tests/SecureLab.Api.Tests.csproj --configuration Release
```

## Розділ 15. 4. Перевірка

```bash
bash scripts/test.sh
```

## Розділ 4, етап 3. Перевірте trace-id у журналі (відмінний рівень)

```bash
curl -i -H "traceparent: 00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01" "http://localhost:5080/api/incidents/severity-summary"
```

У PowerShell використайте curl.exe з тими самими аргументами. Порівнюйте тільки 32-символьний trace-id після 00-, а не весь Activity.Id.
