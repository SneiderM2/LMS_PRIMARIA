// Service Worker para LMS SciKids - Versión v6 (estable, sin recargas)
// Estrategia: solo caché offline para recursos estáticos. NO reload automático.
const CACHE_NAME = 'scikids-lms-cache-v6';

// Instalación: NO usar skipWaiting para evitar activaciones abruptas
self.addEventListener('install', (event) => {
  // Esperar hasta que el SW anterior libere los clientes antes de activar
  // NO llamar skipWaiting() aquí para evitar el loop de recarga
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll(['./index.html']).catch(() => {});
    })
  );
});

// Activación: limpiar cachés viejas SIN reclamar clientes activos
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
    // NOTA: No llamar self.clients.claim() para evitar disparar 'controllerchange'
    // que reinicia la página en clientes ya activos
  );
});

// Intercepción de solicitudes de red
self.addEventListener('fetch', (event) => {
  const request = event.request;
  const url = new URL(request.url);

  // Ignorar esquemas que no sean HTTP/HTTPS
  if (!url.protocol.startsWith('http')) return;

  // 1. Peticiones a la API del backend (Render): siempre red, sin caché
  if (url.hostname.includes('onrender.com') || url.pathname.startsWith('/api/')) {
    event.respondWith(
      fetch(request).catch(() => {
        return new Response(
          JSON.stringify({
            status: 503,
            message: 'Sin conexión. La sincronización se reanudará al reconectarte.'
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

  // 2. Solicitudes de navegación SPA: Network-First, fallback a index.html en caché
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
        .catch(() => caches.match('./index.html'))
    );
    return;
  }

  // 3. Recursos JS, CSS: Network-First (bundles cambian con cada build)
  if (url.pathname.endsWith('.js') || url.pathname.endsWith('.css')) {
    event.respondWith(
      fetch(request)
        .then((networkResponse) => {
          if (networkResponse && networkResponse.status === 200) {
            const clone = networkResponse.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
          }
          return networkResponse;
        })
        .catch(() => caches.match(request))
    );
    return;
  }

  // 4. Otros recursos estáticos: Cache-First con fallback a red
  event.respondWith(
    caches.match(request).then((cachedResponse) => {
      if (cachedResponse) return cachedResponse;
      return fetch(request).then((networkResponse) => {
        if (networkResponse && networkResponse.status === 200 && networkResponse.type === 'basic') {
          const clone = networkResponse.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
        }
        return networkResponse;
      });
    })
  );
});
