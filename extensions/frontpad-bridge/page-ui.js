/**
 * FrontPad page UI: LP toolbar button + connection modal.
 *
 * Placement: li.menu_user after #fr_status (float:right → left of ФР).
 */
(function () {
  "use strict";

  if (window !== window.top) return;
  if (window.__lpFpUiInstalled) return;
  window.__lpFpUiInstalled = true;

  const BTN_ID = "lp-fp-btn";
  const OVERLAY_ID = "lp-fp-overlay";
  const STYLE_ID = "lp-fp-page-ui-style";
  const COLOR_OK = "#6AC579";
  const COLOR_BAD = "#E74C3C";
  const HEARTBEAT_FRESH_MS = 120000;

  let connected = false;
  let svgMarkup = null;
  let injectTimer = null;
  let observer = null;
  let statusTimer = null;

  function extAlive() {
    try {
      return !!(chrome && chrome.runtime && chrome.runtime.id);
    } catch {
      return false;
    }
  }

  function storageGet(defaults) {
    return new Promise((resolve) => {
      const fallback = defaults || {};
      if (!extAlive()) {
        resolve(fallback);
        return;
      }
      try {
        chrome.storage.local.get(fallback, (data) => {
          try {
            if (chrome.runtime.lastError) {
              resolve(fallback);
              return;
            }
            resolve(data || fallback);
          } catch {
            resolve(fallback);
          }
        });
      } catch {
        resolve(fallback);
      }
    });
  }

  function storageSet(patch) {
    return new Promise((resolve) => {
      if (!extAlive()) {
        resolve(false);
        return;
      }
      try {
        chrome.storage.local.set(patch, () => {
          void chrome.runtime.lastError;
          resolve(true);
        });
      } catch {
        resolve(false);
      }
    });
  }

  function sendRuntime(message) {
    return new Promise((resolve) => {
      if (!extAlive()) {
        resolve(null);
        return;
      }
      try {
        chrome.runtime.sendMessage(message, (res) => {
          void chrome.runtime.lastError;
          resolve(res || null);
        });
      } catch {
        resolve(null);
      }
    });
  }

  function ensureCss() {
    if (document.getElementById(STYLE_ID)) return;
    try {
      if (!extAlive()) return;
      const link = document.createElement("link");
      link.id = STYLE_ID;
      link.rel = "stylesheet";
      link.href = chrome.runtime.getURL("page-ui.css");
      (document.head || document.documentElement).appendChild(link);
    } catch {
      // ignore — modal has inline-critical fallbacks via page-ui.css when loaded
    }
  }

  async function loadSvg() {
    if (svgMarkup) return svgMarkup;
    try {
      if (extAlive()) {
        const res = await fetch(chrome.runtime.getURL("icons/printer.svg"));
        svgMarkup = await res.text();
      }
    } catch {
      svgMarkup = null;
    }
    if (!svgMarkup) {
      svgMarkup =
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor"><rect x="7" y="3" width="10" height="5"/><rect x="3" y="8" width="18" height="8"/><rect x="8" y="17" width="8" height="4"/><rect x="8" y="11" width="8" height="2"/></svg>';
    }
    return svgMarkup;
  }

  function applyConnectedState(ok) {
    connected = !!ok;
    const btn = document.getElementById(BTN_ID);
    if (btn) {
      btn.classList.toggle("lp-fp-btn--ok", connected);
      const icon = btn.querySelector(".lp-fp-im");
      if (icon) icon.style.color = connected ? COLOR_OK : COLOR_BAD;
    }
    updateModalStatus();
  }

  function computeConnected(data) {
    if (!data) return false;
    if (data.lastHeartbeatError) return false;
    if (!data.lastHeartbeatAt) return false;
    const age = Date.now() - new Date(data.lastHeartbeatAt).getTime();
    return Number.isFinite(age) && age >= 0 && age <= HEARTBEAT_FRESH_MS;
  }

  function refreshConnectionFromStorage() {
    storageGet({ lastHeartbeatAt: null, lastHeartbeatError: null }).then((data) => {
      applyConnectedState(computeConnected(data));
    });
  }

  function parseWebhook(url) {
    try {
      const u = new URL(url || "http://127.0.0.1:8765/");
      return {
        ip: u.hostname || "127.0.0.1",
        port: u.port || (u.protocol === "https:" ? "443" : "80")
      };
    } catch {
      return { ip: "127.0.0.1", port: "8765" };
    }
  }

  function buildWebhook(ip, port) {
    const host = (ip || "127.0.0.1").trim() || "127.0.0.1";
    const p = String(port || "8765").trim() || "8765";
    return `http://${host}:${p}/`;
  }

  function updateModalStatus() {
    const statusEl = document.getElementById("lp-modal-status");
    if (!statusEl) return;
    statusEl.textContent = connected ? "Подключено" : "Не подключено";
    statusEl.classList.toggle("ok", connected);
  }

  function ensureModal() {
    let overlay = document.getElementById(OVERLAY_ID);
    if (overlay) return overlay;

    overlay = document.createElement("div");
    overlay.id = OVERLAY_ID;
    overlay.setAttribute("data-lp-keep-bg", "1");
    overlay.hidden = true;
    overlay.innerHTML =
      '<div id="lp-fp-modal" role="dialog" aria-modal="true" aria-labelledby="lp-modal-title">' +
      '<h2 id="lp-modal-title">Подключение LabelPrint</h2>' +
      '<div class="lp-sec"><div class="lp-sec__title">Настройки</div>' +
      '<div class="lp-row">' +
      '<div class="lp-field"><label for="lp-ip">IP</label>' +
      '<input id="lp-ip" type="text" spellcheck="false" autocomplete="off" value="127.0.0.1" /></div>' +
      '<div class="lp-field lp-field--port"><label for="lp-port">Port</label>' +
      '<input id="lp-port" type="text" spellcheck="false" autocomplete="off" value="8765" /></div>' +
      "</div>" +
      '<div class="lp-actions">' +
      '<label class="lp-check"><input id="lp-auto" type="checkbox" checked /> Подключать автоматически</label>' +
      '<button type="button" class="lp-btn-green" id="lp-save">Сохранить</button>' +
      "</div>" +
      '<div class="lp-msg" id="lp-save-msg"></div></div>' +
      '<div class="lp-sec"><div class="lp-sec__title">Состояние</div>' +
      '<div class="lp-status-block"><div class="lp-status-text">' +
      "<strong>LabelPrint</strong>" +
      '<span id="lp-modal-status">Не подключено</span></div>' +
      '<button type="button" class="lp-btn-green" id="lp-connect">Подключить</button>' +
      "</div></div>" +
      '<div class="lp-sec"><div class="lp-sec__title">Дополнительно</div>' +
      '<div class="lp-actions">' +
      '<button type="button" id="lp-test">Тест webhook</button>' +
      '<label class="lp-check"><input id="lp-dark" type="checkbox" /> Тёмная тема FrontPad</label>' +
      "</div>" +
      '<div class="lp-msg" id="lp-extra-msg"></div></div>' +
      '<div class="lp-footer"><button type="button" id="lp-close">Закрыть</button></div>' +
      "</div>";

    overlay.addEventListener("click", (e) => {
      if (e.target === overlay) closeModal();
    });

    document.addEventListener(
      "keydown",
      (e) => {
        if (e.key === "Escape") {
          const el = document.getElementById(OVERLAY_ID);
          if (el && !el.hidden) closeModal();
        }
      },
      true
    );

    overlay.querySelector("#lp-close").addEventListener("click", closeModal);
    overlay.querySelector("#lp-save").addEventListener("click", () => {
      void onSave();
    });
    overlay.querySelector("#lp-connect").addEventListener("click", onConnect);
    overlay.querySelector("#lp-test").addEventListener("click", onTest);
    overlay.querySelector("#lp-dark").addEventListener("change", (e) => {
      void storageSet({ darkTheme: !!e.target.checked }).then(() => {
        const msg = document.getElementById("lp-extra-msg");
        if (msg) {
          msg.className = "lp-msg";
          msg.textContent = e.target.checked
            ? "Тёмная тема включена."
            : "Тёмная тема выключена.";
        }
      });
    });

    (document.body || document.documentElement).appendChild(overlay);
    return overlay;
  }

  async function fillModalFromStorage() {
    const data = await storageGet({
      webhookUrl: "http://127.0.0.1:8765/",
      autoConnect: true,
      darkTheme: false,
      lastHeartbeatAt: null,
      lastHeartbeatError: null
    });

    const parsed = parseWebhook(data.webhookUrl);
    const ip = document.getElementById("lp-ip");
    const port = document.getElementById("lp-port");
    const auto = document.getElementById("lp-auto");
    const dark = document.getElementById("lp-dark");
    if (ip) ip.value = parsed.ip;
    if (port) port.value = !parsed.port || parsed.port === "80" ? "8765" : parsed.port;
    if (auto) auto.checked = data.autoConnect !== false;
    if (dark) dark.checked = !!data.darkTheme;
    applyConnectedState(computeConnected(data));
  }

  function openModal() {
    try {
      ensureCss();
    } catch {
      // ignore
    }
    const overlay = ensureModal();
    // Show first — storage must not block the UI.
    overlay.hidden = false;
    void fillModalFromStorage();
  }

  function closeModal() {
    const overlay = document.getElementById(OVERLAY_ID);
    if (overlay) overlay.hidden = true;
  }

  async function onSave() {
    const ip = document.getElementById("lp-ip") && document.getElementById("lp-ip").value;
    const port = document.getElementById("lp-port") && document.getElementById("lp-port").value;
    const autoEl = document.getElementById("lp-auto");
    const auto = !!(autoEl && autoEl.checked);
    const webhookUrl = buildWebhook(ip, port);
    const msg = document.getElementById("lp-save-msg");

    await storageSet({ webhookUrl, autoConnect: auto });

    if (msg) {
      msg.className = "lp-msg";
      msg.textContent = "Сохранено.";
    }

    await sendRuntime({ type: "ping-heartbeat" });
    setTimeout(refreshConnectionFromStorage, 400);
  }

  function onConnect() {
    const msg = document.getElementById("lp-save-msg");
    if (msg) {
      msg.className = "lp-msg";
      msg.textContent = "Подключение…";
    }
    void sendRuntime({ type: "ping-heartbeat" }).then(() => {
      setTimeout(() => {
        void storageGet({ lastHeartbeatError: null, lastHeartbeatAt: null }).then((data) => {
          refreshConnectionFromStorage();
          if (!msg) return;
          if (computeConnected(data)) {
            msg.className = "lp-msg";
            msg.textContent = "Подключено.";
          } else {
            msg.className = "lp-msg err";
            msg.textContent =
              data.lastHeartbeatError ||
              "Не удалось подключиться. Запустите LabelPrint.";
          }
        });
      }, 500);
    });
  }

  function onTest() {
    const msg = document.getElementById("lp-extra-msg");
    if (msg) {
      msg.className = "lp-msg";
      msg.textContent = "Отправка теста…";
    }
    void sendRuntime({ type: "test-webhook" }).then((res) => {
      setTimeout(() => {
        void storageGet({ lastStatus: "", lastError: null }).then((data) => {
          if (!msg) return;
          if (res && res.ok) {
            msg.className = "lp-msg";
            msg.textContent = data.lastStatus || "Тест OK.";
          } else {
            msg.className = "lp-msg err";
            msg.textContent = data.lastError || "Тест не прошёл.";
          }
          refreshConnectionFromStorage();
        });
      }, 300);
    });
  }

  function findMenuUl() {
    return (
      document.querySelector("div.top div.menu > ul") ||
      document.querySelector("div.menu > ul")
    );
  }

  async function createButton() {
    ensureCss();
    const svg = await loadSvg();
    const li = document.createElement("li");
    li.id = BTN_ID;
    li.className = "menu_user";
    li.setAttribute("data-lp-keep-bg", "1");
    li.title = "LabelPrint Bridge";
    li.innerHTML =
      '<div class="im lp-fp-im" aria-hidden="true">' + svg + "</div>LP";
    return li;
  }

  function isCorrectlyPlaced(btn, ul) {
    if (!btn || !ul || btn.parentElement !== ul) return false;
    const fr = document.getElementById("fr_status");
    if (fr && fr.parentElement === ul) {
      return fr.nextElementSibling === btn;
    }
    return btn === ul.lastElementChild;
  }

  async function injectButton() {
    const ul = findMenuUl();
    if (!ul) return false;

    let btn = document.getElementById(BTN_ID);
    if (btn && isCorrectlyPlaced(btn, ul)) {
      return true;
    }

    if (!btn) {
      btn = await createButton();
    } else if (btn.parentElement) {
      btn.parentElement.removeChild(btn);
    }

    if (btn.tagName !== "LI" || !btn.classList.contains("menu_user")) {
      const fresh = await createButton();
      if (btn.parentElement) btn.parentElement.removeChild(btn);
      btn = fresh;
    }

    const fr = document.getElementById("fr_status");
    if (fr && fr.parentElement === ul) {
      if (fr.nextSibling) ul.insertBefore(btn, fr.nextSibling);
      else ul.appendChild(btn);
    } else {
      ul.appendChild(btn);
    }

    applyConnectedState(connected);
    return true;
  }

  function scheduleInject() {
    if (injectTimer) return;
    injectTimer = setTimeout(() => {
      injectTimer = null;
      void injectButton().catch(() => {});
    }, 80);
  }

  function startObserver() {
    if (observer) return;

    const attach = () => {
      const ul = findMenuUl();
      if (!ul) {
        scheduleInject();
        return false;
      }
      if (observer) observer.disconnect();
      observer = new MutationObserver(() => scheduleInject());
      observer.observe(ul, { childList: true });
      scheduleInject();
      return true;
    };

    if (!attach()) {
      const boot = new MutationObserver(() => {
        if (attach()) boot.disconnect();
      });
      boot.observe(document.documentElement, { childList: true, subtree: true });
    }
  }

  // Event delegation — survives button re-inserts.
  document.addEventListener(
    "click",
    (e) => {
      const t = e.target && e.target.closest ? e.target.closest("#" + BTN_ID) : null;
      if (!t) return;
      e.preventDefault();
      e.stopPropagation();
      openModal();
    },
    true
  );

  try {
    ensureCss();
  } catch {
    // ignore
  }
  startObserver();

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", scheduleInject, { once: true });
  } else {
    scheduleInject();
  }

  setTimeout(scheduleInject, 800);
  setTimeout(scheduleInject, 2500);
  setTimeout(refreshConnectionFromStorage, 500);

  try {
    if (extAlive() && chrome.storage && chrome.storage.onChanged) {
      chrome.storage.onChanged.addListener((changes, area) => {
        if (area !== "local") return;
        if (
          changes.lastHeartbeatAt ||
          changes.lastHeartbeatError ||
          changes.webhookUrl
        ) {
          refreshConnectionFromStorage();
        }
      });
    }
  } catch {
    // ignore
  }

  statusTimer = setInterval(() => {
    if (document.visibilityState === "hidden") return;
    if (!extAlive()) return;
    void storageGet({ autoConnect: true }).then((data) => {
      if (data.autoConnect === false) {
        refreshConnectionFromStorage();
        return;
      }
      void sendRuntime({ type: "ping-heartbeat" }).then(() => {
        setTimeout(refreshConnectionFromStorage, 400);
      });
    });
  }, 30000);

  void statusTimer;
})();
