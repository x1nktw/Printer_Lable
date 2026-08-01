/**
 * MAIN world — hooks XHR/fetch before FrontPad scripts (document_start).
 */
(function () {
  "use strict";

  if (window.__labelPrintFrontPadBridgeInjected) return;
  window.__labelPrintFrontPadBridgeInjected = true;

  // order.php, /order.php?x=, .../ajax/order.php, occasional order_save.php
  const ORDER_RE = /order(\.php|_save|\.php\/)|\/order(?:\?|$)/i;

  function post(type, extra) {
    try {
      window.postMessage(
        Object.assign(
          {
            source: "labelprint-frontpad-bridge",
            type
          },
          extra || {}
        ),
        "*"
      );
    } catch {
      // ignore
    }
  }

  function emit(rawRequest, rawResponse, url, note) {
    post("order-captured", {
      url: String(url || ""),
      requestBody: typeof rawRequest === "string" ? rawRequest : rawRequest == null ? null : String(rawRequest),
      responseBody: typeof rawResponse === "string" ? rawResponse : String(rawResponse ?? ""),
      note: note || null
    });
  }

  function bodyToString(body) {
    if (body == null) return "";
    if (typeof body === "string") return body;
    if (typeof URLSearchParams !== "undefined" && body instanceof URLSearchParams) {
      return body.toString();
    }
    if (typeof FormData !== "undefined" && body instanceof FormData) {
      try {
        const params = new URLSearchParams();
        body.forEach((value, key) => {
          if (typeof value === "string") params.append(key, value);
          else if (value && typeof value.name === "string") params.append(key, value.name);
        });
        return params.toString();
      } catch {
        return "";
      }
    }
    if (typeof ArrayBuffer !== "undefined" && body instanceof ArrayBuffer) {
      try {
        return new TextDecoder("utf-8").decode(body);
      } catch {
        return "";
      }
    }
    if (typeof Blob !== "undefined" && body instanceof Blob) {
      return null; // need async — handled by caller
    }
    try {
      return JSON.stringify(body);
    } catch {
      return "";
    }
  }

  function looksLikeOrderUrl(url) {
    return !!url && ORDER_RE.test(String(url));
  }

  function looksLikeOrderResponse(text) {
    if (!text || text.length > 500000) return false;
    return /"order_id"\s*:/.test(text) && /"result"\s*:\s*"success"/i.test(text);
  }

  // Announce hook is alive (isolated content script relays → popup)
  post("hook-ready", { href: String(location.href || "") });

  // --- XHR ---
  const xhrOpen = XMLHttpRequest.prototype.open;
  const xhrSend = XMLHttpRequest.prototype.send;

  XMLHttpRequest.prototype.open = function (method, url) {
    try {
      this.__lpMethod = method;
      this.__lpUrl = typeof url === "string" ? url : String(url);
    } catch {
      // ignore
    }
    return xhrOpen.apply(this, arguments);
  };

  XMLHttpRequest.prototype.send = function (body) {
    const url = this.__lpUrl;
    const track = looksLikeOrderUrl(url);
    // Always keep body: URL may not match our regex, but response can still be an order save.
    const requestText = bodyToString(body) || "";
    this.__lpRequestText = requestText;

    if (track) {
      post("order-seen", { url: String(url), phase: "send", bodyLen: requestText.length });
    }

    const onDone = () => {
      try {
        if (this.readyState !== 4) return;
        const responseText = this.responseText || "";
        const interesting = track || looksLikeOrderResponse(responseText);
        if (!interesting) return;
        if (this.status >= 200 && this.status < 300) {
          emit(
            this.__lpRequestText || "",
            responseText,
            url,
            track ? "xhr-url" : "xhr-response-detect"
          );
        }
      } catch {
        // ignore
      }
    };

    this.addEventListener("readystatechange", onDone);
    return xhrSend.apply(this, arguments);
  };

  // Patch again after short delays in case page replaces XHR (rare)
  function rehook() {
    if (XMLHttpRequest.prototype.open !== xhrOpen && !XMLHttpRequest.prototype.__lpBridged) {
      // page replaced open — leave as-is if already our wrapper chain is lost; skip complex rewrap
      return;
    }
  }
  setTimeout(rehook, 0);
  setTimeout(rehook, 1000);

  // --- fetch ---
  const origFetch = window.fetch;
  if (typeof origFetch === "function") {
    window.fetch = function (input, init) {
      const url = typeof input === "string" ? input : input && input.url;
      const track = looksLikeOrderUrl(url);
      let requestText = track && init ? bodyToString(init.body) : "";

      if (track) {
        post("order-seen", { url: String(url), phase: "fetch", bodyLen: (requestText || "").length });
      }

      return origFetch.apply(this, arguments).then(function (response) {
        const interesting = track;
        if (!interesting && !track) {
          // clone only when URL matched; for fetch we don't scan every response
          return response;
        }
        const clone = response.clone();
        clone
          .text()
          .then(function (text) {
            if (response.ok && (track || looksLikeOrderResponse(text))) {
              emit(requestText || "", text, url, "fetch");
            }
          })
          .catch(function () { /* ignore */ });
        return response;
      });
    };
  }

  // eslint-disable-next-line no-console
  console.info("[LabelPrint Bridge] hook active on", location.href);
})();
