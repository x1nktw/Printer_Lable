# Architecture

Актуально для **LabelPrint Pro 1.0.0**.

## Направление зависимостей

```
UI ──▶ Application ──▶ Domain
         ▲                ▲
         │                │
Infrastructure ───────────┘
         │
         ├── Infrastructure.Printing  ──▶ Plugins.Abstractions
         └── Infrastructure.FrontPad  ──▶ Plugins.Abstractions

Plugins.Abstractions ──▶ Domain
```

- `Domain` не зависит ни от чего, кроме BCL.
- `Application` не знает про EF Core / Avalonia / HTTP.
- `UI` ссылается на `Infrastructure` **только** в composition root (`Program.cs` / `App.axaml.cs`).
- ViewModels не должны импортировать Infrastructure — проверяется `LabelPrint.ArchitectureTests`.

## Ключевые агрегаты

### PrintJob

Состояния: `Pending → Rendering → Printing → Completed | Failed | Cancelled`.

Переходы только через методы агрегата. Reprint создаёт **новый** job с `SourceJobId`.

### LabelTemplate

Метаданные в таблице + `ContentJson` со `schemaVersion`. Миграция схемы — `ITemplateSchemaMigrator`.  
Элементы: текст, цена, штрихкод, QR, фигуры, линия, иконка (`ProductIconKey` / файл), `AddonsKitchen`.

### Product

- Unique `Sku` / `Barcode` (индексы БД).
- `DefaultTemplateId` + `OrderItemTemplateId` (fallback на default).
- Custom fields — EAV (`CustomFieldDefinition` + `ProductCustomField`), не JSON-blob.
- Маркировка: категории-корни + подкатегории (создаются вручную); `ShelfLife` / единицы; `TemperatureRegime`; `ProductIconKey`.

## Порты плагинов

| Порт | Назначение |
|------|------------|
| `IPrinterGateway` | Печать / статус устройства (File, Windows, TSPL, CPCL, EscPos stub) |
| `IOrderProvider` | Внешние заказы (FrontPad Bridge / inbox) |
| `IVariableProvider` | `{{ProductName}}`, `{{TemperatureRegime}}`, `{{ProductIconKey}}`, `{{Custom.*}}`, … |
| `ITemplateElementRenderer` | Кастомные элементы шаблона |

## UI composition (1.0.0)

Сайдбар: **Главная** · **Заказы** · **Маркировка** · **Каталог** · **Настройки**.  
Каталог — вкладки: Товары · Маркировка · Добавки.  
Настройки — вкладки: Общие (Admin) · Шаблоны · Принтеры · Очередь · История.  
Тема и акцентный цвет — в Общих; акцент красит содержимое контролов, не заливку кнопок.  
Версия сборки показывается на Главной (`LabelPrint Pro v{Major.Minor.Build}`).

## FrontPad

Shop API не используется. Заказы: browser extension → локальный webhook (`OrderWebhookListener`) → inbox/БД.  
Heartbeat Bridge обновляет статус на Главной.

## Ошибки

- Домен: `DomainException`.
- Граница Application: `Result` / `Result<T>` — UI показывает сообщение, не падает.
- Инфраструктура: логируется через Serilog с контекстом (`ProductId`, `PrintJobId`, …).

## Persistence

- SQLite file, EF Core Code-First + миграции при старте.
- Перед migrate — копия `.bak` с меткой версии.
- История: keyset pagination (`CreatedAt` cursor), не `OFFSET` на больших объёмах.
- Подробнее: [PERSISTENCE.md](PERSISTENCE.md).

## Updates

Velopack `UpdateManager` + GitHub Releases (`Updates` в `appsettings.json`).  
Проверка неблокирующая (таймаут), UI Настроек/Главной остаётся отзывчивым.
