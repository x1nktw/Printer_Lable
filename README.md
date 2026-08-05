# LabelPrint Pro

**Версия приложения: 1.0.1** · **FrontPad Bridge: 1.3.15** · [.NET 8 / Windows x64](docs/INSTALLER.md)

Коммерческое Windows-приложение для печати термоэтикеток: каталог, маркировка, кухонные заказы FrontPad, очередь печати.

## Что умеет (1.0.0)

- **Каталог** — товары (SKU, штрихкод, EAV), маркировка (корни + подкатегории, срок, температура, иконки), добавки с иконками
- **Маркировка** — быстрая печать сырья / заготовок / полуфабрикатов / соусов
- **Заказы** — FrontPad Bridge → локальный webhook → кухонный чек **40×58**; inbox JSON
- **Шаблоны** — визуальный редактор, превью = печать, импорт/экспорт JSON, системные пресеты
- **Принтеры** — File (PNG), Windows, TSPL, CPCL; очередь, история, reprint
- **Главная** — статус Bridge / FrontPad / принтер / очередь / обновления
- **Система** — тема Fluent + акцент, Velopack-автообновление, плагины

## Стек

| Слой | Технологии |
|------|------------|
| UI | Avalonia 11.2, MVVM (CommunityToolkit.Mvvm) |
| Application | .NET 8, FluentValidation, Result\<T\> |
| Domain | Чистый C#, DDD-элементы (PrintJob, Template) |
| Persistence | SQLite + EF Core |
| Logging | Serilog (file rolling) |
| Updates | Velopack 1.2 + GitHub Releases |
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
  frontpad-bridge/   # Chrome/Edge v1.3.15: заказы + тёмная тема FrontPad
scripts/             # publish-win.ps1, pack-release.ps1
tests/
docs/
```

Кухонные заказы: [docs/FRONTPAD_KITCHEN.md](docs/FRONTPAD_KITCHEN.md) · расширение [extensions/frontpad-bridge](extensions/frontpad-bridge).

## Установка без SDK

[Releases](https://github.com/x1nktw/Printer_Lable/releases) (см. [INSTALLER.md](docs/INSTALLER.md)):

| Файл | Назначение |
|------|------------|
| `LabelPrintPro-win-Setup.exe` | Установщик Velopack (рекомендуется) |
| `LabelPrintPro-win-Portable.zip` | Portable |
| `LabelPrintPro-1.0.0-full.nupkg` + `releases.win.json` | Канал автообновления |
| `frontpad-bridge-1.3.15.zip` | Только Bridge (версия из `manifest.json`) |

```powershell
git tag v1.0.0
git push origin v1.0.0
```

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
- Печать File: `%LocalAppData%\LabelPrintPro\prints\`
- Inbox заказов: `%LocalAppData%\LabelPrintPro\orders-inbox\`

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

Drop compiled plugin DLLs into `plugins/` next to the executable (Velopack: `%LocalAppData%\LabelPrintPro\current\plugins\`). At startup, Infrastructure loads assemblies via `AssemblyLoadContext` and registers types implementing:

- `IVariableProvider`
- `ITemplateElementRenderer`
- `IOrderProvider`

Reference `LabelPrint.Plugins.Abstractions` from your plugin project; do not bundle duplicate copies of Domain/Abstractions assemblies.

## Сценарий эксплуатации

Один ПК, локальный SQLite (сценарий A). Пользователи Admin/Operator — локально, для истории и ACL. При старте выполняется вход от имени администратора.

## Лицензия

Proprietary — коммерческий продукт.
