/**
 * Selective FrontPad dark theme (Claude warm palette).
 * No flash cover — sync inline paint + CSS white overrides only.
 */
(function () {
  "use strict";

  const CLASS_NAME = "lp-fp-dark";
  const STYLE_ID = "lp-fp-dark-style";
  const LS_KEY = "lp-fp-dark";
  const KEEP_ATTR = "data-lp-keep-bg";
  const SURFACE_ATTR = "data-lp-dark-surface";
  const ORIG_BGCOLOR = "data-lp-orig-bgcolor";
  const ORIG_STYLE_BG = "data-lp-orig-style-bg";
  const ORIG_KEEP_BG = "data-lp-orig-keep-bg";
  const PAINTED_ATTR = "data-lp-painted";
  const TEXT_FIXED_ATTR = "data-lp-text-fixed";
  const ORIG_COLOR_ATTR = "data-lp-orig-color";
  const TEXT_COLOR = "#f5f4ef";

  const SURFACE_HEX = {
    "1": "#1c1b19",
    "2": "#262624",
    "3": "#30302e"
  };

  let observer = null;
  let darkEnabled = false;
  let eventsBound = false;
  let pendingRaf = false;

  function ensureStylesheet() {
    if (document.getElementById(STYLE_ID)) {
      return;
    }
    const link = document.createElement("link");
    link.id = STYLE_ID;
    link.rel = "stylesheet";
    link.href = chrome.runtime.getURL("dark-theme.css");
    (document.head || document.documentElement).appendChild(link);
  }

  function mirrorLocal(enabled) {
    try {
      localStorage.setItem(LS_KEY, enabled ? "1" : "0");
    } catch {
      // ignore
    }
  }

  function readLocalMirror() {
    try {
      return localStorage.getItem(LS_KEY) === "1";
    } catch {
      return false;
    }
  }

  function parseCssColor(raw) {
    if (!raw || typeof raw !== "string") {
      return null;
    }
    const s = raw.trim().toLowerCase();
    if (!s || s === "transparent" || s === "inherit" || s === "initial") {
      return null;
    }
    if (s === "white") {
      return { r: 255, g: 255, b: 255, a: 1 };
    }
    if (s === "black") {
      return { r: 0, g: 0, b: 0, a: 1 };
    }

    let m = s.match(/^#([0-9a-f]{3})$/i);
    if (m) {
      const h = m[1];
      return {
        r: parseInt(h[0] + h[0], 16),
        g: parseInt(h[1] + h[1], 16),
        b: parseInt(h[2] + h[2], 16),
        a: 1
      };
    }

    m = s.match(/^#([0-9a-f]{6})$/i);
    if (m) {
      const h = m[1];
      return {
        r: parseInt(h.slice(0, 2), 16),
        g: parseInt(h.slice(2, 4), 16),
        b: parseInt(h.slice(4, 6), 16),
        a: 1
      };
    }

    m = s.match(
      /^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)(?:\s*,\s*([\d.]+))?\s*\)$/
    );
    if (m) {
      return {
        r: Number(m[1]),
        g: Number(m[2]),
        b: Number(m[3]),
        a: m[4] === undefined ? 1 : Number(m[4])
      };
    }
    return null;
  }

  function relativeLuminance(c) {
    const lin = (v) => {
      const x = v / 255;
      return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4);
    };
    return 0.2126 * lin(c.r) + 0.7152 * lin(c.g) + 0.0722 * lin(c.b);
  }

  function saturation(c) {
    const max = Math.max(c.r, c.g, c.b) / 255;
    const min = Math.min(c.r, c.g, c.b) / 255;
    if (max === 0) {
      return 0;
    }
    return (max - min) / max;
  }

  function isLightNeutral(c) {
    if (!c || c.a < 0.35) {
      return false;
    }
    const L = relativeLuminance(c);
    const S = saturation(c);
    if (L >= 0.72 && S <= 0.22) {
      return true;
    }
    if (L >= 0.55 && S <= 0.12) {
      return true;
    }
    if (L >= 0.85 && S <= 0.35) {
      return true;
    }
    return false;
  }

  function isSaturatedStatus(c) {
    if (!c || c.a < 0.35) {
      return false;
    }
    const L = relativeLuminance(c);
    const S = saturation(c);
    return S >= 0.18 && L >= 0.12 && L <= 0.92;
  }

  function muteStatusCss(c) {
    const base = { r: 28, g: 27, b: 25 };
    const t = 0.4;
    return (
      "rgb(" +
      Math.round(c.r * t + base.r * (1 - t)) +
      ", " +
      Math.round(c.g * t + base.g * (1 - t)) +
      ", " +
      Math.round(c.b * t + base.b * (1 - t)) +
      ")"
    );
  }

  function surfaceLevel(c) {
    const L = relativeLuminance(c);
    if (L >= 0.92) {
      return "1";
    }
    if (L >= 0.78) {
      return "2";
    }
    return "3";
  }

  function skipTag(tag) {
    return (
      tag === "IMG" ||
      tag === "SVG" ||
      tag === "VIDEO" ||
      tag === "CANVAS" ||
      tag === "SCRIPT" ||
      tag === "STYLE" ||
      tag === "LINK" ||
      tag === "META" ||
      tag === "BR" ||
      tag === "HR" ||
      tag === "PATH" ||
      tag === "I" ||
      tag === "B" ||
      tag === "STRONG" ||
      tag === "EM" ||
      tag === "SMALL" ||
      tag === "FONT" ||
      tag === "SPAN"
    );
  }

  function isDarkText(c) {
    if (!c || c.a < 0.4) {
      return false;
    }
    // Unreadable on dark surfaces (black / charcoal / dark gray)
    return relativeLuminance(c) <= 0.55 && saturation(c) <= 0.35;
  }

  function insideKeepBg(el) {
    return !!(el.closest && el.closest("[" + KEEP_ATTR + "]"));
  }

  function forceLightText(el) {
    if (!el || el.nodeType !== 1 || insideKeepBg(el)) {
      return;
    }
    if (!el.hasAttribute(ORIG_COLOR_ATTR)) {
      const prev =
        (el.getAttribute("color") || "") +
        "|" +
        ((el.style && el.style.getPropertyValue("color")) || "");
      el.setAttribute(ORIG_COLOR_ATTR, prev);
    }
    if (el.hasAttribute("color")) {
      el.removeAttribute("color");
    }
    el.style.setProperty("color", TEXT_COLOR, "important");
    el.style.setProperty("-webkit-text-fill-color", TEXT_COLOR, "important");
    el.style.setProperty("text-shadow", "none", "important");
    el.setAttribute(TEXT_FIXED_ATTR, "1");
  }

  function fixDarkText(el) {
    if (!el || el.nodeType !== 1 || insideKeepBg(el)) {
      return;
    }

    const tag = el.tagName;
    if (
      tag === "IMG" ||
      tag === "SVG" ||
      tag === "SCRIPT" ||
      tag === "STYLE" ||
      tag === "LINK" ||
      tag === "META" ||
      tag === "BR" ||
      tag === "HR" ||
      tag === "PATH"
    ) {
      return;
    }

    // Legacy color= / style color=
    if (el.hasAttribute("color")) {
      const c = parseCssColor(el.getAttribute("color"));
      if (isDarkText(c) || !c) {
        forceLightText(el);
        return;
      }
    }

    const style = el.getAttribute("style");
    if (style && /(?:^|;)\s*color\s*:/i.test(style)) {
      const fromStyle = el.style && el.style.color;
      const c =
        parseCssColor(fromStyle) ||
        parseCssColor((style.match(/(?:^|;)\s*color\s*:\s*([^;]+)/i) || [])[1]);
      if (isDarkText(c)) {
        forceLightText(el);
        return;
      }
    }

    // Controls / tabs / toolbar cells — always force readable text
    const cls = typeof el.className === "string" ? el.className.toLowerCase() : "";
    const id = (el.id || "").toLowerCase();
    const isChrome =
      tag === "BUTTON" ||
      tag === "LABEL" ||
      tag === "LEGEND" ||
      tag === "SUMMARY" ||
      tag === "A" ||
      tag === "FONT" ||
      (tag === "INPUT" &&
        /^(button|submit|reset)$/i.test(el.type || "")) ||
      cls.indexOf("btn") >= 0 ||
      cls.indexOf("button") >= 0 ||
      cls.indexOf("tab") >= 0 ||
      cls.indexOf("toolbar") >= 0 ||
      cls.indexOf("menu") >= 0 ||
      id.indexOf("tab") >= 0 ||
      id.indexOf("menu") >= 0 ||
      el.getAttribute("role") === "button" ||
      el.getAttribute("role") === "tab";

    if (isChrome) {
      let computed;
      try {
        computed = parseCssColor(window.getComputedStyle(el).color);
      } catch {
        computed = null;
      }
      if (!computed || isDarkText(computed) || relativeLuminance(computed) < 0.7) {
        forceLightText(el);
      }
      return;
    }

    // Any other node that still computes to near-black
    try {
      const computed = parseCssColor(window.getComputedStyle(el).color);
      if (isDarkText(computed)) {
        forceLightText(el);
      }
    } catch {
      // ignore
    }
  }

  function paintSurface(el, level) {
    const hex = SURFACE_HEX[level] || SURFACE_HEX["2"];
    el.setAttribute(SURFACE_ATTR, level);
    el.setAttribute(PAINTED_ATTR, "1");
    el.removeAttribute(KEEP_ATTR);
    el.style.setProperty("background-color", hex, "important");
    el.style.setProperty("background-image", "none", "important");
  }

  function applyKeep(el, c) {
    el.setAttribute(KEEP_ATTR, "1");
    el.removeAttribute(SURFACE_ATTR);
    if (c) {
      const muted = muteStatusCss(c);
      if (!el.hasAttribute(ORIG_KEEP_BG)) {
        const prev =
          (el.style && el.style.getPropertyValue("background-color")) ||
          el.getAttribute("bgcolor") ||
          "";
        el.setAttribute(ORIG_KEEP_BG, prev);
      }
      el.style.setProperty("background-color", muted, "important");
      el.style.setProperty("background-image", "none", "important");
      el.setAttribute(PAINTED_ATTR, "1");
    }
  }

  function markElement(el) {
    if (!el || el.nodeType !== 1 || skipTag(el.tagName)) {
      return;
    }

    if (el.hasAttribute("bgcolor")) {
      const raw = el.getAttribute("bgcolor");
      const c = parseCssColor(raw);
      if (isSaturatedStatus(c)) {
        if (!el.hasAttribute(ORIG_BGCOLOR)) {
          el.setAttribute(ORIG_BGCOLOR, raw);
        }
        el.removeAttribute("bgcolor");
        applyKeep(el, c);
      } else if (isLightNeutral(c)) {
        if (!el.hasAttribute(ORIG_BGCOLOR)) {
          el.setAttribute(ORIG_BGCOLOR, raw);
        }
        el.removeAttribute("bgcolor");
        paintSurface(el, surfaceLevel(c));
      }
    }

    const style = el.getAttribute("style");
    if (style && /background(-color)?\s*:/i.test(style) && !el.hasAttribute(PAINTED_ATTR)) {
      const fromStyle = el.style && el.style.backgroundColor;
      const c =
        parseCssColor(fromStyle) ||
        parseCssColor((style.match(/background-color\s*:\s*([^;]+)/i) || [])[1]);
      if (isSaturatedStatus(c)) {
        applyKeep(el, c);
      } else if (isLightNeutral(c)) {
        if (!el.hasAttribute(ORIG_STYLE_BG) && fromStyle) {
          el.setAttribute(ORIG_STYLE_BG, fromStyle);
        }
        paintSurface(el, surfaceLevel(c));
      }
    }
  }

  function markComputedSurface(el) {
    if (!el || el.nodeType !== 1 || skipTag(el.tagName) || el.hasAttribute(KEEP_ATTR)) {
      return;
    }

    let bg;
    try {
      bg = window.getComputedStyle(el).backgroundColor;
    } catch {
      return;
    }
    const c = parseCssColor(bg);
    if (!c || c.a < 0.35) {
      return;
    }
    if (isSaturatedStatus(c)) {
      applyKeep(el, c);
      return;
    }
    if (isLightNeutral(c)) {
      if (!el.hasAttribute(ORIG_STYLE_BG)) {
        el.setAttribute(ORIG_STYLE_BG, bg);
      }
      paintSurface(el, surfaceLevel(c));
    }
  }

  function normalizeTree(root) {
    if (!root || !darkEnabled) {
      return;
    }

    if (root.nodeType === 1) {
      markElement(root);
      markComputedSurface(root);
      fixDarkText(root);
    }

    const painted = root.querySelectorAll
      ? root.querySelectorAll("[bgcolor], [style*='background']")
      : [];
    for (let i = 0; i < painted.length; i++) {
      markElement(painted[i]);
    }

    const structural = root.querySelectorAll
      ? root.querySelectorAll(
          "body, table, thead, tbody, tr, td, th, div, form, fieldset, section, header, footer, nav, main, aside, ul, ol, li, dialog"
        )
      : [];
    const cap = Math.min(structural.length, 5000);
    for (let i = 0; i < cap; i++) {
      markComputedSurface(structural[i]);
    }

    // Text: fonts, links, buttons, tabs, cells
    const textNodes = root.querySelectorAll
      ? root.querySelectorAll(
          "font, a, button, label, legend, summary, span, b, strong, em, i, u, td, th, li, p, h1, h2, h3, h4, h5, h6, input[type='button'], input[type='submit'], input[type='reset'], [role='button'], [role='tab'], [class*='btn'], [class*='button'], [class*='tab'], [class*='menu'], [class*='toolbar'], [id*='tab'], [id*='menu']"
        )
      : [];
    const tcap = Math.min(textNodes.length, 6000);
    for (let i = 0; i < tcap; i++) {
      fixDarkText(textNodes[i]);
    }
  }

  function normalizeSoon() {
    if (!darkEnabled || pendingRaf) {
      return;
    }
    pendingRaf = true;
    requestAnimationFrame(() => {
      pendingRaf = false;
      normalizeTree(document.documentElement);
    });
  }

  function restoreSurfaces() {
    document.querySelectorAll("[" + ORIG_BGCOLOR + "]").forEach((el) => {
      el.setAttribute("bgcolor", el.getAttribute(ORIG_BGCOLOR));
      el.removeAttribute(ORIG_BGCOLOR);
      el.removeAttribute(SURFACE_ATTR);
      el.removeAttribute(KEEP_ATTR);
      el.removeAttribute(PAINTED_ATTR);
      el.style.removeProperty("background-color");
      el.style.removeProperty("background-image");
    });

    document.querySelectorAll("[" + ORIG_STYLE_BG + "]").forEach((el) => {
      el.style.removeProperty("background-color");
      el.style.removeProperty("background-image");
      const orig = el.getAttribute(ORIG_STYLE_BG);
      if (orig) {
        el.style.backgroundColor = orig;
      }
      el.removeAttribute(ORIG_STYLE_BG);
      el.removeAttribute(SURFACE_ATTR);
      el.removeAttribute(KEEP_ATTR);
      el.removeAttribute(PAINTED_ATTR);
    });

    document.querySelectorAll("[" + ORIG_KEEP_BG + "]").forEach((el) => {
      el.style.removeProperty("background-color");
      el.style.removeProperty("background-image");
      el.removeAttribute(ORIG_KEEP_BG);
      el.removeAttribute(KEEP_ATTR);
      el.removeAttribute(PAINTED_ATTR);
    });

    document
      .querySelectorAll("[" + SURFACE_ATTR + "], [" + KEEP_ATTR + "], [" + PAINTED_ATTR + "]")
      .forEach((el) => {
        el.removeAttribute(SURFACE_ATTR);
        el.removeAttribute(KEEP_ATTR);
        el.removeAttribute(PAINTED_ATTR);
        el.style.removeProperty("background-color");
        el.style.removeProperty("background-image");
      });

    document.querySelectorAll("[" + TEXT_FIXED_ATTR + "]").forEach((el) => {
      el.style.removeProperty("color");
      el.style.removeProperty("-webkit-text-fill-color");
      el.style.removeProperty("text-shadow");
      const orig = el.getAttribute(ORIG_COLOR_ATTR) || "";
      const parts = orig.split("|");
      if (parts[0]) {
        el.setAttribute("color", parts[0]);
      }
      if (parts[1]) {
        el.style.color = parts[1];
      }
      el.removeAttribute(ORIG_COLOR_ATTR);
      el.removeAttribute(TEXT_FIXED_ATTR);
    });
  }

  function startObserver() {
    if (observer || !document.documentElement) {
      return;
    }

    observer = new MutationObserver((mutations) => {
      if (!darkEnabled) {
        return;
      }

      for (let i = 0; i < mutations.length; i++) {
        const m = mutations[i];
        if (m.type === "attributes") {
          markElement(m.target);
          markComputedSurface(m.target);
          fixDarkText(m.target);
          if (m.target.querySelectorAll) {
            const kids = m.target.querySelectorAll(
              "table, tr, td, th, div, form, ul, ol, li, font, a, span, button, input"
            );
            const n = Math.min(kids.length, 800);
            for (let k = 0; k < n; k++) {
              markElement(kids[k]);
              markComputedSurface(kids[k]);
              fixDarkText(kids[k]);
            }
          }
        } else if (m.addedNodes && m.addedNodes.length) {
          for (let j = 0; j < m.addedNodes.length; j++) {
            const node = m.addedNodes[j];
            if (node.nodeType === 1) {
              normalizeTree(node);
            }
          }
        }
      }
    });

    observer.observe(document.documentElement, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ["bgcolor", "style", "class", "hidden", "aria-expanded", "aria-hidden"]
    });

    if (!eventsBound) {
      eventsBound = true;
      // Quiet rescan after UI toggles — no overlay
      document.addEventListener(
        "click",
        () => {
          if (darkEnabled) {
            normalizeTree(document.documentElement);
            normalizeSoon();
          }
        },
        true
      );
    }
  }

  function stopObserver() {
    if (observer) {
      observer.disconnect();
      observer = null;
    }
  }

  function setDark(enabled) {
    try {
      ensureStylesheet();
      darkEnabled = !!enabled;
      document.documentElement.classList.toggle(CLASS_NAME, darkEnabled);
      mirrorLocal(darkEnabled);

      if (darkEnabled) {
        startObserver();
        normalizeTree(document.documentElement);
        normalizeSoon();
      } else {
        stopObserver();
        restoreSurfaces();
      }
    } catch {
      // ignore
    }
  }

  if (readLocalMirror()) {
    try {
      ensureStylesheet();
      document.documentElement.classList.add(CLASS_NAME);
      darkEnabled = true;
    } catch {
      // ignore
    }
  }

  function readAndApply() {
    chrome.storage.local.get({ darkTheme: false }, (data) => {
      if (chrome.runtime.lastError) {
        return;
      }
      setDark(!!data.darkTheme);
    });
  }

  readAndApply();

  if (document.readyState === "loading") {
    document.addEventListener(
      "DOMContentLoaded",
      () => {
        if (darkEnabled) {
          normalizeTree(document.documentElement);
        }
        readAndApply();
      },
      { once: true }
    );
  }

  chrome.storage.onChanged.addListener((changes, area) => {
    if (area !== "local" || !changes.darkTheme) {
      return;
    }
    setDark(!!changes.darkTheme.newValue);
  });
})();
