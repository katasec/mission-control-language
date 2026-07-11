// Forge install affordance — Phase 40.4, Task 5.
//
// Two paths, both dismissible + remembered in localStorage:
//   • iOS Safari has no beforeinstallprompt and never auto-prompts, so we show a
//     one-time hint with the manual Share → "Add to Home Screen" ritual.
//   • Android/desktop Chromium fire beforeinstallprompt; we capture it and show
//     an "Install Forge" button that triggers the native prompt on click.
// Self-contained vanilla JS — no framework coupling to the SignalR circuit.
(function () {
  'use strict';

  var DISMISS_KEY = 'forge.install.dismissed';
  if (localStorage.getItem(DISMISS_KEY) === '1') return;

  // Already installed / launched standalone → nothing to offer.
  var standalone = window.matchMedia('(display-mode: standalone)').matches ||
    window.navigator.standalone === true;
  if (standalone) return;

  function isIosSafari() {
    var ua = window.navigator.userAgent;
    var iOS = /iPad|iPhone|iPod/.test(ua) ||
      // iPadOS 13+ masquerades as Mac; detect via touch.
      (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    if (!iOS) return false;
    // Exclude in-app browsers (FB/IG/Chrome-iOS) — no Add-to-Home-Screen there.
    var webkit = /WebKit/.test(ua);
    var otherBrowser = /CriOS|FxiOS|EdgiOS|OPiOS|GSA|FBAN|FBAV|Instagram|Line/.test(ua);
    return webkit && !otherBrowser;
  }

  function injectStyles() {
    if (document.getElementById('forge-install-styles')) return;
    var css = [
      '.forge-install{position:fixed;left:0;right:0;bottom:0;z-index:9000;',
        'padding:.9rem 1rem calc(.9rem + env(safe-area-inset-bottom));',
        'display:flex;gap:.75rem;align-items:center;',
        'background:var(--surface,#fff);color:var(--ink,#1c1b19);',
        'border-top:1px solid var(--border,rgba(0,0,0,.1));',
        'box-shadow:0 -6px 24px rgba(0,0,0,.12);font-size:.9rem;line-height:1.35;',
        'animation:forge-install-in .28s ease-out both}',
      '@keyframes forge-install-in{from{transform:translateY(100%)}to{transform:translateY(0)}}',
      '.forge-install__mark{width:34px;height:34px;flex:0 0 auto;border-radius:50%}',
      '.forge-install__body{flex:1 1 auto;min-width:0}',
      '.forge-install__body b{display:block;font-weight:600}',
      '.forge-install__body span{opacity:.72}',
      '.forge-install__act{flex:0 0 auto;font:inherit;font-weight:600;cursor:pointer;',
        'color:var(--accent-contrast,#fff);background:var(--accent,#c2410c);',
        'border:0;padding:.5rem 1rem;border-radius:999px;white-space:nowrap}',
      '.forge-install__x{flex:0 0 auto;cursor:pointer;background:none;border:0;',
        'color:inherit;opacity:.5;font-size:1.25rem;line-height:1;padding:.25rem}',
      '.forge-install__x:hover{opacity:.9}'
    ].join('');
    var el = document.createElement('style');
    el.id = 'forge-install-styles';
    el.textContent = css;
    document.head.appendChild(el);
  }

  function dismiss(banner) {
    localStorage.setItem(DISMISS_KEY, '1');
    if (banner && banner.parentNode) banner.parentNode.removeChild(banner);
  }

  function banner(innerHtml, wireAction) {
    injectStyles();
    var el = document.createElement('div');
    el.className = 'forge-install';
    el.setAttribute('role', 'dialog');
    el.setAttribute('aria-label', 'Install Forge');
    el.innerHTML =
      '<img class="forge-install__mark" src="/icons/icon-192.png" alt="" />' +
      '<div class="forge-install__body">' + innerHtml + '</div>' + wireAction +
      '<button class="forge-install__x" aria-label="Dismiss">×</button>';
    el.querySelector('.forge-install__x').addEventListener('click', function () {
      dismiss(el);
    });
    document.body.appendChild(el);
    return el;
  }

  // ── Android / desktop: native install prompt ──────────────────────────────
  window.addEventListener('beforeinstallprompt', function (e) {
    e.preventDefault();
    var deferred = e;
    var el = banner(
      '<b>Install Forge</b><span>Add it to your device for an app-like experience.</span>',
      '<button class="forge-install__act">Install</button>'
    );
    el.querySelector('.forge-install__act').addEventListener('click', function () {
      deferred.prompt();
      deferred.userChoice.finally(function () { dismiss(el); });
    });
  });

  // ── iOS Safari: manual Add-to-Home-Screen hint ────────────────────────────
  if (isIosSafari()) {
    // Defer a beat so it doesn't fight first paint.
    window.addEventListener('load', function () {
      setTimeout(function () {
        if (localStorage.getItem(DISMISS_KEY) === '1') return;
        banner(
          '<b>Add Forge to your Home Screen</b>' +
          '<span>Tap Share ↑, then “Add to Home Screen.”</span>',
          ''
        );
      }, 1200);
    });
  }
})();
