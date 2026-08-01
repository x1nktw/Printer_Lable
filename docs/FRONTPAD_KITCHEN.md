# FrontPad → LabelPrint Pro

## Что используем из официального API

Только **`get_products`** — каталог (артикул, название, цена), не чаще 1 раза в час.  
Секрет и Base URL: Настройки LabelPrint / FrontPad → Общие.  
Кнопка синхронизации каталога: раздел **Каталог** → **FrontPad**.

Остальные методы shop-API (`new_order`, `change_status`, …) **не используем**.

---

## Поток заказов (не API)

1. Расширение **FrontPad Bridge** (`extensions/frontpad-bridge`, версия в `manifest.json`) перехватывает `order.php` в браузере  
2. POST JSON на локальный webhook LabelPrint (`http://127.0.0.1:8765/` по умолчанию)  
3. Автоимпорт в БД + обновление списка (+ автопечать при включении)

Дополнительно: JSON-inbox, ручное создание заказа на странице **Заказы**.

### Установка Bridge

См. [extensions/frontpad-bridge/README.md](../extensions/frontpad-bridge/README.md): загрузить распакованное в Chrome/Edge, обновить после правок, перезагрузить вкладку FrontPad.

### Добавки (модификаторы)

FrontPad шлёт добавки отдельными строками `positions` с `parent` = индекс блюда (часто `"0"`).  
Bridge **склеивает** их в одну позицию заказа:

- `name` — только блюдо  
- `addons` / `comment` — список добавок  

На печати шаблон **«Кухня чек 40×58»** рисует блок **ДОБАВКИ** с иконками (не отдельные этикетки на каждую добавку).

### Схема JSON заказа

```json
{
  "externalOrderId": "14320522",
  "number": "65501",
  "customerName": "Зал",
  "comment": "Без лука",
  "statusCode": "new",
  "orderedAt": "2026-08-01T12:00:00+05:00",
  "items": [
    {
      "sku": "303984",
      "name": "Классика",
      "quantity": 1,
      "price": 340,
      "addons": ["Бекон", "Картофель"],
      "comment": "Бекон\nКартофель"
    }
  ]
}
```

`sku` предпочтительно = артикул после sync каталога; из Bridge часто приходит внутренний `productID`.

### Этикетка кухни

- Пресет: **Кухня чек 40×58** (вертикаль; ширина ленты в принтере = **40** мм).  
- Как настроить принтер: [PRINTERS.md](PRINTERS.md).  
- Дата/время: Настройки → Realtime/ручная; на этикетке — из контекста печати.

Установка расширения: [extensions/frontpad-bridge/README.md](../extensions/frontpad-bridge/README.md).
