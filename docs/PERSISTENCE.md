# Документация Persistence

Актуально для **LabelPrint Pro 1.1.0**.

## Старт

При запуске UI вызывается `DatabaseInitializer.InitializeAsync()`:

1. Если файл БД существует — копия в `%LocalAppData%\LabelPrintPro\backups\labelprint_yyyyMMdd_HHmmss.db.bak`  
   (чтение пути бэкапа устойчиво к ещё не применённым миграциям колонок `AppSettings`)
2. `MigrateAsync()` применяет EF-миграции
3. Seed:
   - пользователи Admin / Operator
   - `AppSettings`
   - системные шаблоны (upsert **без** перезаписи `ContentJson` у существующих пресетов; обновления макета — через явные миграции)
   - категории-корни маркировки (Сырьё, Заготовки, Полуфабрикаты, Соусы)
   - примеры сырья (по необходимости)
   - каталог добавок (базовый seed имён)
4. Подкатегории маркировки **не** сидятся автоматически — создаются вручную; legacy-сиды архивируются при апгрейде

## Индексы

- `Products.Sku` unique
- `Products.Barcode` unique (partial, `WHERE Barcode IS NOT NULL`)
- `Orders.ExternalOrderId` unique
- `PrintHistory (PrintedAt, Status)` для keyset-пагинации

## DateTimeOffset

SQLite: все `DateTimeOffset` хранятся как `long` UTC ticks (value converter в `LabelPrintDbContext`), чтобы `ORDER BY` работал без client evaluation.

## Миграции (накопительно к 1.1.0)

Помимо `InitialCreate`:

| Миграция | Суть |
|----------|------|
| `AddLabelDateTimeSettings` | `AppSettings.LabelDateTimeMode`, `ManualLabelDateTime` |
| `AddPrinterRotate90` | `Printers.Rotate90` |
| `AddPrinterPrintOffset` | `PrintOffsetXMm` / `PrintOffsetYMm` |
| `AddAddons` | таблица добавок / иконки |
| `AddProductShelfLifeUnit` | единицы срока годности |
| `AddPrintTemplateSelections` | выбранные шаблоны заказов/маркировки |
| `AddAccentColor` | `AppSettings.AccentColor` |
| `AddProductTemperatureRegime` | `Products.TemperatureRegime` |
| `AddProductIconKey` | `Products.ProductIconKey` (иконка маркировки) |
| `AddOrdersPrintPrinterId` | `AppSettings.OrdersPrintPrinterId` (принтер авто/ручной печати заказов) |

Путь БД по умолчанию: `%LocalAppData%\LabelPrintPro\labelprint.db`  
(override: `LabelPrint:DatabasePath` / `DatabaseFileName` в `appsettings.json`).
