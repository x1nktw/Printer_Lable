# Open questions (LabelPrint Pro)

Актуально на **1.0.0** / Bridge **1.3.15**.  
Tracked decisions that must not be silently assumed in schema or plugin contracts.

## FrontPad

Shop API (`get_products`, `new_order`, …) **не используется**.

Заказы с кассы: browser extension Bridge **1.3.15** → локальный webhook (см. [FRONTPAD_KITCHEN.md](FRONTPAD_KITCHEN.md)).  
Опционально: тёмная тема FrontPad в popup расширения.

### Still open

1. Сопоставление `productID` из Bridge с локальным каталогом (сейчас часто match по имени / raw id).
2. Нужен ли отдельный маппинг артикулов без shop API.

## Product decisions already fixed

- Scenario A (single workstation, SQLite) for MVP / 1.0.
- Local Admin/Operator users for history/ACL; auto sign-in as Administrator on startup.
- Dual templates: `DefaultTemplateId` + `OrderItemTemplateId`.
- Unmatched order items print without auto-creating products.
- Marking category roots + optional subcategories; `TemperatureRegime`; `ProductIconKey`.
- Distribution: Velopack Setup + Portable + auto-update channel; Bridge zip named by extension version.
- No FrontPad shop API sync.
