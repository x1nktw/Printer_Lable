# Architecture

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

### Product

- Unique `Sku` / `Barcode` (индексы БД).
- `DefaultTemplateId` + `OrderItemTemplateId` (fallback на default).
- Custom fields — EAV (`CustomFieldDefinition` + `ProductCustomField`), не JSON-blob.
- Маркировка (0.8.0): категории-корни + подкатегории; `ShelfLife` / единицы; `TemperatureRegime` (переменная печати `TemperatureRegime`).

## Порты плагинов

| Порт | Назначение |
|------|------------|
| `IPrinterGateway` | Печать / статус устройства |
| `IOrderProvider` | Внешние заказы (FrontPad Bridge / inbox) |
| `IVariableProvider` | `{{ProductName}}`, `{{TemperatureRegime}}`, `{{Custom.*}}`, … |
| `ITemplateElementRenderer` | Кастомные элементы шаблона |

## UI composition (0.8.0)

Сайдбар: Главная · Заказы · Маркировка · Каталог · Настройки.  
Вкладки **Настройки**: Общие · Принтеры · Очередь · История · Шаблоны.  
Тема и акцентный цвет — в Общих; акцент красит содержимое контролов, не заливку кнопок.

## Ошибки

- Домен: `DomainException`.
- Граница Application: `Result` / `Result<T>` — UI показывает сообщение, не падает.
- Инфраструктура: логируется через Serilog с контекстом (`ProductId`, `PrintJobId`, …).

## Persistence

- SQLite file, EF Core Code-First + миграции при старте.
- Перед migrate — копия `.bak` с меткой версии.
- История: keyset pagination (`CreatedAt` cursor), не `OFFSET` на больших объёмах.
