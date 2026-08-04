/**
 * Maps FrontPad order.php request + response into LabelPrint inbox JSON.
 * Shared by the service worker (importScripts).
 */
(function (global) {
  "use strict";

  function toArray(value) {
    if (value == null) return [];
    if (Array.isArray(value)) return value;
    if (typeof value === "object") {
      return Object.keys(value)
        .sort((a, b) => Number(a) - Number(b) || String(a).localeCompare(String(b)))
        .map((k) => value[k]);
    }
    return [value];
  }

  function asNumber(value, fallback) {
    if (value == null || value === "") return fallback;
    const n = Number(String(value).replace(",", "."));
    return Number.isFinite(n) ? n : fallback;
  }

  /** Max length for PHP-style arrays / sparse objects (`{3: "x"}` → 4). */
  function fieldLength(value) {
    if (value == null) return 0;
    if (Array.isArray(value)) return value.length;
    if (typeof value === "object") {
      const idxs = Object.keys(value)
        .map((k) => Number(k))
        .filter((n) => Number.isFinite(n) && n >= 0 && Number.isInteger(n));
      if (idxs.length === 0) return toArray(value).length;
      return Math.max(...idxs) + 1;
    }
    return 0;
  }

  function padNames(list, expectedCount) {
    const out = list.map((x) => String(x ?? "").trim());
    while (out.length < expectedCount) out.push("");
    return out.slice(0, Math.max(expectedCount, out.length));
  }

  /**
   * FrontPad joins position names with "," (no space): "Классика,Бекон".
   * Human lists inside one name use ", " (comma+space): "Капуста, томаты, огурцы".
   * Split only on commas that are NOT followed by whitespace.
   */
  function splitNameSeparators(raw) {
    return String(raw ?? "").split(/,(?!\s)/);
  }

  /**
   * FrontPad often sends positions.name as:
   * - real array / sparse object (preferred)
   * - comma-joined string "A,B,C" when names have no commas
   * - sparse join ",,,Dish,,,,Addon with, commas." (Array#toString)
   * Never treat ", " inside an addon as a position boundary.
   */
  function splitNames(nameField, expectedCount) {
    if (Array.isArray(nameField)) {
      return padNames(nameField, expectedCount);
    }
    if (nameField && typeof nameField === "object") {
      const len = Math.max(expectedCount, fieldLength(nameField));
      const densified = [];
      for (let i = 0; i < len; i++) {
        const v = nameField[i] ?? nameField[String(i)];
        densified.push(v == null ? "" : String(v).trim());
      }
      return densified;
    }

    const raw = String(nameField ?? "");
    if (!raw.trim()) {
      return Array.from({ length: expectedCount }, () => "");
    }
    if (expectedCount <= 1) {
      return [raw.trim()];
    }

    const parts = splitNameSeparators(raw);
    if (parts.length === expectedCount) {
      return parts.map((s) => s.trim());
    }

    // Classic compact join: "Классика,Бекон,Картофель" (no spaces after commas)
    const compact = parts.map((s) => s.trim()).filter((s) => s.length > 0);
    if (compact.length === expectedCount) {
      return compact;
    }

    if (parts.length < expectedCount) {
      // Prefer mapping compact names onto leading slots when join omitted empties
      if (compact.length > 0 && compact.length < expectedCount) {
        return padNames(compact, expectedCount);
      }
      return padNames(parts, expectedCount);
    }

    // Too many separators → extras belong inside the last slot.
    const head = parts.slice(0, expectedCount - 1).map((s) => s.trim());
    const tail = parts
      .slice(expectedCount - 1)
      .join(",")
      .replace(/^,+/, "")
      .trim();
    return [...head, tail];
  }

  function isPlaceholderName(name) {
    return /^Позиция(\s+\d+)?$/i.test(String(name ?? "").trim());
  }

  function hasProductId(productId) {
    if (productId == null) return false;
    const s = String(productId).trim();
    return s !== "" && s !== "0";
  }

  /** Ghost slot: no id, no price, placeholder/empty name — not a real line. */
  function isGhostRow(row) {
    if (row.parentIndex != null) return false;
    if (hasProductId(row.productId)) return false;
    if (row.price != null) return false;
    const name = String(row.name ?? "").trim();
    return name === "" || isPlaceholderName(name);
  }

  /** Modifier rows without parent → attach to nearest preceding dish/anchor. */
  function inferMissingParents(rows) {
    let lastAnchor = -1;
    for (let i = 0; i < rows.length; i++) {
      const row = rows[i];
      if (row.parentIndex != null) {
        const root = rootParentIndex(rows, row.parentIndex);
        if (root >= 0) lastAnchor = root;
        continue;
      }
      const isAnchor = hasProductId(row.productId) || row.price != null;
      if (isAnchor) {
        lastAnchor = i;
        continue;
      }
      const name = String(row.name ?? "").trim();
      if (!name || isPlaceholderName(name)) {
        continue;
      }
      if (lastAnchor >= 0) {
        row.parentIndex = lastAnchor;
      } else {
        lastAnchor = i;
      }
    }
  }

  /**
   * When comma-splitting shredded one dish into several roots but only one line
   * has price/productId — reassemble: first name = dish, the rest = one addon text.
   */
  function coalesceFragmentItems(items) {
    if (items.length <= 1) return items;

    const priced = items.filter((it) => it.price != null || hasProductId(it.externalProductId));
    if (priced.length !== 1) return items;

    const orphans = items.filter((it) => it.price == null && !hasProductId(it.externalProductId));
    if (orphans.length === 0) return items;
    // Only coalesce when every non-priced line looks like a name fragment (no own sku)
    if (orphans.length + priced.length !== items.length) return items;

    const main = priced[0];
    const addonBits = [];
    let dish = null;

    for (const it of items) {
      const n = String(it.name ?? "").trim();
      const isMain = it === main;
      if (!isMain) {
        if (!dish && n) dish = n;
        else if (n) addonBits.push(n);
        for (const a of it.addons || []) {
          const t = String(a ?? "").trim();
          if (t) addonBits.push(t);
        }
        continue;
      }
      // priced row: its name is often a shredded leftover ("томаты")
      if (n && !isPlaceholderName(n) && !/^Товар\s/i.test(n)) {
        if (!dish) dish = n;
        else addonBits.push(n);
      }
      for (const a of it.addons || []) {
        const t = String(a ?? "").trim();
        if (t) addonBits.push(t);
      }
    }

    if (!dish) dish = main.name || "Без названия";
    const addonText = addonBits.join(", ").replace(/\s+,/g, ",").trim();
    const addons = addonText ? [addonText] : [];

    return [{
      externalProductId: main.externalProductId,
      sku: main.sku ?? main.externalProductId,
      name: dish,
      quantity: main.quantity || 1,
      price: main.price,
      comment: addons.length ? addons.join("\n") : null,
      addons
    }];
  }

  /**
   * PHP/jQuery-style keys: positions[productID][0]=…, positions[name][]=…
   */
  function assignPath(root, pathParts, value) {
    let cur = root;
    for (let i = 0; i < pathParts.length; i++) {
      const part = pathParts[i];
      const last = i === pathParts.length - 1;
      const next = pathParts[i + 1];
      const partIsIndex = part === "" || /^\d+$/.test(part);

      if (last) {
        if (part === "") {
          if (!Array.isArray(cur)) return;
          cur.push(value);
          return;
        }
        if (Array.isArray(cur) && /^\d+$/.test(part)) {
          cur[Number(part)] = value;
          return;
        }
        if (Object.prototype.hasOwnProperty.call(cur, part) && cur[part] !== value) {
          if (!Array.isArray(cur[part])) cur[part] = [cur[part]];
          cur[part].push(value);
        } else {
          cur[part] = value;
        }
        return;
      }

      // Create container for next segment
      if (part === "") {
        // shouldn't appear mid-path often
        return;
      }

      const nextIsArray = next === "" || /^\d+$/.test(next);
      if (nextIsArray) {
        if (!Array.isArray(cur[part])) cur[part] = [];
        cur = cur[part];
        continue;
      }

      if (!cur[part] || typeof cur[part] !== "object" || Array.isArray(cur[part])) {
        cur[part] = {};
      }
      cur = cur[part];
    }
  }

  function parseNestedForm(params) {
    const root = {};
    for (const [key, value] of params.entries()) {
      const path = key.replace(/\]/g, "").split("[");
      assignPath(root, path, value);
    }
    return root;
  }

  function parseRequestBody(rawBody) {
    if (rawBody == null) return null;
    if (typeof rawBody === "object" && !(rawBody instanceof ArrayBuffer) && !(typeof Blob !== "undefined" && rawBody instanceof Blob)) {
      // Already an object (unlikely from XHR string path)
      return rawBody;
    }

    let text = "";
    if (typeof rawBody === "string") text = rawBody;
    else return null;

    text = text.trim();
    if (!text) return null;

    if (text.startsWith("{") || text.startsWith("[")) {
      try {
        return JSON.parse(text);
      } catch {
        // continue
      }
    }

    try {
      const params = new URLSearchParams(text);
      // Single JSON field variants
      for (const key of ["data", "order", "json", "payload"]) {
        if (params.has(key)) {
          const inner = params.get(key);
          if (inner && inner.trim().startsWith("{")) {
            try {
              return JSON.parse(inner);
            } catch {
              // ignore
            }
          }
        }
      }
      return parseNestedForm(params);
    } catch {
      return null;
    }
  }

  /**
   * FrontPad: positions.parent[i] = index (0-based) of the parent line for modifiers/add-ons.
   * Important: parent "0" is valid — must not use truthy checks.
   * positions.mod[i] = 1 means the product *allows* modifiers, not that the row is a modifier.
   */
  function resolveParentIndex(parentRaw, rowCount, ids) {
    if (parentRaw == null) return null;
    const s = String(parentRaw).trim();
    if (s === "") return null;

    const asNum = Number(s);
    if (Number.isFinite(asNum) && Number.isInteger(asNum) && asNum >= 0 && asNum < rowCount) {
      return asNum;
    }

    // Fallback: parent may be productID of the root line
    const byId = ids.findIndex((id) => id != null && String(id) === s);
    return byId >= 0 ? byId : null;
  }

  function rootParentIndex(rows, startIndex) {
    let p = startIndex;
    const seen = new Set();
    while (rows[p].parentIndex != null && !seen.has(p)) {
      seen.add(p);
      p = rows[p].parentIndex;
    }
    return p;
  }

  function buildItems(positions) {
    if (!positions || typeof positions !== "object") return [];
    const idRaw = positions.productID ?? positions.productId ?? positions.product_id;
    const ids = toArray(idRaw);
    const kol = toArray(positions.kol ?? positions.kol_val);
    const prices = toArray(positions.price);
    const parents = toArray(positions.parent ?? positions.product_mod ?? positions.productMod);

    const count = Math.max(
      fieldLength(idRaw),
      ids.length,
      fieldLength(positions.kol),
      fieldLength(positions.kol_val),
      kol.length,
      fieldLength(positions.price),
      prices.length,
      fieldLength(positions.parent),
      fieldLength(positions.product_mod),
      parents.length,
      fieldLength(positions.name)
    );
    if (count === 0) return [];

    const names = splitNames(positions.name, count);

    const rows = [];
    for (let i = 0; i < count; i++) {
      const productId = ids[i] != null && String(ids[i]).trim() !== "" ? String(ids[i]).trim() : null;
      const parentIndex = resolveParentIndex(parents[i], count, ids.map((id) => (id == null ? null : String(id))));
      const name = String(names[i] ?? "").trim();
      rows.push({
        productId,
        name,
        quantity: asNumber(kol[i], 1) || 1,
        price: prices[i] != null && String(prices[i]).trim() !== "" ? asNumber(prices[i], null) : null,
        parentIndex: parentIndex === i ? null : parentIndex
      });
    }

    inferMissingParents(rows);

    const addonsByRoot = new Map();
    for (let i = 0; i < rows.length; i++) {
      if (rows[i].parentIndex == null) continue;
      const root = rootParentIndex(rows, rows[i].parentIndex);
      if (root === i) continue;
      if (!addonsByRoot.has(root)) addonsByRoot.set(root, []);
      addonsByRoot.get(root).push(rows[i]);
    }

    const items = [];
    for (let i = 0; i < rows.length; i++) {
      if (rows[i].parentIndex != null) continue;
      if (isGhostRow(rows[i]) && !(addonsByRoot.get(i) || []).length) continue;

      const addons = addonsByRoot.get(i) || [];
      let name = rows[i].name;
      if (!name || isPlaceholderName(name)) {
        name = name || "";
      }
      let price = rows[i].price;
      let comment = null;
      let addonNames = [];

      if (addons.length > 0) {
        addonNames = addons
          .map((a) => a.name)
          .map((n) => String(n ?? "").trim())
          .filter((n) => n && !isPlaceholderName(n));
        const priceParts = [rows[i].price, ...addons.map((a) => a.price)].filter((p) => p != null);
        if (priceParts.length > 0) {
          price = priceParts.reduce((sum, p) => sum + p, 0);
        }
        comment = addonNames.length ? addonNames.join("\n") : null;
      }

      if ((!name || isPlaceholderName(name)) && addonNames.length === 0 && !hasProductId(rows[i].productId) && price == null) {
        continue;
      }

      if (isPlaceholderName(name)) {
        name = hasProductId(rows[i].productId) ? `Товар ${rows[i].productId}` : "Без названия";
      }

      items.push({
        externalProductId: rows[i].productId,
        sku: rows[i].productId,
        name,
        quantity: rows[i].quantity,
        price,
        comment,
        addons: addonNames
      });
    }

    return coalesceFragmentItems(items);
  }

  function buildAddress(order) {
    const parts = [order.street, order.home, order.pod && `под. ${order.pod}`, order.et && `эт. ${order.et}`, order.kvart && `кв. ${order.kvart}`]
      .map((x) => (x == null ? "" : String(x).trim()))
      .filter(Boolean);
    return parts.length ? parts.join(", ") : null;
  }

  function parseOrderedAt(response, request) {
    const date = response?.date || "";
    const time = response?.time || "";
    if (date && time) {
      // dd.MM.yyyy HH:mm — treat as local, append offset unknown → ISO-like local
      const m = /^(\d{2})\.(\d{2})\.(\d{4})$/.exec(String(date).trim());
      const t = String(time).trim();
      if (m) {
        const isoLocal = `${m[3]}-${m[2]}-${m[1]}T${t.length === 5 ? t + ":00" : t}`;
        const d = new Date(isoLocal);
        if (!Number.isNaN(d.getTime())) return d.toISOString();
      }
    }
    if (request?.datetime) {
      const d = new Date(request.datetime);
      if (!Number.isNaN(d.getTime())) return d.toISOString();
    }
    return new Date().toISOString();
  }

  /**
   * @param {string|object|null} requestBody
   * @param {string|object} responseBody
   * @returns {{ ok: boolean, order?: object, error?: string }}
   */
  function buildLabelPrintOrder(requestBody, responseBody) {
    let request = typeof requestBody === "string" || requestBody == null
      ? parseRequestBody(requestBody)
      : requestBody;
    let response = responseBody;
    if (typeof response === "string") {
      try {
        response = JSON.parse(response);
      } catch {
        return { ok: false, error: "Response is not JSON" };
      }
    }

    if (!response || String(response.result).toLowerCase() !== "success") {
      return { ok: false, error: "Response result is not success" };
    }

    const orderId = response.order_id ?? response.orderId;
    const orderN = response.order_n ?? response.orderN ?? orderId;
    if (orderId == null || orderId === "") {
      return { ok: false, error: "Missing order_id in response" };
    }

    // Some FrontPad posts wrap fields; unwrap common containers
    if (request && request.positions == null && request.order && request.order.positions) {
      request = request.order;
    }

    const items = buildItems(request?.positions);
    if (items.length === 0) {
      return { ok: false, error: "No positions in request" };
    }

    const customerName = (request?.name && String(request.name).trim())
      || (request?.table && `Стол ${request.table}`)
      || "";
    const commentParts = [];
    if (request?.descr) commentParts.push(String(request.descr));
    if (request?.tags) commentParts.push(String(request.tags));

    const order = {
      externalOrderId: String(orderId),
      number: String(orderN),
      customerName: customerName || null,
      customerPhone: request?.phone ? String(request.phone) : null,
      comment: commentParts.length ? commentParts.join("; ") : null,
      address: buildAddress(request || {}),
      employee: request?.waiter ? String(request.waiter) : null,
      totalAmount: request?.total != null ? asNumber(request.total, null) : null,
      orderedAt: parseOrderedAt(response, request),
      statusCode: "new",
      items
    };

    return { ok: true, order };
  }

  global.LabelPrintFrontPadParse = {
    parseRequestBody,
    buildLabelPrintOrder,
    buildItems
  };
})(typeof self !== "undefined" ? self : globalThis);
