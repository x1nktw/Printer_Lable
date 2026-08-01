/* global LabelPrintFrontPadParse */
"use strict";

importScripts("parse-order.js");

const DEFAULTS = {
  webhookUrl: "http://127.0.0.1:8765/",
  enabled: true,
  lastStatus: "Ожидание заказов FrontPad…",
  lastOrderNumber: null,
  lastError: null,
  lastDebug: null,
  sentCount: 0,
  hookSeenAt: null,
  hookHref: null
};

const recentOrderIds = new Map();
const DEDUP_MS = 5 * 60 * 1000;

async function getSettings() {
  const stored = await chrome.storage.local.get(DEFAULTS);
  return { ...DEFAULTS, ...stored };
}

async function setStatus(patch) {
  const current = await getSettings();
  const next = { ...current, ...patch };
  await chrome.storage.local.set(next);
  return next;
}

function isDuplicate(orderId) {
  const now = Date.now();
  for (const [id, ts] of recentOrderIds) {
    if (now - ts > DEDUP_MS) recentOrderIds.delete(id);
  }
  if (recentOrderIds.has(orderId)) return true;
  recentOrderIds.set(orderId, now);
  return false;
}

function clip(text, max) {
  if (text == null) return "";
  const s = String(text);
  return s.length <= max ? s : s.slice(0, max) + "…";
}

async function postToWebhook(webhookUrl, order) {
  const url = webhookUrl.endsWith("/") ? webhookUrl : `${webhookUrl}/`;
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json; charset=utf-8" },
    body: JSON.stringify(order)
  });
  const text = await response.text().catch(() => "");
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: ${text || response.statusText}`);
  }
  return text;
}

async function handleCapture(message) {
  const settings = await getSettings();
  if (!settings.enabled) {
    await setStatus({ lastStatus: "Пауза: мост выключен", lastError: null });
    return { ok: false, skipped: true };
  }

  await setStatus({
    lastDebug: `capture ${message.note || ""} url=${clip(message.url, 120)} reqLen=${(message.requestBody || "").length} resLen=${(message.responseBody || "").length}`
  });

  const built = LabelPrintFrontPadParse.buildLabelPrintOrder(
    message.requestBody,
    message.responseBody
  );

  if (!built.ok) {
    await setStatus({
      lastStatus: `Пропуск: ${built.error}`,
      lastError: built.error,
      lastDebug: `req=${clip(message.requestBody, 200)} | res=${clip(message.responseBody, 200)}`
    });
    return { ok: false, error: built.error };
  }

  const order = built.order;
  if (isDuplicate(order.externalOrderId)) {
    await setStatus({
      lastStatus: `Дубль №${order.number} (уже отправляли)`,
      lastError: null
    });
    return { ok: true, duplicate: true };
  }

  try {
    await postToWebhook(settings.webhookUrl, order);
    const sentCount = (settings.sentCount || 0) + 1;
    await setStatus({
      lastStatus: `Отправлен заказ №${order.number} (${order.items.length} поз.)`,
      lastOrderNumber: order.number,
      lastError: null,
      sentCount
    });
    return { ok: true, order };
  } catch (err) {
    recentOrderIds.delete(order.externalOrderId);
    const error = err && err.message ? err.message : String(err);
    await setStatus({
      lastStatus: `Ошибка отправки №${order.number}`,
      lastError: error + " — проверьте, что LabelPrint запущен и webhook = " + settings.webhookUrl
    });
    return { ok: false, error };
  }
}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (!message || !message.type) return false;

  if (message.type === "frontpad-hook-ready" || message.type === "frontpad-content-loaded") {
    setStatus({
      hookSeenAt: new Date().toISOString(),
      hookHref: message.href || null,
      lastStatus: "Хук на странице FrontPad активен. Сохраните заказ.",
      lastError: null
    }).then(() => sendResponse({ ok: true }));
    return true;
  }

  if (message.type === "frontpad-order-seen") {
    setStatus({
      lastDebug: `seen ${message.phase}: ${clip(message.url, 160)} bodyLen=${message.bodyLen}`,
      lastStatus: "Вижу order.php — жду ответ…"
    }).then(() => sendResponse({ ok: true }));
    return true;
  }

  if (message.type === "frontpad-order-captured") {
    handleCapture(message).then(sendResponse);
    return true;
  }

  if (message.type === "test-webhook") {
    (async () => {
      const settings = await getSettings();
      const sample = {
        externalOrderId: "bridge-test-" + Date.now(),
        number: "TEST",
        customerName: "Bridge test",
        comment: "Проверка webhook из расширения",
        statusCode: "new",
        orderedAt: new Date().toISOString(),
        items: [{ sku: "test", name: "Тестовая позиция", quantity: 1, price: 1 }]
      };
      try {
        await postToWebhook(settings.webhookUrl, sample);
        await setStatus({
          lastStatus: "Тест webhook OK — смотрите Заказы → Inbox / Синхронизировать",
          lastError: null
        });
        sendResponse({ ok: true });
      } catch (err) {
        const error = err && err.message ? err.message : String(err);
        await setStatus({
          lastStatus: "Тест webhook не прошёл",
          lastError: error
        });
        sendResponse({ ok: false, error });
      }
    })();
    return true;
  }

  return false;
});

chrome.runtime.onInstalled.addListener(async () => {
  const current = await chrome.storage.local.get(null);
  if (!current.webhookUrl) {
    await chrome.storage.local.set(DEFAULTS);
  }
});
