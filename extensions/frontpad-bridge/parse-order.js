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

  function splitNames(nameField, expectedCount) {
    if (Array.isArray(nameField)) return nameField.map((x) => String(x ?? "").trim());
    if (nameField && typeof nameField === "object") return toArray(nameField).map((x) => String(x ?? "").trim());
    const raw = String(nameField ?? "").trim();
    if (!raw) return Array.from({ length: expectedCount }, () => "Позиция");
    const parts = raw.split(",").map((s) => s.trim()).filter(Boolean);
    if (expectedCount > 0 && parts.length === expectedCount) return parts;
    if (expectedCount <= 1) return [raw];
    // Uneven split — keep whole string on first row, placeholders on rest
    const result = [raw];
    while (result.length < expectedCount) result.push(`Позиция ${result.length + 1}`);
    return result;
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
    const ids = toArray(positions.productID ?? positions.productId ?? positions.product_id);
    const count = Math.max(
      ids.length,
      toArray(positions.kol).length,
      toArray(positions.kol_val).length,
      toArray(positions.price).length
    );
    if (count === 0) return [];

    const names = splitNames(positions.name, count);
    const kol = toArray(positions.kol ?? positions.kol_val);
    const prices = toArray(positions.price);
    const parents = toArray(positions.parent ?? positions.product_mod ?? positions.productMod);

    const rows = [];
    for (let i = 0; i < count; i++) {
      const productId = ids[i] != null ? String(ids[i]) : null;
      const parentIndex = resolveParentIndex(parents[i], count, ids);
      rows.push({
        productId,
        name: names[i] || `Позиция ${i + 1}`,
        quantity: asNumber(kol[i], 1) || 1,
        price: prices[i] != null ? asNumber(prices[i], null) : null,
        parentIndex: parentIndex === i ? null : parentIndex
      });
    }

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

      const addons = addonsByRoot.get(i) || [];
      let name = rows[i].name;
      let price = rows[i].price;
      let comment = null;
      let addonNames = [];

      if (addons.length > 0) {
        addonNames = addons.map((a) => a.name).filter(Boolean);
        const priceParts = [rows[i].price, ...addons.map((a) => a.price)].filter((p) => p != null);
        if (priceParts.length > 0) {
          price = priceParts.reduce((sum, p) => sum + p, 0);
        }
        comment = addonNames.join("\n");
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

    return items;
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
