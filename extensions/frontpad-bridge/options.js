"use strict";

async function load() {
  const data = await chrome.storage.local.get({
    webhookUrl: "http://127.0.0.1:8765/",
    enabled: true,
    darkTheme: false
  });
  document.getElementById("webhookUrl").value = data.webhookUrl || "http://127.0.0.1:8765/";
  document.getElementById("enabled").checked = data.enabled !== false;
  document.getElementById("darkTheme").checked = !!data.darkTheme;
}

document.getElementById("save").addEventListener("click", async () => {
  const webhookUrl = document.getElementById("webhookUrl").value.trim() || "http://127.0.0.1:8765/";
  const enabled = document.getElementById("enabled").checked;
  const darkTheme = document.getElementById("darkTheme").checked;
  await chrome.storage.local.set({ webhookUrl, enabled, darkTheme });
  document.getElementById("msg").textContent = "Сохранено.";
});

document.getElementById("darkTheme").addEventListener("change", async (e) => {
  await chrome.storage.local.set({ darkTheme: e.target.checked });
  document.getElementById("msg").textContent = e.target.checked
    ? "Тёмная тема включена (на вкладках FrontPad)."
    : "Тёмная тема выключена.";
});

load();
