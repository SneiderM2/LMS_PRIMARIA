// Service Worker para LMS SciKids - Soporte PWA Offline
const CACHE_NAME = 'scikids-lms-cache-v3';
const SCOPE = self.registration ? self.registration.scope : './';

// Instalación: Precarga de assets críticos
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(async (cache) => {
      console.log('[SW] Pre-cacheados los recursos estáticos iniciales');
      const assetsToCache = [
        new URL('./', SCOPE).href,
        new URL('index.html', SCOPE).href,
        new URL('manifest.webmanifest', SCOPE).href,
        new URL('assets/icons/icon-192x192.png', SCOPE).href,
        new URL('assets/icons/icon-512x512.png', SCOPE).href
      ];
      for (const asset of assetsToCache) {
        try {
          await cache.add(asset);
        } catch (err) {
          console.warn('[SW] No se pudo pre-cachear:', asset, err);
        }
      }
    })
  );
  self.skipWaiting();
});

// Activación: Limpieza de versiones obsoletas
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys.map((key) => {
          if (key !== CACHE_NAME) {
            console.log('[SW] Eliminando caché obsoleta:', key);
            return caches.delete(key);
          }
        })
      )
    )
  );
  self.clients.claim();
});

// Intercepción de solicitudes de red
self.addEventListener('fetch', (event) => {
  const request = event.request;
  const url = new URL(request.url);

  // Ignorar esquemas que no sean HTTP/HTTPS (como chrome-extension://)
  if (!url.protocol.startsWith('http')) return;

  // 1. Peticiones a la API del backend: Estrategia Network-First con fallback JSON
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(
      fetch(request).catch(() => {
        return new Response(
          JSON.stringify({
            status: 503,
            message: 'Estás en modo sin conexión. La sincronización con el servidor se reanudará al conectarte a internet.'
          }),
          {
            status: 503,
            headers: { 'Content-Type': 'application/json; charset=utf-8' }
          }
        );
      })
    );
    return;
  }

  // 2. Solicitudes de navegación (rutas Angular SPA): Fallback al index.html
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then((response) => {
          if (response && response.status === 200) {
            const clone = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
          }
          return response;
        })
        .catch(() => {
          return caches.match('/index.html');
        })
    );
    return;
  }

  // 3. Recursos estáticos (JS, CSS, imágenes, fuentes): Stale-While-Revalidate
  event.respondWith(
    caches.match(request).then((cachedResponse) => {
      const fetchPromise = fetch(request)
        .then((networkResponse) => {
          if (networkResponse && networkResponse.status === 200 && networkResponse.type === 'basic') {
            const clone = networkResponse.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
          }
          return networkResponse;
        })
        .catch(() => cachedResponse);

      return cachedResponse || fetchPromise;
    })
  );
});
