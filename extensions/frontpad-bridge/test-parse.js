/**
 * Quick parser smoke test (node). Run: node test-parse.js
 */
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const code = fs.readFileSync(path.join(__dirname, "parse-order.js"), "utf8");
const sandbox = { self: {}, console };
vm.runInNewContext(code, sandbox);
const { buildLabelPrintOrder, buildItems } = sandbox.self.LabelPrintFrontPadParse;

function assert(cond, msg, detail) {
  if (!cond) {
    console.error("FAIL", msg, detail ?? "");
    process.exit(1);
  }
}

// Two independent dishes (mod=1 only means "allows modifiers")
{
  const request = {
    total: 405,
    positions: {
      productID: [303984, 304024],
      name: "Классика,мини",
      kol: [1, 1],
      price: [230, 175],
      mod: [1, 0],
      parent: ["", ""]
    }
  };
  const response = {
    result: "success",
    order_id: 14320522,
    order_n: 65501,
    date: "01.08.2026",
    time: "14:06"
  };
  const built = buildLabelPrintOrder(JSON.stringify(request), JSON.stringify(response));
  assert(built.ok, "two dishes ok", built.error);
  assert(built.order.items.length === 2, "two dishes count", built.order.items);
  assert(built.order.items[0].name === "Классика", "name0", built.order.items[0]);
  assert(built.order.items[1].name === "мини", "name1", built.order.items[1]);
}

// Add-ons with parent "0" (must not be treated as falsy)
{
  const items = buildItems({
    productID: [303984, 118904, 118908],
    name: ["Классика", "Бекон", "Картофель"],
    kol: [1, 1, 1],
    price: [230, 70, 40],
    mod: [1, 0, 0],
    parent: ["", "0", "0"]
  });
  assert(items.length === 1, "addons merged count", items);
  assert(items[0].name === "Классика", "addons name", items[0]);
  assert(items[0].price === 340, "addons price sum", items[0]);
  assert(items[0].comment === "Бекон\nКартофель", "addons comment", items[0]);
  assert(Array.isArray(items[0].addons) && items[0].addons.join(",") === "Бекон,Картофель", "addons array", items[0]);
}

// Numeric parent 0
{
  const items = buildItems({
    productID: { 0: 10, 1: 20 },
    name: { 0: "Пицца", 1: "Сыр" },
    kol: { 0: 1, 1: 1 },
    price: { 0: 100, 1: 30 },
    parent: { 0: "", 1: 0 }
  });
  assert(items.length === 1, "numeric parent count", items);
  assert(items[0].name === "Пицца", "numeric parent name", items[0]);
  assert(items[0].price === 130, "numeric parent price", items[0]);
}

// Order 65535-style end-to-end
{
  const built = buildLabelPrintOrder(
    JSON.stringify({
      total: 340,
      positions: {
        productID: [303984, 118904, 118908],
        name: "Классика,Бекон,Картофель",
        kol: [1, 1, 1],
        price: [230, 70, 40],
        mod: [1, 0, 0],
        parent: ["", "0", "0"]
      }
    }),
    JSON.stringify({
      result: "success",
      order_id: 14328596,
      order_n: 65535,
      date: "01.08.2026",
      time: "15:43"
    })
  );
  assert(built.ok, "65535 ok", built.error);
  assert(built.order.number === "65535", "65535 number");
  assert(built.order.items.length === 1, "65535 items", built.order.items);
  assert(built.order.items[0].name === "Классика", "65535 name", built.order.items[0]);
  assert(built.order.items[0].comment === "Бекон\nКартофель", "65535 comment", built.order.items[0]);
}

console.log("OK");
process.exit(0);
