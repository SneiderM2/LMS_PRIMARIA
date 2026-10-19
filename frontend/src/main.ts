import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));

// Registro único del Service Worker (PWA). Se retiró el registro duplicado de
// ngsw-worker.js que competía con este service worker y generaba recargas.
if (typeof window !== 'undefined'
    && 'serviceWorker' in navigator
    && (window.location.protocol === 'http:' || window.location.protocol === 'https:')) {
  window.addEventListener('load', () => {
    navigator.serviceWorker
      .register('service-worker.js?v=6')
      .catch((error) => console.warn('[PWA] No fue posible registrar el Service Worker:', error));
  });
}
