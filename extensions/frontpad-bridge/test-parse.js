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

// Regression: sparse name join + addon text with commas (order 14375204 style)
{
  const sparseName =
    ",,,Шаверма Классическая,,,,Овощи свежие. Капуста, томаты, огурцы.";
  const items = buildItems({
    productID: ["", "", "", "303001", "", "", "", "116705"],
    name: sparseName,
    kol: [1, 1, 1, 1, 1, 1, 1, 1],
    price: ["", "", "", 340, "", "", "", ""],
    parent: ["", "", "", "", "", "", "", "3"]
  });
  assert(items.length === 1, "sparse+comma addon count", items);
  assert(items[0].name === "Шаверма Классическая", "sparse dish name", items[0]);
  assert(
    items[0].addons && items[0].addons[0] === "Овощи свежие. Капуста, томаты, огурцы.",
    "sparse addon text",
    items[0]
  );
  assert(items[0].price === 340, "sparse price", items[0]);
}

// Same mangled string but wrong short count: must not invent Позиция 2..N labels
{
  const items = buildItems({
    productID: ["", "", "", "303001", "", "", "116705"],
    name: ",,,Шаверма Классическая,,,,Овощи свежие. Капуста, томаты, огурцы.",
    kol: [1, 1, 1, 1, 1, 1, 1],
    price: ["", "", "", 340, "", "", ""],
    parent: ["", "", "", "", "", "", "3"]
  });
  assert(items.length === 1, "short-count no placeholders", items);
  assert(items[0].name === "Шаверма Классическая", "short-count dish", items[0]);
  assert(
    items[0].addons.join("|") === "Овощи свежие. Капуста, томаты, огурцы.",
    "short-count addon",
    items[0]
  );
}

// Ghost empty slots without parent must not print
{
  const items = buildItems({
    productID: ["", "", "10", "", ""],
    name: ["", "", "Пицца", "", ""],
    kol: [1, 1, 1, 1, 1],
    price: ["", "", 100, "", ""],
    parent: ["", "", "", "", ""]
  });
  assert(items.length === 1, "ghost skip count", items);
  assert(items[0].name === "Пицца", "ghost skip name", items[0]);
}

// Sparse object names (indexed, not dense array)
{
  const items = buildItems({
    productID: { 0: 1, 1: 2 },
    name: { 0: "Блюдо", 1: "Овощи свежие. Капуста, томаты, огурцы." },
    kol: { 0: 1, 1: 1 },
    price: { 0: 340, 1: 0 },
    parent: { 0: "", 1: "0" }
  });
  assert(items.length === 1, "object names count", items);
  assert(items[0].addons[0].includes("Капуста, томаты"), "object names addon commas", items[0]);
}

// Regression: order 14375300 — addon commas became 3 labels
{
  const items = buildItems({
    productID: ["", "", "116705", "116706"],
    name: "Шаверма Классическая,Овощи свежие. Капуста, томаты, огурцы.",
    kol: [1, 1, 1, 1],
    price: ["", "", 340, ""],
    parent: ["", "", "", "2"]
  });
  assert(items.length === 1, "14375300 count", items);
  assert(items[0].name === "Шаверма Классическая", "14375300 dish", items[0]);
  assert(
    items[0].addons.join("|") === "Овощи свежие. Капуста, томаты, огурцы.",
    "14375300 addon rejoined",
    items[0]
  );
  assert(items[0].price === 340, "14375300 price", items[0]);
}

// Safe split: comma+space stays inside one name when count matches compact length wrongly under old logic
{
  const items = buildItems({
    productID: [10, 20],
    name: "Шаверма Классическая,Овощи свежие. Капуста, томаты, огурцы.",
    kol: [1, 1],
    price: [340, 0],
    parent: ["", "0"]
  });
  assert(items.length === 1, "comma-space split count", items);
  assert(items[0].name === "Шаверма Классическая", "comma-space dish", items[0]);
  assert(
    items[0].addons[0] === "Овощи свежие. Капуста, томаты, огурцы.",
    "comma-space addon",
    items[0]
  );
}

console.log("OK");
process.exit(0);
