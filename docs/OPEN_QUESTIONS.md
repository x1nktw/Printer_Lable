# Open questions (LabelPrint Pro)

Tracked decisions that must not be silently assumed in schema or plugin contracts.

## FrontPad

Source: [API Frontpad](https://docs.google.com/document/d/1gs81CYvJ6FD9KOseL3GOcrcR2YnEvjQqJn9mJRRc5Yk/edit?tab=t.0).

**Используем только `get_products`** (каталог, ≤1/час). Остальной shop-API не подключаем.

Заказы с кассы: browser extension Bridge → локальный webhook (см. [FRONTPAD_KITCHEN.md](FRONTPAD_KITCHEN.md)).

### Still open

1. Сопоставление `productID` из Bridge с артикулом после `get_products` (сейчас часто match по имени / raw id).
2. Confirm Corporate/Professional tariff if production secret required for `get_products`.

## Product decisions already fixed in plan

- Scenario A (single workstation, SQLite) for MVP.
- Local Admin/Operator users for history/ACL.
- Dual templates: `DefaultTemplateId` + `OrderItemTemplateId`.
- Unmatched order items print without auto-creating products.
