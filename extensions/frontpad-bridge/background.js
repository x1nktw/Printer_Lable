/* global LabelPrintFrontPadParse */
"use strict";

importScripts("parse-order.js");

const DEFAULTS = {
  webhookUrl: "http://127.0.0.1:8765/",
  enabled: true,
  darkTheme: false,
  lastStatus: "Ожидание заказов FrontPad…",
  lastOrderNumber: null,
  lastError: null,
  lastDebug: null,
  sentCount: 0,
  hookSeenAt: null,
  hookHref: null,
  lastHeartbeatAt: null,
  lastHeartbeatError: null
};

const recentOrderIds = new Map();
const DEDUP_MS = 5 * 60 * 1000;
const HEARTBEAT_ALARM = "labelprint-bridge-heartbeat";
const FRONT_PAD_HOOK_MS = 60 * 1000;

let heartbeatInFlight = null;
let heartbeatTimer = null;

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

function normalizeWebhookUrl(webhookUrl) {
  return webhookUrl.endsWith("/") ? webhookUrl : `${webhookUrl}/`;
}

async function postToWebhook(webhookUrl, payload) {
  const url = normalizeWebhookUrl(webhookUrl);
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json; charset=utf-8" },
    body: JSON.stringify(payload)
  });
  const text = await response.text().catch(() => "");
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: ${text || response.statusText}`);
  }
  return text;
}

function isFrontPadHookActive(settings) {
  if (!settings.hookSeenAt) return false;
  const age = Date.now() - new Date(settings.hookSeenAt).getTime();
  return Number.isFinite(age) && age >= 0 && age <= FRONT_PAD_HOOK_MS;
}

async function sendHeartbeat() {
  if (heartbeatInFlight) return heartbeatInFlight;

  heartbeatInFlight = (async () => {
    const settings = await getSettings();
    const hookActive = isFrontPadHookActive(settings);
    const base = normalizeWebhookUrl(settings.webhookUrl || "http://127.0.0.1:8765/");

    const qs = new URLSearchParams({
      bridge: "1",
      enabled: settings.enabled ? "1" : "0",
      frontPad: hookActive ? "1" : "0"
    });
    if (hookActive && settings.hookSeenAt) {
      qs.set("hookSeenAt", settings.hookSeenAt);
    }

    const candidates = webhookUrlCandidates(base);
    let lastError = "Failed to fetch";

    for (const candidate of candidates) {
      try {
        const getResponse = await fetch(`${candidate}?${qs.toString()}`, {
          method: "GET",
          cache: "no-store"
        });
        if (getResponse.ok) {
          await chrome.storage.local.set({
            lastHeartbeatAt: new Date().toISOString(),
            lastHeartbeatError: null,
            webhookUrl: candidate
          });
          return;
        }
        lastError = `HTTP ${getResponse.status}`;
      } catch (err) {
        lastError = err && err.message ? err.message : String(err);
      }

      try {
        const postResponse = await fetch(candidate, {
          method: "POST",
          headers: { "Content-Type": "application/json; charset=utf-8" },
          body: JSON.stringify({
            type: "bridge-heartbeat",
            enabled: !!settings.enabled,
            hookSeenAt: hookActive ? settings.hookSeenAt : null,
            frontPadHookActive: hookActive,
            sentAt: new Date().toISOString()
          })
        });
        if (postResponse.ok) {
          await chrome.storage.local.set({
            lastHeartbeatAt: new Date().toISOString(),
            lastHeartbeatError: null,
            webhookUrl: candidate
          });
          return;
        }
        lastError = `HTTP ${postResponse.status}`;
      } catch (err) {
        lastError = err && err.message ? err.message : String(err);
      }
    }

    await chrome.storage.local.set({
      lastHeartbeatError:
        `Heartbeat: ${lastError}. Запустите LabelPrint Pro и проверьте URL (${candidates.join(" | ")})`
    });
    console.warn("[LabelPrint Bridge] heartbeat failed", lastError, candidates);
  })();

  try {
    await heartbeatInFlight;
  } finally {
    heartbeatInFlight = null;
  }
}

function webhookUrlCandidates(base) {
  const list = [base];
  try {
    const uri = new URL(base);
    if (uri.hostname === "127.0.0.1") {
      uri.hostname = "localhost";
      list.push(uri.toString());
    } else if (uri.hostname === "localhost") {
      uri.hostname = "127.0.0.1";
      list.push(uri.toString());
    }
  } catch {
    // keep base only
  }
  return [...new Set(list.map(normalizeWebhookUrl))];
}

function scheduleHeartbeat(delayMs) {
  if (heartbeatTimer) clearTimeout(heartbeatTimer);
  heartbeatTimer = setTimeout(() => {
    heartbeatTimer = null;
    sendHeartbeat();
  }, delayMs);
}

function ensureHeartbeatAlarm() {
  try {
    chrome.alarms.create(HEARTBEAT_ALARM, { periodInMinutes: 1 });
  } catch (err) {
    console.warn("[LabelPrint Bridge] alarms.create failed", err);
  }
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
      lastDebug: `req=${clip(message.requestBody, 400)} | res=${clip(message.responseBody, 200)}`
    });
    return { ok: false, error: built.error };
  }

  const order = built.order;
  const itemSummary = (order.items || [])
    .map((it) => `${it.name}${it.addons && it.addons.length ? " +[" + it.addons.join("; ") + "]" : ""}`)
    .join(" | ");
  await setStatus({
    lastDebug: `order №${order.number} items=${order.items.length}: ${clip(itemSummary, 240)}`
  });
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
      lastDebug: `order №${order.number} items=${order.items.length}: ${clip(itemSummary, 240)}`,
      sentCount
    });
    scheduleHeartbeat(0);
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

  // Keepalive / unload: do not hold the message port open.
  if (message.type === "frontpad-hook-ready" || message.type === "frontpad-content-loaded") {
    setStatus({
      hookSeenAt: new Date().toISOString(),
      hookHref: message.href || null,
      lastStatus: "Хук на странице FrontPad активен. Сохраните заказ.",
      lastError: null
    })
      .then(() => scheduleHeartbeat(50))
      .catch(() => {});
    return false;
  }

  if (message.type === "frontpad-hook-gone") {
    setStatus({
      hookSeenAt: null,
      hookHref: null,
      lastStatus: "Вкладка FrontPad закрыта",
      lastError: null
    })
      .then(() => scheduleHeartbeat(50))
      .catch(() => {});
    return false;
  }

  if (message.type === "frontpad-order-seen") {
    setStatus({
      lastDebug: `seen ${message.phase}: ${clip(message.url, 160)} bodyLen=${message.bodyLen}`,
      lastStatus: "Вижу order.php — жду ответ…"
    }).catch(() => {});
    return false;
  }

  if (message.type === "frontpad-order-captured") {
    handleCapture(message)
      .then((result) => sendResponse(result))
      .catch((err) => sendResponse({ ok: false, error: String(err) }));
    return true;
  }

  if (message.type === "ping-heartbeat") {
    sendHeartbeat()
      .then(() => sendResponse({ ok: true }))
      .catch(() => sendResponse({ ok: false }));
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
        items: [{ sku: "test", name: "Тестовая позиция", quantity: 1, qty: 1 }]
      };
      try {
        await postToWebhook(settings.webhookUrl, sample);
        await setStatus({
          lastStatus: "Тест webhook OK — смотрите Заказы → Inbox / Синхронизировать",
          lastError: null
        });
        await sendHeartbeat();
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

chrome.alarms.onAlarm.addListener((alarm) => {
  if (alarm.name === HEARTBEAT_ALARM) {
    sendHeartbeat();
  }
});

// Only react to user/config changes — not to heartbeat timestamps (avoids loops).
chrome.storage.onChanged.addListener((changes, area) => {
  if (area !== "local") return;
  if (changes.enabled || changes.webhookUrl) {
    scheduleHeartbeat(100);
  }
});

chrome.runtime.onInstalled.addListener(async () => {
  try {
    const current = await chrome.storage.local.get(null);
    if (!current.webhookUrl) {
      await chrome.storage.local.set(DEFAULTS);
    }
    ensureHeartbeatAlarm();
    await sendHeartbeat();
  } catch (err) {
    console.warn("[LabelPrint Bridge] onInstalled failed", err);
  }
});

chrome.runtime.onStartup.addListener(() => {
  ensureHeartbeatAlarm();
  sendHeartbeat();
});

ensureHeartbeatAlarm();
sendHeartbeat();
