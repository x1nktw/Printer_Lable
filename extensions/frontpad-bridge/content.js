/**
 * Isolated world — relay page messages to the service worker.
 */
(function () {
  "use strict";

  const KEEP_ALIVE_MS = 20000;
  const isTopFrame = window === window.top;
  let keepAliveTimer = null;

  function relay(payload, expectResponse) {
    try {
      if (expectResponse) {
        chrome.runtime.sendMessage(payload, () => {
          void chrome.runtime.lastError;
        });
        return;
      }

      // Fire-and-forget: never call .catch on a non-Promise return value.
      chrome.runtime.sendMessage(payload, () => {
        void chrome.runtime.lastError;
      });
    } catch {
      // Extension context invalidated (reload).
    }
  }

  function notifyHookAlive() {
    if (!isTopFrame) return;
    relay({ type: "frontpad-hook-ready", href: location.href });
  }

  function notifyHookGone() {
    if (!isTopFrame) return;
    relay({ type: "frontpad-hook-gone", href: location.href });
  }

  function startKeepAlive() {
    if (!isTopFrame || keepAliveTimer) return;
    keepAliveTimer = setInterval(() => {
      if (document.visibilityState === "hidden") return;
      notifyHookAlive();
    }, KEEP_ALIVE_MS);
  }

  function stopKeepAlive() {
    if (!keepAliveTimer) return;
    clearInterval(keepAliveTimer);
    keepAliveTimer = null;
  }

  window.addEventListener("message", (event) => {
    if (event.source !== window) return;
    const data = event.data;
    if (!data || data.source !== "labelprint-frontpad-bridge") return;

    if (data.type === "hook-ready") {
      notifyHookAlive();
      startKeepAlive();
      return;
    }

    if (data.type === "order-seen") {
      relay({
        type: "frontpad-order-seen",
        url: data.url,
        phase: data.phase,
        bodyLen: data.bodyLen
      });
      return;
    }

    if (data.type === "order-captured") {
      relay({
        type: "frontpad-order-captured",
        url: data.url,
        requestBody: data.requestBody,
        responseBody: data.responseBody,
        note: data.note
      });
    }
  });

  if (isTopFrame) {
    document.addEventListener("visibilitychange", () => {
      if (document.visibilityState === "hidden") return;
      notifyHookAlive();
    });

    window.addEventListener("pagehide", () => {
      stopKeepAlive();
      notifyHookGone();
    });

    relay({ type: "frontpad-content-loaded", href: location.href });
    startKeepAlive();
  }
})();
