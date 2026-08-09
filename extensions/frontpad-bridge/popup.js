"use strict";

const HEARTBEAT_FRESH_MS = 120000;

function computeConnected(data) {
  if (!data) return false;
  if (data.lastHeartbeatError) return false;
  if (!data.lastHeartbeatAt) return false;
  const age = Date.now() - new Date(data.lastHeartbeatAt).getTime();
  return Number.isFinite(age) && age >= 0 && age <= HEARTBEAT_FRESH_MS;
}

async function loadIcon() {
  const host = document.getElementById("previewIcon");
  if (!host) return;
  try {
    const res = await fetch(chrome.runtime.getURL("icons/printer.svg"));
    host.innerHTML = await res.text();
  } catch {
    host.textContent = "🖨";
  }
}

async function load() {
  const manifest = chrome.runtime.getManifest();
  const verEl = document.getElementById("version");
  if (verEl) verEl.textContent = "Версия " + (manifest.version || "");

  const data = await chrome.storage.local.get({
    lastHeartbeatAt: null,
    lastHeartbeatError: null,
    lastStatus: "",
    webhookUrl: "http://127.0.0.1:8765/"
  });

  const preview = document.getElementById("preview");
  const ok = computeConnected(data);
  if (preview) preview.classList.toggle("ok", ok);

  const foot = document.getElementById("foot");
  if (foot) {
    foot.textContent = ok
      ? "Соединение с LabelPrint установлено"
      : "Нет соединения — запустите LabelPrint и нажмите LP в FrontPad";
  }
}

loadIcon();
load();
chrome.storage.onChanged.addListener(() => {
  load();
});

try {
  chrome.runtime.sendMessage({ type: "ping-heartbeat" }, () => {
    void chrome.runtime.lastError;
    load();
  });
} catch {
  // ignore
}
