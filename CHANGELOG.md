# Changelog

Все значимые изменения проекта документируются в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.1.0/),
версионирование — [SemVer](https://semver.org/lang/ru/).

## [Unreleased]

## [1.0.1] - 2026-08-05

### Fixed

- Системные шаблоны больше **не перезаписываются** при каждом запуске — правки в редакторе сохраняются.
- Экспорт JSON: диалог **«Сохранить как…»**; по умолчанию — `Документы\LabelPrint Pro\exports\` (не внутри Velopack/AppData).
- При старте JSON из старой папки `%LocalAppData%\LabelPrintPro\exports\` копируется в Documents, если файлы ещё там есть.
- Выбор шаблона маркировки по умолчанию ищет **«Маркировка»**, не только «Сырьё».

## [1.0.0] - 2026-08-05

Первый стабильный релиз LabelPrint Pro: каталог и маркировка, кухонные заказы FrontPad, редактор шаблонов, очередь печати, Velopack-автообновление.

**Состав релиза:** приложение **1.0.0** · FrontPad Bridge **1.3.15**.

### Added

- Полный операторский контур: **Главная**, **Заказы**, **Маркировка**, **Каталог**, **Настройки** (Общие / Шаблоны / Принтеры / Очередь / История).
- Каталог: товары (SKU/штрихкод, EAV), **Маркировка** (корни + ручные подкатегории, срок годности, температура, иконки), **Добавки** (иконки для FrontPad).
- Печать: протоколы File / Windows / TSPL / CPCL; очередь, retry, reprint, история.
- Системные шаблоны: Ценник, Срок, Позиция заказа, **Маркировка 58×40**, Штрихкод, Кухня, **Кухня чек 40×58**.
- Редактор шаблонов: текст/цена/штрихкод/QR/фигуры/линия/**иконка**/добавки; undo/redo; **Превью печати** (Skia); Invert; импорт/экспорт JSON.
- FrontPad Bridge **1.3.15**: webhook заказов, heartbeat, склейка добавок, устойчивый парсер имён с запятыми, опциональная **тёмная тема** FrontPad.
- Статус системы на Главной (Bridge / FrontPad / принтер / очередь / обновления).
- Автообновление Velopack (Setup + Portable + GitHub Releases).
- Тема Fluent + акцентный цвет; дата/время на этикетках (realtime / ручная / override).

### Changed

- Дистрибуция: Velopack (вместо Inno Setup с 0.9.0); single-file + `config/` / `plugins/` / `extensions/`.
- Каталог только для редактирования; печать — из Маркировки / Заказов.
- Маркировка: без автоматических подкатегорий-сидов; только пользовательские PNG-иконки в списках выбора.
- Shop API FrontPad не используется (только Bridge + JSON-inbox).

### Fixed

- Превью редактора совпадает с печатью; линии/поворот/шрифт pt; Windows-печать в мм шаблона; поворот 90° и смещения принтера.
- UI на низких экранах: скролл форм принтеров, узкие колонки таблиц, диалоги подтверждения.
- Настройки/статус не блокируются проверкой обновлений.

### Upgrade

- С **0.9.x (Velopack):** Настройки → Система → Обновить, либо новый Setup.
- С **0.8.x (Inno):** однократно установить `LabelPrintPro-win-Setup.exe`.
- Bridge: обновить расширение до **1.3.15** и перезагрузить вкладку FrontPad.

## [0.9.1] - 2026-08-04

### Fixed

- Настройки больше не «зависают» на проверке обновлений: страница открывается сразу, GitHub/Velopack check идёт в фоне с таймаутом.
- Статус системы сначала показывает локальные проверки (Bridge/принтер/очередь), затем обновления; ошибки статуса не глотаются молча.
- Publish снова **single-file** + папки `config/`, `plugins/`, `extensions/` (без сотен DLL в корне установки).

## [0.9.0] - 2026-08-04

### Added

- Автообновление через Velopack: **Настройки → Система → Обновить** скачивает пакет, применяет обновление и перезапускает приложение.

### Changed

- Дистрибуция переведена с Inno Setup на **Velopack 1.2.0**: release-ассеты теперь включают `LabelPrintPro-win-Setup.exe`, `LabelPrintPro-win-Portable.zip`, `.nupkg` и `releases.win.json`.
- Publish/layout для Windows перестроен под Velopack folder build (`artifacts/publish/vpk-app`, `config/`, `plugins/`, `extensions/frontpad-bridge/`).
- Проверка обновлений использует `UpdateManager` + GitHub Releases; portable и перенесённые Velopack-установки получают one-click update.

## [0.8.0] - 2026-08-04

### Added

- Маркировка: корни (Сырьё, Заготовки, Полуфабрикаты, Соусы), подкатегории, поле **температурный режим**, переменная печати `TemperatureRegime`.
- Главная: блок **Статус системы** (Bridge, FrontPad, принтер, очередь, последняя печать).
- Publish: self-contained **single-file** win-x64 (`LabelPrint.UI.exe` + `config/` + `plugins/` + `extensions/frontpad-bridge/`).
- Релиз: **Inno Setup** установщик + portable ZIP + отдельный zip Bridge; GitHub Actions по тегу `v*`.
- CI/CD: GitHub Actions — сборка/тесты и релиз по тегу `v*`.
- Каталог добавок с иконками; срок годности; сырьё; кухонный чек 40×58; FrontPad Bridge v1.3.4 (добавки, heartbeat).

### Changed

- Акцентный цвет темы; пункты Принтеры/Очередь/История/Шаблоны — вкладки Настроек; каталог без печати.
- Удалён FrontPad shop API и ручной ввод заказа (только Bridge + inbox).

### Fixed

- Выравнивание/шрифты в редакторе; Windows-печать в мм; поворот 90° и смещения; добавки FrontPad не плодят лишние этикетки; бэкап БД до миграции.

### Database

- `AddLabelDateTimeSettings`, `AddPrinterRotate90`, `AddPrinterPrintOffset`, `AddProductTemperatureRegime`, `AddAddons`, `AddProductShelfLifeUnit`, `AddPrintTemplateSelections`, `AddAccentColor`.

## [0.3.0] - 2026-08-01

### Added

- Persistence: EF Core + SQLite, репозитории, seed, интеграционные тесты.
- Документация README / ARCHITECTURE / ROADMAP / CHANGELOG.

## [0.2.0] - 2026-07-31

### Added

- Application-порты, Product/Category services, unit- и architecture-тесты.

### Changed

- Avalonia **11.2.5** (совместимость с .NET 8 SDK).

## [0.1.0] - 2026-07-31

### Added

- Solution skeleton по MASTER_SPEC v2.0: Domain, Plugins.Abstractions, Infrastructure composition, UI shell.

[Unreleased]: https://github.com/x1nktw/Printer_Lable/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/x1nktw/Printer_Lable/releases/tag/v1.0.0
[0.9.1]: https://github.com/x1nktw/Printer_Lable/releases/tag/v0.9.1
[0.9.0]: https://github.com/x1nktw/Printer_Lable/releases/tag/v0.9.0
[0.8.0]: https://github.com/x1nktw/Printer_Lable/releases/tag/v0.8.0
[0.3.0]: https://github.com/x1nktw/Printer_Lable/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/x1nktw/Printer_Lable/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/x1nktw/Printer_Lable/releases/tag/v0.1.0
