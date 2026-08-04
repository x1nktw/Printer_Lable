# FrontPad → LabelPrint Pro

Совместимо с **LabelPrint Pro 0.9.1** и **FrontPad Bridge 1.3.5**.

## Shop API

**Не используем.** Методы `get_products`, `new_order` и остальной internet-shop API отключены.  
Каталог ведётся локально (ручной ввод / CSV). Секрет и Base URL в настройках не нужны.

---

## Поток заказов

1. Расширение **FrontPad Bridge 1.3.5** (`extensions/frontpad-bridge`) перехватывает `order.php` в браузере  
2. POST JSON на локальный webhook LabelPrint (`http://127.0.0.1:8765/` по умолчанию)  
3. Автоимпорт в БД + обновление списка (+ автопечать при включении)  
4. Heartbeat Bridge → индикатор на **Главной** («Статус системы»)

Дополнительно: JSON-файлы в inbox (`%LocalAppData%\LabelPrintPro\orders-inbox`) — кнопка **Inbox** / **Пример** на странице **Заказы**.

Webhook URL: **Настройки → Общие** → FrontPad Bridge.

Иконки добавок на кухонной этикетке настраиваются в **Каталог → Добавки** (название как в FrontPad + иконка; без записи остаётся кружок).

### Установка Bridge 1.3.5

В релизе / publish / установщике расширение уже лежит в `extensions/frontpad-bridge/` (рядом с `LabelPrint.UI.exe`).  
Отдельный артефакт релиза: `frontpad-bridge-1.3.5.zip`.

Chrome/Edge → Режим разработчика → **Загрузить распакованное** → эта папка.  
См. `INSTALL.txt` и [README расширения](../extensions/frontpad-bridge/README.md).

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

`sku` из Bridge часто = внутренний `productID`; сопоставление с каталогом — по SKU / имени (без sync через API).

### Этикетка кухни

- Пресет: **Кухня чек 40×58** (вертикаль; ширина ленты в принтере = **40** мм).  
- Как настроить принтер: [PRINTERS.md](PRINTERS.md).  
- Дата/время: **Настройки → Общие** → Realtime/ручная; на этикетке — из контекста печати.

Установка расширения: [extensions/frontpad-bridge/README.md](../extensions/frontpad-bridge/README.md).
