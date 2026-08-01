# Open questions (LabelPrint Pro)

Tracked decisions that must not be silently assumed in schema or plugin contracts.

## FrontPad

Shop API (`get_products`, `new_order`, …) **не используется**.

Заказы с кассы: browser extension Bridge → локальный webhook (см. [FRONTPAD_KITCHEN.md](FRONTPAD_KITCHEN.md)).

### Still open

1. Сопоставление `productID` из Bridge с локальным каталогом (сейчас часто match по имени / raw id).
2. Нужен ли отдельный маппинг артикулов без shop API.

## Product decisions already fixed in plan

- Scenario A (single workstation, SQLite) for MVP.
- Local Admin/Operator users for history/ACL.
- Dual templates: `DefaultTemplateId` + `OrderItemTemplateId`.
- Unmatched order items print without auto-creating products.
