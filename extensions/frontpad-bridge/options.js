"use strict";

async function load() {
  const data = await chrome.storage.local.get({
    webhookUrl: "http://127.0.0.1:8765/",
    enabled: true
  });
  document.getElementById("webhookUrl").value = data.webhookUrl || "http://127.0.0.1:8765/";
  document.getElementById("enabled").checked = data.enabled !== false;
}

document.getElementById("save").addEventListener("click", async () => {
  const webhookUrl = document.getElementById("webhookUrl").value.trim() || "http://127.0.0.1:8765/";
  const enabled = document.getElementById("enabled").checked;
  await chrome.storage.local.set({ webhookUrl, enabled });
  document.getElementById("msg").textContent = "Сохранено.";
});

load();
