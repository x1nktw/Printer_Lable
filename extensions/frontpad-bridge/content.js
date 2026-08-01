/**
 * Isolated world — relay page messages to the service worker.
 */
(function () {
  "use strict";

  function relay(payload) {
    try {
      chrome.runtime.sendMessage(payload).catch(() => {});
    } catch {
      // extension reloaded
    }
  }

  window.addEventListener("message", (event) => {
    if (event.source !== window) return;
    const data = event.data;
    if (!data || data.source !== "labelprint-frontpad-bridge") return;

    if (data.type === "hook-ready") {
      relay({ type: "frontpad-hook-ready", href: data.href });
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

  // Ping so popup can show isolated script is loaded even before MAIN posts
  relay({ type: "frontpad-content-loaded", href: location.href });
})();
