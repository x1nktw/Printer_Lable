"use strict";

async function load() {
  const data = await chrome.storage.local.get({
    enabled: true,
    darkTheme: false,
    lastStatus: "—",
    lastOrderNumber: null,
    lastError: null,
    lastDebug: null,
    sentCount: 0,
    webhookUrl: "http://127.0.0.1:8765/",
    hookSeenAt: null,
    hookHref: null,
    lastHeartbeatAt: null,
    lastHeartbeatError: null
  });

  const statusEl = document.getElementById("status");
  const errorEl = document.getElementById("error");
  const debugEl = document.getElementById("debug");
  const enabledEl = document.getElementById("enabled");
  const darkThemeEl = document.getElementById("darkTheme");
  const metaEl = document.getElementById("meta");
  if (!statusEl || !errorEl || !debugEl || !enabledEl || !darkThemeEl || !metaEl) return;

  statusEl.textContent = data.lastStatus || "—";
  errorEl.textContent = data.lastError || data.lastHeartbeatError || "";
  debugEl.textContent = data.lastDebug || "";
  enabledEl.checked = !!data.enabled;
  darkThemeEl.checked = !!data.darkTheme;

  let meta = `Webhook: ${data.webhookUrl} · отправлено: ${data.sentCount || 0}`;
  if (data.lastOrderNumber) meta += ` · последний №${data.lastOrderNumber}`;
  if (data.lastHeartbeatAt) meta += `\nHeartbeat: ${data.lastHeartbeatAt}`;
  else meta += `\nHeartbeat: ещё не был`;
  if (data.hookSeenAt) meta += `\nХук FrontPad: ${data.hookSeenAt}`;
  if (data.hookHref) meta += `\n${data.hookHref}`;
  metaEl.textContent = meta;
}

function pingHeartbeat(thenLoad) {
  try {
    chrome.runtime.sendMessage({ type: "ping-heartbeat" }, () => {
      void chrome.runtime.lastError;
      if (thenLoad) load();
    });
  } catch {
    if (thenLoad) load();
  }
}

document.getElementById("enabled").addEventListener("change", async (e) => {
  await chrome.storage.local.set({
    enabled: e.target.checked,
    lastStatus: e.target.checked ? "Мост включён" : "Пауза: мост выключен",
    lastError: null
  });
  pingHeartbeat(true);
});

document.getElementById("darkTheme").addEventListener("change", async (e) => {
  await chrome.storage.local.set({ darkTheme: e.target.checked });
});

document.getElementById("options").addEventListener("click", () => {
  chrome.runtime.openOptionsPage();
});

document.getElementById("refresh").addEventListener("click", () => {
  pingHeartbeat(true);
});

document.getElementById("test").addEventListener("click", () => {
  chrome.runtime.sendMessage({ type: "test-webhook" }, () => {
    void chrome.runtime.lastError;
    load();
  });
});

load();
pingHeartbeat(true);
chrome.storage.onChanged.addListener(() => {
  load();
});
