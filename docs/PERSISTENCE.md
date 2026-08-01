# Документация Persistence

## Старт

При запуске UI вызывается `DatabaseInitializer.InitializeAsync()`:

1. Если файл БД существует — копия в `%LocalAppData%\LabelPrintPro\backups\labelprint_yyyyMMdd_HHmmss.db.bak`
2. `MigrateAsync()` применяет EF-миграции
3. Seed: пользователи Admin/Operator, `AppSettings`, 3 системных шаблона

## Индексы

- `Products.Sku` unique
- `Products.Barcode` unique (partial, `WHERE Barcode IS NOT NULL`)
- `Orders.ExternalOrderId` unique
- `PrintHistory (PrintedAt, Status)` для keyset-пагинации

## DateTimeOffset

SQLite: все `DateTimeOffset` хранятся как `long` UTC ticks (value converter в `LabelPrintDbContext`), чтобы `ORDER BY` работал без client evaluation.
