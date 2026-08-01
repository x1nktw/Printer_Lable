"use strict";

async function load() {
  const data = await chrome.storage.local.get({
    enabled: true,
    lastStatus: "—",
    lastOrderNumber: null,
    lastError: null,
    lastDebug: null,
    sentCount: 0,
    webhookUrl: "http://127.0.0.1:8765/",
    hookSeenAt: null,
    hookHref: null
  });

  document.getElementById("status").textContent = data.lastStatus || "—";
  document.getElementById("error").textContent = data.lastError || "";
  document.getElementById("debug").textContent = data.lastDebug || "";
  document.getElementById("enabled").checked = !!data.enabled;

  let meta = `Webhook: ${data.webhookUrl} · отправлено: ${data.sentCount || 0}`;
  if (data.lastOrderNumber) meta += ` · последний №${data.lastOrderNumber}`;
  if (data.hookSeenAt) meta += `\nХук: ${data.hookSeenAt}`;
  if (data.hookHref) meta += `\n${data.hookHref}`;
  document.getElementById("meta").textContent = meta;
}

document.getElementById("enabled").addEventListener("change", async (e) => {
  await chrome.storage.local.set({
    enabled: e.target.checked,
    lastStatus: e.target.checked ? "Мост включён" : "Пауза: мост выключен",
    lastError: null
  });
  await load();
});

document.getElementById("options").addEventListener("click", () => {
  chrome.runtime.openOptionsPage();
});

document.getElementById("refresh").addEventListener("click", load);

document.getElementById("test").addEventListener("click", () => {
  chrome.runtime.sendMessage({ type: "test-webhook" }, () => load());
});

load();
chrome.storage.onChanged.addListener(load);
