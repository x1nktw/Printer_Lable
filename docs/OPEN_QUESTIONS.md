# Open questions (LabelPrint Pro)

Актуально на **0.9.1** / Bridge **1.3.5**.  
Tracked decisions that must not be silently assumed in schema or plugin contracts.

## FrontPad

Shop API (`get_products`, `new_order`, …) **не используется**.

Заказы с кассы: browser extension Bridge **1.3.5** → локальный webhook (см. [FRONTPAD_KITCHEN.md](FRONTPAD_KITCHEN.md)).

### Still open

1. Сопоставление `productID` из Bridge с локальным каталогом (сейчас часто match по имени / raw id).
2. Нужен ли отдельный маппинг артикулов без shop API.

## Product decisions already fixed

- Scenario A (single workstation, SQLite) for MVP.
- Local Admin/Operator users for history/ACL; auto sign-in as Administrator on startup.
- Dual templates: `DefaultTemplateId` + `OrderItemTemplateId`.
- Unmatched order items print without auto-creating products.
- Marking category tree + `TemperatureRegime` (0.8.0).
- Distribution: Velopack Setup + Portable + auto-update channel; Bridge zip named by extension version.
