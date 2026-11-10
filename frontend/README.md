# LMS Primaria — Frontend Client (Angular 18 PWA) 🎒✨

Cliente web progresivo (Progressive Web App) para la plataforma **LMS Primaria**, diseñado especialmente para el proceso pedagógico en básica primaria. Construido con **Angular 18**, componentes independientes (*Standalone Components*), control de reactividad mediante **Signals**, internacionalización en tiempo real y estrictas medidas de seguridad y privacidad en aulas de informática y equipos compartidos.

---

## 🌟 Características Principales

- **Arquitectura Standalone:** Cero módulos innecesarios (`NgModule`), carga diferida granular (*lazy loading*) por rutas y dependencias directas en componentes.
- **Progressive Web App (PWA):** Instalable en ordenadores y tabletas escolares, con Service Worker optimizado (estrategia Network-First para scripts/estilos y Cache-First para recursos estáticos).
- **Protección de Datos Infantiles:** Interfaz amigable en lenguaje claro que cumple los requerimientos de la Superintendencia de Industria y Comercio (SIC) y la Ley 1098 de 2006.
- **Soporte Multilingüe (i18n):** Traducción en vivo en 5 idiomas (Español 🇪🇸, Inglés 🇺🇸, Chino mandarín 🇨🇳, Japonés 🇯🇵 y Portugués 🇧🇷) mediante DOM Walker reactivo.
- **Accesibilidad:** Cumplimiento de estándares WCAG 2.1 AA / NTC 5854, soporte de modo oscuro con paleta contrastada y navegación por teclado.

---

## 🔐 Flujo de Autenticación y Privacidad en Equipos Compartidos

En las salas de sistemas escolares, múltiples niños utilizan el mismo computador a lo largo de la jornada académica. Para evitar fugas de información personal identificable (PII), se implementó la siguiente arquitectura:

### 1. Sesión en Memoria RAM (`signal`)
- En [`AuthService`](src/app/core/services/auth.service.ts), el estado del usuario autenticado se almacena en memoria volátil:
  ```typescript
  readonly currentUser = signal<User | null>(null);
  ```
- **Cero PII en `localStorage`:** Los datos sensibles del alumno (nombres, rol, grado, documento) jamás se persisten en `localStorage` en texto plano.

### 2. Rehidratación Silenciosa al Recargar (`F5`) y Aislamiento Multi-Pestaña
- Para permitir que dos usuarios distintos (ej. Docente y Estudiante) trabajen simultáneamente en pestañas diferentes del mismo navegador sin expulsarse mutuamente ni re-adoptar tokens ajenos al cerrar sesión:
  1. Cada pestaña mantiene su JWT aislado en `sessionStorage` (`lms_tab_auth_token`) y metadatos mínimos en `lms_session_meta`.
  2. Si el usuario presiona `F5`, `AuthService.ensureUserHydrated()` rehidrata la sesión de la pestaña desde `sessionStorage` o consultando `/api/auth/me` con el JWT correspondiente.
  3. Los guards de ruta esperan la finalización de esta rehidratación antes de decidir redirecciones.
  4. `SessionTimeoutService.handleStorageEvent` decodifica los claims `nameid`/`sub`/`user_id` mediante `extractUserIdFromToken` y únicamente cierra sesión cuando otra pestaña autentica al **mismo usuario**.
  5. Al cerrar sesión (`clearSessionData()`), si `localStorage['lms_auth_token']` pertenece a otra pestaña con distinto token, se conserva `localStorage` intacto para la otra sesión y se marca `sessionStorage['lms_tab_logged_out'] = 'true'` hasta el próximo `saveAuthData()`.

### 3. Cierre Preventivo por Inactividad (5 Minutos)
- El servicio [`SessionTimeoutService`](src/app/core/services/session-timeout.service.ts) registra los eventos de interacción del usuario (clics, teclado, desplazamientos, toques).
- Tras **2,5 minutos** sin actividad muestra el modal preventivo de advertencia (`WARNING_MS`) y, al cumplirse **5 minutos de inactividad** (`LOGOUT_MS`), ejecuta `AuthService.clearSessionData()`:
  - Revoca y limpia el estado/token de sesión de la pestaña y redirige a inicio de sesión con advertencia de inactividad.

---

## 🛡️ Protección de Rutas y `minorConsentGuard`

El sistema protege la navegación hacia los paneles estudiantiles mediante el guard funcional **`minorConsentGuard`** (`src/app/core/guards/minor-consent.guard.ts`):

```typescript
export const minorConsentGuard: CanActivateFn = (route, state) => {
  // 1. Asegura la rehidratación del estado del usuario
  // 2. Si el rol es Student y no cuenta con consentimiento activo de acudiente:
  //    - Cancela la navegación hacia /student/*
  //    - Redirige a /login?reason=minor_consent_required
  //    - Abre el modal de Aviso de Privacidad y Consentimiento de Acudiente
};
```

Rutas protegidas por la combinación de guards:
- `/student/**` protegido por `authGuard`, `roleGuard(['Student'])` y `minorConsentGuard`.
- `/teacher/**` protegido por `authGuard` y `roleGuard(['Teacher'])`.
- `/admin/**` protegido por `authGuard` y `roleGuard(['Admin'])`.

---

## 🛠️ Comandos de Desarrollo y Compilación

### Instalación de Dependencias
```bash
npm ci
```

### Servidor de Desarrollo Local
```bash
npm start
```
Disponible en `http://localhost:4200` con proxy o redirección hacia la API local en `http://localhost:5000`.

### Compilación para Producción (GitHub Pages / CDN)
Para generar el bundle optimizado de producción:
```bash
npx ng build --configuration production
```
Los artefactos de salida se ubicarán en `dist/lms/browser` listos para ser desplegados en **GitHub Pages** u hosting estático seguro.

### Ejecución de Pruebas Unitarias
```bash
npm test
```
Ejecuta las **24 pruebas** unitarias, de aislamiento multi-pestaña y de accesibilidad (Karma/Jasmine) en entorno `ChromeHeadless`.
