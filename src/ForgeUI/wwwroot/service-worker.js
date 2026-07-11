// Forge PWA service worker — Phase 40.4.
//
// DELIBERATE and minimal. Blazor Server renders every frame over the SignalR
// circuit, so this app is online-by-nature: there is NO offline mode to cache.
// The worker exists only to (a) make the app installable and (b) serve the
// static shell fast on repeat visits.
//
// The load-bearing rule: the SignalR circuit (/_blazor, WebSocket negotiate)
// and the OIDC round-trip (/auth, /signin-oidc, /signout) must be NETWORK-ONLY,
// never intercepted. A "cache everything" worker silently breaks both. We
// intercept ONLY the precached static shell and top-level navigations; every
// other request falls through to the network untouched.

const CACHE_VERSION = 'forge-shell-v1';

// Static shell — safe to cache. base href is "/".
const SHELL = [
  '/',
  '/css/forge.css',
  '/favicon.png',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
  '/manifest.webmanifest',
  '/offline.html',
  '/_framework/blazor.server.js',
];

// Paths that must never be served from cache — always hit the network.
const NETWORK_ONLY = ['/_blazor', '/auth', '/signin-oidc', '/signout'];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_VERSION)
      // addAll is atomic; a single 404 would fail install. Cache best-effort.
      .then((cache) => Promise.all(
        SHELL.map((url) => cache.add(url).catch(() => {}))
      ))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(
        keys.filter((k) => k !== CACHE_VERSION).map((k) => caches.delete(k))
      ))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const req = event.request;

  // Only ever touch GET. POSTs (auth callbacks, blazor negotiate) pass through.
  if (req.method !== 'GET') return;

  const url = new URL(req.url);

  // Cross-origin (CDN-less app, but be safe) → network.
  if (url.origin !== self.location.origin) return;

  // Circuit + OIDC carve-out → network-only, never intercept.
  if (NETWORK_ONLY.some((p) => url.pathname.startsWith(p))) return;

  // Top-level navigations: network-first, fall back to the offline card.
  if (req.mode === 'navigate') {
    event.respondWith(
      fetch(req).catch(() =>
        caches.match('/offline.html').then((r) => r || Response.error())
      )
    );
    return;
  }

  // Static shell assets → cache-first, then network (and warm the cache).
  event.respondWith(
    caches.match(req).then((cached) => {
      if (cached) return cached;
      return fetch(req).then((res) => {
        if (res.ok && SHELL.includes(url.pathname)) {
          const copy = res.clone();
          caches.open(CACHE_VERSION).then((c) => c.put(req, copy));
        }
        return res;
      });
    })
  );
});
