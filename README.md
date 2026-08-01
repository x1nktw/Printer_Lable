# LabelPrint Pro

Коммерческое Windows-приложение для печати термоэтикеток: каталог товаров, редактор шаблонов, очередь печати и интеграция с FrontPad.

## Стек

| Слой | Технологии |
|------|------------|
| UI | Avalonia 11.2, MVVM (CommunityToolkit.Mvvm) |
| Application | .NET 8, FluentValidation, Result\<T\> |
| Domain | Чистый C#, DDD-элементы (PrintJob, Template) |
| Persistence | SQLite + EF Core |
| Logging | Serilog (file rolling) |
| Tests | xUnit, FluentAssertions, NSubstitute, NetArchTest |

## Структура solution

```
src/
  LabelPrint.Domain
  LabelPrint.Application
  LabelPrint.Plugins.Abstractions
  LabelPrint.Infrastructure
  LabelPrint.Infrastructure.Printing
  LabelPrint.Infrastructure.FrontPad
  LabelPrint.UI
extensions/
  frontpad-bridge/   # Chrome/Edge: заказы FrontPad → webhook LabelPrint
tests/
  …
docs/
plugins/
scripts/
```

Кухонные заказы FrontPad: [docs/FRONTPAD_KITCHEN.md](docs/FRONTPAD_KITCHEN.md), расширение [extensions/frontpad-bridge](extensions/frontpad-bridge).

## Быстрый старт

Требования: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet restore LabelPrint.sln
dotnet build LabelPrint.sln
dotnet test LabelPrint.sln
dotnet run --project src/LabelPrint.UI
```

База и логи по умолчанию:

- БД: `%LocalAppData%\LabelPrintPro\labelprint.db`
- Логи: `%LocalAppData%\LabelPrintPro\logs\`
- Бэкапы перед миграцией: `%LocalAppData%\LabelPrintPro\backups\`

## Документация

- [Архитектура](docs/ARCHITECTURE.md)
- [Persistence](docs/PERSISTENCE.md)
- [Принтеры — как добавить](docs/PRINTERS.md)
- [FrontPad / кухня](docs/FRONTPAD_KITCHEN.md)
- [Changelog](CHANGELOG.md) — ведём подробно (Keep a Changelog)
- [Открытые вопросы](docs/OPEN_QUESTIONS.md)
- [Этапы разработки](docs/ROADMAP.md)
- [Installer / publish](docs/INSTALLER.md)

### Первый запуск: принтер

Без принтера печать из каталога/заказов не стартует. Минимальный путь:

1. **Принтеры** → **Добавить виртуальный** → Сохранить (протокол `File`, PNG в `%LocalAppData%\LabelPrintPro\prints\`).
2. Либо протокол `Windows` и в **Подключение** — точное имя очереди из «Принтеры и сканеры».

Подробности и TSPL/сеть: [docs/PRINTERS.md](docs/PRINTERS.md).

## Plugins

Drop compiled plugin DLLs into `plugins/` next to the executable (or `%ProgramFiles%\LabelPrint Pro\plugins\` after install). At startup, Infrastructure loads assemblies via `AssemblyLoadContext` and registers types implementing:

- `IVariableProvider`
- `ITemplateElementRenderer`
- `IOrderProvider`

Reference `LabelPrint.Plugins.Abstractions` from your plugin project; do not bundle duplicate copies of Domain/Abstractions assemblies.

## MVP-сценарий эксплуатации

Один ПК, локальный SQLite (сценарий A). Пользователи Admin/Operator — локально, для истории и ACL.

## Лицензия

Proprietary — коммерческий продукт.
