# Open questions (LabelPrint Pro)

Актуально на **1.1.0** / Bridge **1.4.4**.  
Tracked decisions that must not be silently assumed in schema or plugin contracts.

## FrontPad

Shop API (`get_products`, `new_order`, …) **не используется**.

Заказы с кассы: browser extension Bridge **1.4.4** (кнопка **LP** на странице) → локальный webhook (см. [FRONTPAD_KITCHEN.md](FRONTPAD_KITCHEN.md)).  
Опционально: тёмная тема FrontPad в окне LP / popup расширения.

### Still open

1. Сопоставление `productID` из Bridge с локальным каталогом (сейчас часто match по имени / raw id).
2. Нужен ли отдельный маппинг артикулов без shop API.

## Product decisions already fixed

- Scenario A (single workstation, SQLite) for MVP / 1.x.
- Local Admin/Operator users for history/ACL; auto sign-in as Administrator on startup.
- Dual templates: `DefaultTemplateId` + `OrderItemTemplateId`; заказы также хранят `OrdersPrintPrinterId`.
- Kitchen qty expands to unit labels with N/M indices (not copies of one job).
- Unmatched order items print without auto-creating products.
- Marking category roots + optional subcategories; `TemperatureRegime`; product/addon icons = user PNG only (no built-in pack).
- Distribution: Velopack Setup + Portable + auto-update channel; Bridge zip named by extension version.
- No FrontPad shop API sync.
