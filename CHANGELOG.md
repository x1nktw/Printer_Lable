# Changelog

Все значимые изменения проекта документируются в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/),
версионирование — [SemVer](https://semver.org/lang/ru/).

## [Unreleased]

### Added

#### UI и навигация
- Пункт меню **Сырьё**: быстрая печать маркировки сырья (список категории «Сырьё», свободное имя, принтер, override даты/времени).
- В **Каталоге**: кнопка **FrontPad** (`get_products`, лимит 1/час), тулбар на `WrapPanel`, override даты/времени при печати.
- В **Настройках**: блок **Дата/время на этикетках** — `Realtime` или ручная метка (`LabelDateTimeMode` / `ManualLabelDateTime`).
- Документация для оператора: [PRINTERS.md](docs/PRINTERS.md) (как добавить виртуальный / Windows / сетевой принтер).

#### Дата и время на этикетках
- Сервис `ILabelDateTimeService`: приоритет override при печати → Manual из настроек → `Now`.
- Провайдеры переменных `Date`, `Time`, `ExpireDate`.
- `PrintService` кладёт `Date`/`Time` в `VariableContext` для товара, сырья и позиций заказа.
- Рендер/превью: `CurrentDate`/`CurrentTime` берут значения из контекста, если заданы.

#### Сырьё
- Seed категории **«Сырьё»** и примеров (Мясо, Томаты, Лук, Сыр, …) с шаблоном «Сырьё 58×40».
- `PrintRawLabelAsync` — печать по имени + шаблон сырья.

#### Кухонные этикетки (заказ)
- Системный пресет **«Кухня чек 40×58»** (вертикаль): чёрная шапка, № заказа, дата/время с иконками, крупное название, блок **ДОБАВКИ** с иконками, бейдж N/M.
- Встроенный шрифт **Inter** (Regular/Bold, с кириллицей) и PNG-иконки (календарь, часы, перец, сыр, лук).
- Рендер: белый текст на чёрном (`Invert`), перенос строк, скруглённые бейджи, `Dashed` линии, элементы `Image`, спец-режим `AddonsKitchen`.
- Печать позиции заказа по умолчанию предпочитает шаблон «Кухня чек …».

#### FrontPad Bridge (`extensions/frontpad-bridge` v1.2.1)
- Добавки (`positions.parent`, в т.ч. индекс `"0"`) **не** становятся отдельными позициями.
- В inbox: `name` = блюдо, `addons` / `comment` = список добавок для блока на этикетке.
- Исправлен falsy-баг JS: `parent === "0"` раньше терялся.

#### Системные шаблоны (upsert при старте)
- Ценник 58×40, Срок 58×30, Позиция заказа, Сырьё 58×40, Штрихкод 58×40, Кухня 58×40, **Кухня чек 40×58**.
- Устаревшие «Кухня чек 58×80 / 58×40» архивируются при инициализации БД.

### Changed

- Тема только из **Настроек** (кнопка темы убрана из сайдбара).
- Синхронизация каталога FrontPad перенесена из **Заказов** в **Каталог**.
- `OrderService.SyncFromProviderAsync` = только inbox (без `get_products`).
- Заказы: короче баннер, ручная панель заказа свёрнута по умолчанию.
- Ширина кухонного чека: с горизонтали 58×40/80 на вертикаль **40×58**.

### Fixed

- Старт приложения падал на старой БД: бэкап читал `AppSettings` **до** миграции и требовал колонку `LabelDateTimeMode` → чтение только `BackupPath` + fallback.
- Добавки FrontPad считались отдельными позициями этикетки (заказ вроде 65535: Классика / Бекон / Картофель).
- AccessViolation в рендере добавок: двойной `Dispose` у `SKFont` / lifetime встроенного Inter.
- **Windows-печать: этикетка выходила в разы мельче ленты** — `DrawImage` растягивал PNG на `MarginBounds` страницы драйвера. Теперь страница и отрисовка в физических мм шаблона (`label.WidthMm` × `label.HeightMm`).
- **Печать на двух этикетках / неверная ориентация** — портретный макет 40×58 на рулоне 58 мм шёл вдоль подачи. Автоповорот 90°, если ширина ленты ближе к длинной стороне макета; галка **Повернуть 90°** в настройках принтера; миграция `AddPrinterRotate90`.
- **Верх этикетки обрезан, снизу пусто** — HardMargin/PrintableArea + авто-запас ~1.5 мм; поля **смещ. X/Y мм** в настройках принтера; лог origin при печати.

### Database

- Миграция `AddLabelDateTimeSettings`: `AppSettings.LabelDateTimeMode`, `AppSettings.ManualLabelDateTime`.
- Миграция `AddPrinterRotate90`: `Printers.Rotate90`.
- Миграция `AddPrinterPrintOffset`: `Printers.PrintOffsetXMm`, `PrintOffsetYMm`.

---

## [0.3.0] - 2026-08-01

### Added

- Документация: [README](README.md), [ARCHITECTURE](docs/ARCHITECTURE.md), [ROADMAP](docs/ROADMAP.md), [CHANGELOG](CHANGELOG.md).
- Persistence: `LabelPrintDbContext`, EF Core configurations, unique indexes (Sku/Barcode/ExternalOrderId).
- Репозитории + `UnitOfWork`, `DatabaseInitializer` (backup `.bak` перед migrate, seed users/presets/settings).
- Миграция `InitialCreate`.
- Integration tests: migrate + product CRUD, unique SKU, seed, keyset history pagination.

### Notes (исторический Unreleased, свернуто в этапы 3–8)

Ниже — ранее накопленные пункты до выделения кухонного/сырьевого среза (см. git history / Unreleased выше для актуального).

- Настройки, каталог (категории, EAV, CSV), шаблоны и редактор (snap, undo, переменные).
- Печать: File / Windows / TSPL / CPCL gateways, очередь, история.
- Заказы: inbox JSON, matching, webhook, FrontPad Bridge; из API только `get_products`.
- См. также [FRONTPAD_KITCHEN.md](docs/FRONTPAD_KITCHEN.md), [INSTALLER.md](docs/INSTALLER.md).

## [0.2.0] - 2026-07-31

### Added

- Application-порты репозиториев и `IUnitOfWork`.
- `ProductService` / `CategoryService` с FluentValidation и `Result<T>`.
- In-memory fakes и unit-тесты каталога (SKU/barcode uniqueness, EAV required fields, soft archive).
- Architecture tests (NetArchTest): Domain/Application/Plugins/UI ViewModels.

### Changed

- Avalonia понижена до **11.2.5** (совместимость с Roslyn в .NET 8 SDK; Avalonia 12 требует более новый компилятор).

## [0.1.0] - 2026-07-31

### Added

- Solution skeleton по MASTER_SPEC v2.0.
- Domain: Product (Money/Weight VO, dual templates), Category tree, CustomField EAV, LabelTemplate (JSON + schemaVersion), Printer, PrintJob FSM, Order/OrderItem, PrintHistory, User, AppSettings.
- `LabelPrint.Plugins.Abstractions`: `IPrinterGateway`, `IOrderProvider`, `IVariableProvider`, `ITemplateElementRenderer`, `ITemplateSchemaMigrator`.
- Infrastructure composition root: Serilog file sink, printing/FrontPad DI placeholders.
- UI composition root: DI + `appsettings.json`.
- Domain unit tests для `PrintJob` state machine.

### Removed

- Проекты `LabelPrint.Shared`, верхнеуровневые `LabelPrint.Printing` / `LabelPrint.FrontPad` (перенесены под Infrastructure).

[Unreleased]: https://github.com/local/LabelPrintPro/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/local/LabelPrintPro/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/local/LabelPrintPro/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/local/LabelPrintPro/releases/tag/v0.1.0
