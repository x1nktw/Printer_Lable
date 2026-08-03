# LabelPrint Pro

**Версия приложения: 0.8.0** · **FrontPad Bridge: 1.3.4** · [.NET 8 / Windows x64](docs/INSTALLER.md)

Коммерческое Windows-приложение для печати термоэтикеток: каталог, маркировка, кухонные заказы FrontPad, очередь печати.

## Что умеет (0.8.0)

- **Каталог** — товары, маркировка (4 корня + подкатегории, срок годности, температурный режим), добавки с иконками
- **Маркировка / Сырьё** — быстрая печать сырья и маркировочных этикеток
- **Заказы** — FrontPad Bridge → локальный webhook → кухонный чек 40×58
- **Настройки** — общие, принтеры, очередь, история, шаблоны (вкладки); тема и акцентный цвет
- **Главная** — статус Bridge / FrontPad / принтер / очередь

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
  frontpad-bridge/   # Chrome/Edge v1.3.4: заказы FrontPad → webhook
installer/           # Inno Setup (LabelPrintPro.iss)
scripts/             # publish-win.ps1, pack-release.ps1
tests/
docs/
```

Кухонные заказы: [docs/FRONTPAD_KITCHEN.md](docs/FRONTPAD_KITCHEN.md) · расширение [extensions/frontpad-bridge](extensions/frontpad-bridge).

## Установка без SDK

[Releases](https://github.com/x1nktw/Printer_Lable/releases) (см. [INSTALLER.md](docs/INSTALLER.md)):

| Файл | Назначение |
|------|------------|
| `LabelPrintPro-0.8.0-win-x64-setup.exe` | Установщик |
| `LabelPrintPro-0.8.0-win-x64.zip` | Portable |
| `frontpad-bridge-1.3.4.zip` | Только Bridge (версия из `manifest.json`) |

## Быстрый старт (разработка)

Требования: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet restore LabelPrint.sln
dotnet build LabelPrint.sln
dotnet test LabelPrint.sln
dotnet run --project src/LabelPrint.UI
```

Локальный релиз-пакет:

```powershell
./scripts/pack-release.ps1
# → artifacts/release/
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
- [Changelog](CHANGELOG.md) — Keep a Changelog / SemVer
- [Открытые вопросы](docs/OPEN_QUESTIONS.md)
- [Этапы разработки](docs/ROADMAP.md)
- [Installer / publish](docs/INSTALLER.md)

### Первый запуск: принтер

Без принтера печать из маркировки/заказов не стартует. Минимальный путь:

1. **Настройки → Принтеры** → **Добавить виртуальный** → Сохранить (протокол `File`, PNG в `%LocalAppData%\LabelPrintPro\prints\`).
2. Либо протокол `Windows` и в **Подключение** — точное имя очереди из «Принтеры и сканеры».

Подробности: [docs/PRINTERS.md](docs/PRINTERS.md).

## Plugins

Drop compiled plugin DLLs into `plugins/` next to the executable (or `%ProgramFiles%\LabelPrint Pro\plugins\` after install). At startup, Infrastructure loads assemblies via `AssemblyLoadContext` and registers types implementing:

- `IVariableProvider`
- `ITemplateElementRenderer`
- `IOrderProvider`

Reference `LabelPrint.Plugins.Abstractions` from your plugin project; do not bundle duplicate copies of Domain/Abstractions assemblies.

## MVP-сценарий эксплуатации

Один ПК, локальный SQLite (сценарий A). Пользователи Admin/Operator — локально, для истории и ACL. При старте выполняется вход от имени администратора.

## Лицензия

Proprietary — коммерческий продукт.
