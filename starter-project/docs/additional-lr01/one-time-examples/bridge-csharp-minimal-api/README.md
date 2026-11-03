# Мінімальний ASP.NET Core API для мосту C++ → C#

Це безпечний окремий стенд для матеріалу
[`cpp-to-csharp.md`](../../30-prelabs/00-bridges/cpp-to-csharp.md).
Він не використовує базу даних, автентифікацію, Docker або студентський
проєкт. 

Приклад надано в `one-time-examples/` архіву `lab-01-starter.zip`. Він запускається окремо від starter; тег `v0.1.0` для мосту не потрібен. Потребу в мості визначають за самоперевіркою до основного маршруту.

## Передумови

- остання стабільна .NET 10 SDK;
- термінал і `curl`; у Windows слід явно викликати `curl.exe`;
- за бажанням — редактор із підтримкою `.http`-файлів.

`global.json` цього прикладу має `version: 10.0.100`, `rollForward: latestFeature`, `allowPrerelease: false`. Він обирає найвищу встановлену стабільну SDK сімейства `10.0`. Установіть останню стабільну SDK з [Microsoft](https://dotnet.microsoft.com/download/dotnet/10.0). Поза проєктом перевірте `dotnet --list-sdks`, а в цій папці — `dotnet --version`: очікується стабільний номер `10.0.…`. Якщо приклад уже розпаковано раніше, відредагуйте об’єкт `sdk` у **його власному** `global.json` до цих трьох значень, збережіть інші ключі й повторіть перевірку; зміна файла starter не змінює цей файл. `net10.0` та залежності прикладу не змінюйте. Після встановлення SDK перезапустіть IDE й термінал. За проблеми IDE скористайтеся маршрутом оновлення та шляху Rider у README starter. [Правила global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

## Відновлення та складання

Відкрийте термінал у корені розпакованого `lab-01-starter.zip`. Там поряд є `starter-project/` і `one-time-examples/`. Якщо перебуваєте у starter з цього ZIP, спершу виконайте `cd ..`; для клону відкрийте окремий термінал у ZIP. Приклад не додають до Git-проєкту студента. Виконайте:

```bash
cd one-time-examples/bridge-csharp-minimal-api
dotnet --version
dotnet restore
dotnet build --no-restore
```

Успішне складання повідомляє `Build succeeded` і `0 Error(s)`. Код виходу
останньої команди показує команда `echo $?` у bash/zsh або змінна
`$LASTEXITCODE` у PowerShell.

## Запуск

Запустіть API на фіксованій локальній адресі:

```bash
dotnet run --no-build --urls http://127.0.0.1:5077
```

Не закривайте процес. Запити виконуйте в іншому терміналі, у браузері або через
`BridgeApi.http`.

Якщо застосунок завершується винятком із рядком
`Failed to bind to address … address already in use`, порт зайнято. Довгий
stack trace для цієї відмови нормальний. Виберіть інший вільний порт і однаково
змініть його в `--urls` та `@baseUrl` у `BridgeApi.http`.

## Запити в macOS або Linux

```bash
curl -i "http://127.0.0.1:5077/hello"
curl -i "http://127.0.0.1:5077/incidents?owner=olena"
curl -i "http://127.0.0.1:5077/incidents?owner=unknown"
curl -i "http://127.0.0.1:5077/incidents?owner=maksym"
curl -i "http://127.0.0.1:5077/incidents"
curl -i "http://127.0.0.1:5077/delayed-hello"
```

## Запити у Windows

У Windows PowerShell 5.1 ім’я `curl` є псевдонімом `Invoke-WebRequest`, тому
викликайте саме `curl.exe`. Подвійні лапки придатні також для `cmd.exe`:

```powershell
curl.exe -i "http://127.0.0.1:5077/hello"
curl.exe -i "http://127.0.0.1:5077/incidents?owner=olena"
curl.exe -i "http://127.0.0.1:5077/incidents?owner=unknown"
curl.exe -i "http://127.0.0.1:5077/incidents?owner=maksym"
curl.exe -i "http://127.0.0.1:5077/incidents"
curl.exe -i "http://127.0.0.1:5077/delayed-hello"
```

Якщо у Windows `curl.exe` показує спотворені українські літери, але status і JSON-поля правильні, це може бути кодування термінала. У Command Prompt виконайте `chcp 65001` **до нового виклику** `curl.exe`; у PowerShell також задайте `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8`. Повторіть запит. Для звірки можна відкрити той самий GET у браузері/Scalar. Не змінюйте API через саму лише помилку відображення. Докладніше — у довідці Microsoft: [кодові сторінки консолі Windows](https://learn.microsoft.com/en-us/windows/console/console-code-pages) і [команда chcp](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/chcp).

Причину явного виклику пояснює офіційна сторінка
[`curl on Windows`](https://learn.microsoft.com/en-us/windows/curl/).
Прості GET-запити `/hello` і `/delayed-hello` можна також відкрити у браузері.
Для перевірки кодів стану й заголовків використовуйте `curl` або
`BridgeApi.http`.

## Безпечна мінімальна зміна

У `Program.cs` знайдіть коментар `Крок зміни з мосту` і константу
`BridgeText.HelloMessage`. Змініть лише її текстове значення, наприклад на
`"Зміну перевірено"`. Потім:

1. зупиніть застосунок через `Ctrl+C`;
2. повторіть `dotnet build --no-restore`;
3. знову запустіть `dotnet run --no-build --urls http://127.0.0.1:5077`;
4. викличте `/hello` і `/delayed-hello`.

Обидві кінцеві точки читають ту саму константу, тому мають повернути поточне
змінене значення. Не вводьте секрети, HTML або виконуваний код: у вправі
змінюється звичайний навчальний текст відповіді.

## Очікуваний навчальний результат

Після роботи з прикладом студент уміє:

- відрізнити `restore`, `build` і `run`;
- знайти кінцеву точку API та створення об’єкта відповіді;
- прочитати властивість, `string?`, `[Required]`, короткий LINQ-ланцюжок і
  залежність, передану через конструктор;
- розрізнити DI-сервіс і спеціально прив’язаний `CancellationToken`;
- простежити `Task` → `async` → `await` у короткому методі;
- змінити безпечне значення, перезапустити API та перевірити нову відповідь.

Приклад не навчає проєктувати ASP.NET Core застосунок, DI-контейнер або
валідаційну інфраструктуру. Це залишається наступним матеріалам курсу.

