# Документация Persistence

Актуально для **LabelPrint Pro 0.8.0**.

## Старт

При запуске UI вызывается `DatabaseInitializer.InitializeAsync()`:

1. Если файл БД существует — копия в `%LocalAppData%\LabelPrintPro\backups\labelprint_yyyyMMdd_HHmmss.db.bak`  
   (чтение пути бэкапа устойчиво к ещё не применённым миграциям колонок `AppSettings`)
2. `MigrateAsync()` применяет EF-миграции
3. Seed: пользователи Admin/Operator, `AppSettings`, системные шаблоны, категории маркировки (корни + подкатегории Сырья), примеры сырья, каталог добавок

## Индексы

- `Products.Sku` unique
- `Products.Barcode` unique (partial, `WHERE Barcode IS NOT NULL`)
- `Orders.ExternalOrderId` unique
- `PrintHistory (PrintedAt, Status)` для keyset-пагинации

## DateTimeOffset

SQLite: все `DateTimeOffset` хранятся как `long` UTC ticks (value converter в `LabelPrintDbContext`), чтобы `ORDER BY` работал без client evaluation.

## Миграции (накопительно к 0.8.0)

Помимо `InitialCreate`:

- `AddLabelDateTimeSettings` — `AppSettings.LabelDateTimeMode`, `ManualLabelDateTime`
- `AddPrinterRotate90` — `Printers.Rotate90`
- `AddPrinterPrintOffset` — `PrintOffsetXMm` / `PrintOffsetYMm`
- `AddProductTemperatureRegime` — `Products.TemperatureRegime`
