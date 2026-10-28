# ERRORES Y SOLUCIONES — LMS Primaria 🔧

Registro histórico de errores, bugs y problemas técnicos encontrados durante el desarrollo del proyecto, con su diagnóstico y solución aplicada.

---

## 📋 Índice

| # | Categoría | Error | Estado |
|:---:|:---|:---|:---:|
| 1 | Backend / BD | Migración de MySQL a PostgreSQL — incompatibilidad de tipos | ✅ Resuelto |
| 2 | Backend / CORS | `No 'Access-Control-Allow-Origin'` al consumir la API desde Angular | ✅ Resuelto |
| 3 | Backend / Render | Backend "duerme" en Render plan gratuito (Cold Start) | ✅ Mitigado |
| 4 | Frontend / SPA | `404 Not Found` al recargar rutas Angular en GitHub Pages | ✅ Resuelto |
| 5 | Frontend / PWA | Service Worker causaba **loop infinito de recargas** de página | ✅ Resuelto |
| 6 | Frontend / PWA | Al restaurar backup, la página **se caía** (404 tras reload) | ✅ Resuelto |
| 7 | Frontend / Angular | `ViewChild #fileInput` no disponible al hacer clic en Restaurar | ✅ Resuelto |
| 8 | Backend / API | `415 Unsupported Media Type` en endpoint `restore-backup` | ✅ Resuelto |
| 9 | Backend / Supabase | Error de conexión SSL con Npgsql en Supabase Pooler | ✅ Resuelto |
| 10 | Frontend / Google OAuth | Botón de Google no se renderiza en modo producción | ✅ Resuelto |
| 11 | Backend / Auth | JWT expirado no retornaba 401 sino 500 | ✅ Resuelto |
| 12 | Frontend / Build | `base-href` incorrecto — app en blanco en GitHub Pages | ✅ Resuelto |
| 13 | Frontend / CSS | Dark mode no persistía entre recargas de página | ✅ Resuelto |
| 14 | Backend / Logs | `FileLogger` lanzaba excepción por ruta de logs en Render | ✅ Resuelto |
| 15 | Backend / CORS | Render bloqueaba preflight OPTIONS de las peticiones Angular | ✅ Resuelto |
| 16 | Frontend / Deploy | `root/index.html` desactualizado — GitHub Pages servía SW v5 antiguo | ✅ Resuelto |
| 17 | Frontend / UX | `http://example.com/avatar.png` en BD — Mixed Content + 404 en avatares | ✅ Resuelto |
| 18 | Frontend / Inspector | Tab `.htaccess` llamaba endpoint inexistente en .NET — 404 en consola | ✅ Resuelto |
| 19 | Backend / Render / Startup | Timeout en deploy de Render por inicialización bloqueante de BD (`DbInitializer`) | ✅ Resuelto |
| 20 | Rendimiento / Auth | Login demoraba ~20 segundos (Render Cold Start + inicialización pesada) | ✅ Resuelto |
| 21 | Frontend / i18n | Desfase y traducción incompleta en vistas dinámicas de Angular (i18n) | ✅ Resuelto |
| 22 | Backend / Seguridad | BOLA/IDOR en asignación de contenidos y matrícula escolar cruzada | ✅ Resuelto |
| 23 | Backend / Sesión | Persistencia de sesiones previas activas tras cambio de contraseña | ✅ Resuelto |
| 24 | Frontend / Seguridad | Fuga potencial de credenciales JWT hacia recursos de terceros (CWE-200) | ✅ Resuelto |
| 25 | Frontend / UI & A11y | Desbalance de contraste en Modo Oscuro, colisión de controles y enlaces `javascript:` | ✅ Resuelto |

---

## 🔴 Detalle de Errores

---

### #1 — Migración de MySQL a PostgreSQL: incompatibilidad de tipos

**Categoría:** Backend / Base de Datos  
**Fecha:** ~2026-09-20  
**Gravedad:** 🔴 Crítico

**Síntoma:**
```
Npgsql.PostgresException: column "..." is of type integer but expression is of type text
```
Al migrar de MySQL (Pomelo EF Core) a PostgreSQL (Npgsql), varios campos mapeados como `int` en C# colisionaban con columnas `text` heredadas del esquema MySQL, y los enums de EF Core no eran compatibles.

**Causa:**
El esquema `bd.txt` original era para MySQL con tipos `VARCHAR`, `TINYINT(1)` y `ENUM(...)` que no tienen equivalentes directos en PostgreSQL.

**Solución aplicada:**
- Se reescribió el `LMSDbContext` con Fluent API adaptada a PostgreSQL (Npgsql):
  - `TINYINT(1)` → `bool` en C# / `boolean` en PG
  - `ENUM(...)` → columnas `text` con constraint `CHECK`
  - Se eliminaron los índices con longitud prefijada (incompatibles en PG)
- Se ejecutó `dotnet ef migrations add InitPostgres` para regenerar migraciones limpias
- Se usó `EnsureCreatedAsync()` en lugar de `MigrateAsync()` en el arranque inicial

**Archivos afectados:**
- `backend/LMS.API/Data/LMSDbContext.cs`
- `backend/LMS.API/Program.cs`

---

### #2 — CORS: `No 'Access-Control-Allow-Origin'`

**Categoría:** Backend / CORS  
**Fecha:** ~2026-09-20  
**Gravedad:** 🔴 Crítico

**Síntoma:**
```
Access to XMLHttpRequest at 'https://lms-primaria.onrender.com/api/auth/login'
from origin 'https://sneider m2.github.io' has been blocked by CORS policy
```
La app de Angular en GitHub Pages no podía comunicarse con el backend en Render.

**Causa:**
La política CORS del backend solo permitía `http://localhost:4200` (desarrollo local). Al desplegar en producción, el origen `https://sneider m2.github.io` no estaba en la lista de permitidos.

**Solución aplicada:**
En `Program.cs`, se actualizó la política CORS con los dos orígenes:

```csharp
builder.Services.AddCors(options => {
  options.AddPolicy("AllowFrontend", policy => {
    policy.WithOrigins(
      "http://localhost:4200",
      "https://sneider m2.github.io"
    )
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials();
  });
});
```

**Archivos afectados:**
- `backend/LMS.API/Program.cs`

---

### #3 — Render plan gratuito: Cold Start (backend "duerme")

**Categoría:** Backend / Render  
**Fecha:** ~2026-09-22  
**Gravedad:** 🟡 Medio

**Síntoma:**
La primera petición a la API tarda entre **30 y 60 segundos** después de un período de inactividad. El frontend mostraba spinner eterno o error de timeout.

**Causa:**
El plan gratuito de Render apaga los servicios web tras 15 minutos de inactividad. Al recibir una nueva petición, el contenedor debe reiniciarse (Cold Start).

**Solución aplicada:**
- Se añadió un indicador visual de "Iniciando servidor..." en el formulario de login con animación de carga
- Se aumentó el timeout del `HttpClient` de Angular a 90 segundos para peticiones de auth
- Solución permanente sugerida: usar plan de pago en Render o configurar un cron job de keep-alive externo (e.g. UptimeRobot)

**Archivos afectados:**
- `frontend/src/app/features/auth/login/login.component.ts`

---

### #4 — SPA: `404 Not Found` al recargar rutas en GitHub Pages

**Categoría:** Frontend / Hosting  
**Fecha:** ~2026-09-24  
**Gravedad:** 🔴 Crítico

**Síntoma:**
Al acceder directamente a una URL como `https://sneider m2.github.io/LMS_PRIMARIA/dashboard/admin` o al recargar con `F5`, GitHub Pages devolvía una página `404` en lugar de cargar la app Angular.

**Causa:**
GitHub Pages sirve archivos estáticos. Las rutas de una SPA (Single Page Application) no corresponden a archivos físicos en el servidor, así que el servidor devuelve 404 cuando se accede directamente a ellas (solo Angular Router sabe cómo manejarlas).

**Solución aplicada:**
Se creó un archivo `404.html` en la raíz del repositorio que redirige todas las rutas desconocidas de vuelta al `index.html` usando `sessionStorage` + `history.replaceState`:

```html
<!-- 404.html: redirect trick para SPA en GitHub Pages -->
<script>
  const path = window.location.pathname.replace('/LMS_PRIMARIA', '');
  sessionStorage.setItem('redirect', path);
  window.location.replace('/LMS_PRIMARIA/');
</script>
```

Y en `index.html` se añadió el script de recuperación de ruta.

**Archivos afectados:**
- `404.html` (nuevo en raíz del repo)
- `frontend/src/index.html`

---

### #5 — Service Worker: loop infinito de recargas de página

**Categoría:** Frontend / PWA  
**Fecha:** 2026-10-06  
**Gravedad:** 🔴 Crítico

**Síntoma:**
La app en producción **recargaba la página cada pocos segundos** de forma indefinida. Especialmente grave durante operaciones largas (restauración de BD). El usuario veía la página parpadear y reiniciarse constantemente.

**Causa:**
El Service Worker (v3, v4, v5) tenía estas dos llamadas problemáticas:

```javascript
// En install:
self.skipWaiting(); // ← activa el nuevo SW INMEDIATAMENTE

// En activate:
self.clients.claim(); // ← toma control de todos los clientes activos
```

Y en `index.html`:
```javascript
registration.update(); // ← verifica nueva versión en CADA carga
// ...
window.location.reload(); // ← si hay nueva versión, recarga
```

El ciclo completo era:
1. Página carga → `registration.update()` verifica nuevo SW
2. Nuevo SW encontrado → `skipWaiting()` lo activa inmediatamente
3. `clients.claim()` toma control del cliente activo
4. Angular detecta el cambio de `controller` del SW
5. `onupdatefound` dispara `window.location.reload()`
6. Página recarga → volver al paso 1 → **loop infinito**

**Solución aplicada:**
- **SW v6**: Eliminados `skipWaiting()` y `clients.claim()` del Service Worker
- **`index.html`**: Eliminado todo el bloque `onupdatefound` con `window.location.reload()`
- El SW ahora solo se registra silenciosamente sin interferir con la navegación

```javascript
// service-worker.js v6 — install sin skipWaiting
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(['./index.html']).catch(() => {}))
  );
  // NO llamar skipWaiting()
});

// activate sin clients.claim()
self.addEventListener('activate', (event) => {
  event.waitUntil(caches.keys().then(...)); // solo limpiar cachés viejas
  // NO llamar self.clients.claim()
});
```

**Archivos afectados:**
- `frontend/public/service-worker.js`
- `frontend/src/index.html`

> **Nota:** Al desplegar este fix, los usuarios deben limpiar el SW viejo manualmente la primera vez: DevTools → Application → Service Workers → Unregister.

---

### #6 — Restaurar backup causa `404` por `window.location.reload()`

**Categoría:** Frontend / SPA  
**Fecha:** 2026-10-05  
**Gravedad:** 🔴 Crítico

**Síntoma:**
Al presionar "📥 Restaurar Backup BD" y confirmar la restauración, la página se caía mostrando un error `404 Not Found` después de que el proceso terminaba.

**Causa:**
El código original de `onFileSelected` llamaba `window.location.reload()` al terminar la restauración exitosa. En una SPA desplegada en GitHub Pages, recargar la URL actual (e.g. `/LMS_PRIMARIA/dashboard/admin`) genera un 404 porque ese path no existe como archivo estático.

```typescript
// ❌ Código problemático original
this.adminService.restoreBackup(file).subscribe({
  next: () => {
    window.location.reload(); // ← CAUSA el 404 en SPA de GitHub Pages
  }
});
```

**Solución aplicada:**
Se reemplazó `window.location.reload()` por recarga de datos en memoria:

```typescript
// ✅ Solución: refrescar datos sin recargar la página
this.adminService.restoreBackup(file).subscribe({
  next: () => {
    this.loadMetrics();       // recarga métricas en memoria
    this.loadUsers();         // recarga usuarios
    this.loadAdminCourses();  // recarga cursos
  }
});
```

**Archivos afectados:**
- `frontend/src/app/features/admin/admin-dashboard.component.ts`

---

### #7 — `ViewChild #fileInput` no disponible en `triggerRestore()`

**Categoría:** Frontend / Angular  
**Fecha:** 2026-10-05  
**Gravedad:** 🟡 Medio

**Síntoma:**
Al hacer clic en "Restaurar Backup BD", el selector de archivos no se abría. En consola:
```
TypeError: Cannot read properties of undefined (reading 'nativeElement')
```

**Causa:**
El `@ViewChild('fileInput')` de Angular no estaba disponible en el momento en que se llamaba `triggerRestore()`, probablemente por timing del ciclo de vida del componente o porque la sección con el `#fileInput` no era visible (estaba dentro de un `*ngIf`).

**Solución aplicada:**
Se implementó un mecanismo dual con fallback a selector dinámico:

```typescript
public triggerRestore(): void {
  // Intento 1: usar el ViewChild de Angular
  try {
    if (this.fileInput?.nativeElement) {
      this.fileInput.nativeElement.click();
      return;
    }
  } catch (e) {}

  // Fallback: crear input dinámico independiente del DOM de Angular
  const fileSelector = document.createElement('input');
  fileSelector.type = 'file';
  fileSelector.accept = '.sql';
  fileSelector.onchange = (e) => this.onFileSelected(e);
  document.body.appendChild(fileSelector);
  fileSelector.click();
  setTimeout(() => document.body.removeChild(fileSelector), 2000);
}
```

**Archivos afectados:**
- `frontend/src/app/features/admin/admin-dashboard.component.ts`

---

### #8 — `415 Unsupported Media Type` en endpoint `restore-backup`

**Categoría:** Backend / API  
**Fecha:** 2026-10-05  
**Gravedad:** 🔴 Crítico

**Síntoma:**
Al subir el archivo `.sql` para restaurar, la API devolvía:
```json
{ "status": 415, "title": "Unsupported Media Type" }
```

**Causa:**
El endpoint `POST /api/admin/restore-backup` esperaba `multipart/form-data` con `[FromForm]`, pero Angular `HttpClient` enviaba la petición con `Content-Type: application/json` o sin el header correcto al usar `FormData`.

**Solución aplicada:**
En el backend, se verificó que el `AdminController` usa `[FromForm]` y `IFormFile`:

```csharp
[HttpPost("restore-backup")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> RestoreBackup([FromForm] IFormFile sqlFile)
```

En el frontend, se aseguró que `HttpClient` **no** sobreescriba el `Content-Type` al usar `FormData` (Angular lo gestiona automáticamente cuando se usa `FormData` sin especificar headers manuales):

```typescript
restoreBackup(file: File): Observable<any> {
  const formData = new FormData();
  formData.append('sqlFile', file, file.name);
  // NO especificar Content-Type manualmente → Angular lo hace automáticamente
  return this.http.post(`${this.apiUrl}/admin/restore-backup`, formData);
}
```

**Archivos afectados:**
- `backend/LMS.API/Controllers/AdminController.cs`
- `frontend/src/app/core/services/admin.service.ts`

---

### #9 — Error SSL con Npgsql al conectar a Supabase Pooler

**Categoría:** Backend / Base de datos  
**Fecha:** ~2026-09-20  
**Gravedad:** 🔴 Crítico

**Síntoma:**
```
Npgsql.NpgsqlException: SSL connection error / certificate verification failed
```
o
```
Exception: 28000: password authentication failed for user "postgres"
```

**Causa:**
Supabase Connection Pooler (PgBouncer) en modo `Transaction` no es compatible con todos los modos SSL de Npgsql. Además, la URI de conexión debía especificar correctamente el modo SSL.

**Solución aplicada:**
Se configuró la cadena de conexión con `Ssl Mode=Require` y `Trust Server Certificate=true`:

```json
"DefaultConnection": "Host=aws-0-us-east-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.xxxxx;Password=xxx;Ssl Mode=Require;Trust Server Certificate=true"
```

Y se cambió a Session Mode (puerto 5432) en lugar de Transaction Mode (puerto 6543) para compatibilidad completa con EF Core.

**Archivos afectados:**
- `backend/LMS.API/appsettings.json`

---

### #10 — Botón de Google OAuth no se renderiza en producción

**Categoría:** Frontend / Google OAuth  
**Fecha:** ~2026-09-25  
**Gravedad:** 🟡 Medio

**Síntoma:**
El botón de "Iniciar sesión con Google" aparecía en desarrollo local (`localhost:4200`) pero **no se renderizaba** en la app de GitHub Pages.

**Causa:**
Google Identity Services (GSI) requiere que el dominio desde el que se llama esté autorizado en la consola de Google Cloud (`Orígenes JavaScript autorizados`). El dominio de GitHub Pages no estaba registrado.

**Solución aplicada:**
1. En **Google Cloud Console** → Credenciales OAuth 2.0 → se añadió `https://sneider m2.github.io` como origen autorizado
2. Se añadió `https://sneider m2.github.io/LMS_PRIMARIA` como URI de redirección autorizada
3. Se verificó que el `client_id` en el componente coincida con el registrado

**Archivos afectados:**
- `frontend/src/app/features/auth/login/login.component.ts`
- Configuración en Google Cloud Console (externo al código)

---

### #11 — JWT expirado retornaba `500` en lugar de `401`

**Categoría:** Backend / Autenticación  
**Fecha:** ~2026-09-18  
**Gravedad:** 🟡 Medio

**Síntoma:**
Cuando el token JWT expiraba, la API devolvía:
```json
{ "status": 500, "message": "Error interno del servidor" }
```
En lugar del esperado `401 Unauthorized`.

**Causa:**
El `GlobalExceptionMiddleware` capturaba excepciones de validación de JWT (`SecurityTokenExpiredException`) y las devolvía como error 500, porque se registraba antes del middleware de autenticación JWT de ASP.NET Core.

**Solución aplicada:**
Se reordenó el pipeline en `Program.cs` para que la autenticación JWT corra antes del `GlobalExceptionMiddleware`, y se añadió manejo explícito de `401` en el `JwtBearerEvents`:

```csharp
options.Events = new JwtBearerEvents {
  OnChallenge = context => {
    context.HandleResponse();
    context.Response.StatusCode = 401;
    context.Response.ContentType = "application/json";
    return context.Response.WriteAsync("{\"status\":401,\"message\":\"Token expirado o inválido.\"}");
  }
};
```

**Archivos afectados:**
- `backend/LMS.API/Program.cs`

---

### #12 — App en blanco en GitHub Pages por `base-href` incorrecto

**Categoría:** Frontend / Build  
**Fecha:** ~2026-09-23  
**Gravedad:** 🔴 Crítico

**Síntoma:**
Al desplegar el build de Angular en GitHub Pages, la app mostraba una **página en blanco** con errores de recursos 404 en la consola:
```
GET https://sneider m2.github.io/main-xxxxx.js 404
```

**Causa:**
Angular construye con `base-href="/"` por defecto. En GitHub Pages, la app vive en `/LMS_PRIMARIA/`, no en la raíz. Los scripts JS y CSS se buscaban en la raíz del dominio en lugar del subdirectorio correcto.

**Solución aplicada:**
El build debe incluir el flag `--base-href`:
```bash
npm run build -- --base-href "/LMS_PRIMARIA/"
```

Se documentó en los scripts de despliegue y se verificó que el `<base href="/LMS_PRIMARIA/">` quede en el `index.html` generado.

**Archivos afectados:**
- `frontend/package.json` (scripts de build)

---

### #13 — Dark mode no persistía entre recargas

**Categoría:** Frontend / UX  
**Fecha:** ~2026-09-25  
**Gravedad:** 🟢 Menor

**Síntoma:**
Al recargar la página, el tema volvía siempre al modo claro aunque el usuario hubiera seleccionado el modo oscuro previamente.

**Causa:**
El `ThemeService` aplicaba el tema pero no lo persistía en `localStorage`. Al recargar, Angular inicializaba el servicio con el estado por defecto (modo claro).

**Solución aplicada:**
```typescript
// theme.service.ts
setDark(isDark: boolean): void {
  this.isDarkMode.set(isDark);
  document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
  localStorage.setItem('theme', isDark ? 'dark' : 'light'); // ← persistencia
}

// Al inicializar:
const saved = localStorage.getItem('theme');
if (saved) this.setDark(saved === 'dark');
```

**Archivos afectados:**
- `frontend/src/app/core/services/theme.service.ts`

---

### #14 — `FileLogger` lanzaba excepción por ruta de logs en Render

**Categoría:** Backend / Logs  
**Fecha:** ~2026-09-22  
**Gravedad:** 🟡 Medio

**Síntoma:**
En producción (Render), el backend crasheaba al iniciar con:
```
UnauthorizedAccessException: Access to the path '/app/logs' is denied
```
o el directorio de logs no se creaba en el contenedor Docker.

**Causa:**
`FileLogger` intentaba crear el directorio `logs/` relativo al directorio de trabajo del proceso .NET, que en el contenedor Docker de Render no tenía permisos de escritura en la ruta calculada.

**Solución aplicada:**
Se configuró `FileLogger` para usar `AppContext.BaseDirectory` con fallback a `/tmp/logs` si hay error de permisos:

```csharp
private static string GetLogsDirectory() {
  var baseDir = Path.Combine(AppContext.BaseDirectory, "logs");
  try {
    Directory.CreateDirectory(baseDir);
    return baseDir;
  } catch {
    var tmpDir = Path.Combine(Path.GetTempPath(), "lms-logs");
    Directory.CreateDirectory(tmpDir);
    return tmpDir;
  }
}
```

**Archivos afectados:**
- `backend/LMS.API/Middleware/RequestLoggingMiddleware.cs`

---

### #15 — Render bloqueaba preflight OPTIONS de Angular

**Categoría:** Backend / CORS  
**Fecha:** ~2026-09-22  
**Gravedad:** 🔴 Crítico

**Síntoma:**
Peticiones `PUT` y `DELETE` desde Angular fallaban con:
```
Method OPTIONS is not allowed
```
Las peticiones `GET` y `POST` funcionaban pero las que requerían preflight (CORS pre-check) eran bloqueadas.

**Causa:**
El orden de middlewares en `Program.cs` era incorrecto. `UseCors()` se llamaba **después** de `UseAuthentication()` y `UseAuthorization()`, por lo que las peticiones preflight `OPTIONS` eran rechazadas antes de llegar a la política CORS.

**Solución aplicada:**
Se reordenó el pipeline para que `UseCors()` esté **antes** de la autenticación:

```csharp
// Program.cs — orden correcto
app.UseHttpsRedirection();
app.UseCors("AllowFrontend"); // ← PRIMERO
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

**Archivos afectados:**
- `backend/LMS.API/Program.cs`

---

### #16 — `root/index.html` desactualizado: GitHub Pages servía Service Worker v5

**Categoría:** Frontend / Deploy  
**Fecha:** 2026-10-06  
**Gravedad:** 🔴 Crítico

**Síntoma:**
```
AbortError: Failed to register a ServiceWorker for scope
('https://sneiderm2.github.io/LMS_PRIMARIA/')
with script ('https://sneiderm2.github.io/LMS_PRIMARIA/service-worker.js?v=5')
```
Aunque se había actualizado el Service Worker al v6 y corregido el `index.html` en `frontend/src/`, la app en producción seguia cargando el SW v5 antiguo (con `skipWaiting` y el loop de recargas).

**Causa:**
El flujo de deploy del proyecto es **manual**: el build genera archivos en `frontend/dist/lms-primaria/browser/` que deben copiarse a la raíz del repositorio (donde GitHub Pages sirve los archivos). Sin embargo, en varias sesiones de trabajo sólo se editaba `frontend/src/index.html` y se hacía push sin copiar el output del build a la raíz.

El archivo `index.html` de la raíz (el real de producción) permanecía con el código antiguo:
```html
<!-- Raíz del repo: aún con v=5 y window.location.reload() -->
<script>
  const swUrl = 'service-worker.js?v=5';
  registration.update();
  window.location.reload(); // ← esto causaba el loop
</script>
```

**Solución aplicada:**
Se ejecutó el build completo y se copiaron **todos** los archivos de dist a la raíz:
```powershell
npm run build -- --base-href "/LMS_PRIMARIA/"
Copy-Item -Path "frontend\dist\lms-primaria\browser\*" -Destination "." -Recurse -Force
git add -A
git push origin main
```
El nuevo `index.html` en la raíz ahora carga `service-worker.js?v=6` y el nuevo bundle `main-ZJEAHE2D.js` sin ningún `window.location.reload()`.

> 💡 **Leccion aprendida**: Siempre copiar el dist a la raíz después de cada build antes de hacer push para producción en GitHub Pages.

**Archivos afectados:**
- `/index.html` (raíz del repositorio)
- `service-worker.js` (raíz del repositorio)
- Todos los assets del build

---

### #17 — `http://example.com/avatar.png` en BD — Mixed Content + 404 en avatares

**Categoría:** Frontend / UX  
**Fecha:** 2026-10-06  
**Gravedad:** 🟡 Medio

**Síntoma:**
```
Mixed Content: The page was loaded over HTTPS, but requested an insecure
element 'http://example.com/avatar.png'. This request was automatically
upgraded to HTTPS.

GET https://example.com/avatar.png 404 (Not Found)
```
Los avatares de algunos usuarios en la tabla del panel de administrador no cargaban, mostrando iconos rotos.

**Causa:**
Algunos usuarios en la base de datos Supabase tenían el campo `avatar_url` con el valor placeholder `http://example.com/avatar.png` — valor de prueba que quedó guardado cuando se crearon las cuentas en etapas tempranas del desarrollo, antes de implementar la generación automática con `dicebear.com`. El template HTML del panel admin mostraba este valor sin ningún fallback:

```html
<!-- ❌ Sin fallback -->
<img [src]="user.avatarUrl" alt="Avatar" class="avatar-sm" />
```

**Solución aplicada:**
Se agregó un fallback condicional en el template que detecta la URL inválida y genera un avatar dinámico con `dicebear.com`:

```html
<img
  [src]="user.avatarUrl && !user.avatarUrl.includes('example.com')
    ? user.avatarUrl
    : 'https://api.dicebear.com/7.x/bottts/svg?seed=' + user.username"
  alt="Avatar" class="avatar-sm" />
```

**Archivos afectados:**
- `frontend/src/app/features/admin/admin-dashboard.component.html`

---

### #18 — Tab `.htaccess` del SystemInspector llamaba endpoint 404

**Categoría:** Frontend / System Inspector  
**Fecha:** 2026-10-06  
**Gravedad:** 🟡 Medio

**Síntoma:**
```
GET https://lms-primaria.onrender.com/api/systemfiles/htaccess 404 (Not Found)
GET https://lms-primaria.onrender.com/api/systemfiles/logs 401 (Unauthorized)
```
El `SystemInspectorComponent` mostraba una pestaña llamada **⋯ Directivas Servidor (.htaccess)** que al hacer clic (y también en el `ngOnInit`) intentaba cargar el endpoint `/api/systemfiles/htaccess`, que devuelve 404 en el backend .NET de Render.

**Causa:**
El `SystemInspectorComponent` fue diseñado originalmente para un stack PHP/Apache donde `.htaccess` es el archivo de configuración del servidor. Al migrar a .NET Kestrel, el endpoint `/api/systemfiles/htaccess` fue eliminado del backend (no tiene sentido en Kestrel), pero el componente frontend nunca se actualizó para reflejar eso. Adicionalmente, `ngOnInit` llamaba `loadLogsList()` que disparaba el 401 porque el JWT no se propagaba correctamente en el primer ciclo.

**Solución aplicada:**
Se ocultó la pestaña `.htaccess` del template del `SystemInspectorComponent` mediante un comentario, ya que el endpoint no existe en el backend .NET:

```html
<!-- Tab .htaccess ocultada: endpoint no disponible en backend .NET/Render -->
<!-- <button class="tab-btn" (click)="selectTab('htaccess')">...</button> -->
```

También se actualizó el texto descriptivo del header para no mencionar `.htaccess`:
```html
<!-- Antes -->
Monitoreo de seguridad, inspección estructurada de registros y directivas del servidor (.htaccess).
<!-- Después -->
Monitoreo de seguridad e inspección estructurada de registros del servidor Kestrel (.NET).
```

**Archivos afectados:**
- `frontend/src/app/features/admin/system-inspector/system-inspector.component.html`

---

### #19 — Timeout en deploy de Render por inicialización bloqueante de BD (`DbInitializer`)

**Categoría:** Backend / Render / Startup Lifecycle  
**Fecha:** 2026-10-14  
**Gravedad:** 🔴 Crítico

**Síntoma:**
```text
==> Deploying...
==> Timed out waiting for port 8080 to respond.
==> Health check failed. Container marked unhealthy.
```
El despliegue en Render pasaba de tardar ~1 minuto a más de 8 minutos o fallaba por timeout en el health-check, impidiendo que el backend se iniciara exitosamente tras nuevos commits.

**Causa:**
En `Program.cs`, el bloque de inicialización de la base de datos:
```csharp
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<LMSDbContext>();
    await DbInitializer.SeedAsync(context);
}
```
se ejecutaba sincrónicamente en la secuencia de arranque **antes** de llamar a `app.Run()`. Kestrel no abría el puerto HTTP (8080 en el contenedor Docker de Render) hasta que todas las consultas y comprobaciones de esquema contra el pooler de Supabase hubiesen finalizado. Al experimentar latencia de red hacia Supabase en la nube, el health check de Render expiraba antes de que Kestrel comenzara a escuchar peticiones entrantes.

**Solución aplicada:**
Se desacopló la inicialización de la base de datos de la apertura del socket HTTP utilizando el evento del ciclo de vida `app.Lifetime.ApplicationStarted.Register()`:
1. `app.Run()` se ejecuta de inmediato, permitiendo a Kestrel abrir el puerto 8080 y responder al health-check de Render en menos de 500 ms.
2. `DbInitializer.SeedAsync()` se ejecuta en segundo plano (`Task.Run()`) con un breve retardo de 200 ms para asegurar que el servidor esté 100% operativo.
3. Se integró un `CancellationTokenSource(TimeSpan.FromSeconds(25))` y captura global de excepciones (`try/catch`) para que cualquier demora o error de red en la BD no tumbe el servidor web.

```csharp
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(200);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var scope = app.Services.CreateScope();
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<LMSDbContext>();
            await DbInitializer.SeedAsync(context);
            app.Logger.LogInformation("✅ BD PostgreSQL inicializada correctamente (post-startup).");
        }
        catch (OperationCanceledException)
        {
            app.Logger.LogWarning("⚠️ DbInitializer cancelado por timeout (25s). Servidor sigue operativo.");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "⚠️ Error no crítico en inicialización de BD. El servidor continúa en línea.");
        }
    });
});
```

**Archivos afectados:**
- `backend/LMS.API/Program.cs`

---

### #20 — Latencia de login excesiva (~20s) por Cold Start en Render y consultas no indexadas

**Categoría:** Backend & Frontend / Rendimiento & Autenticación  
**Fecha:** 2026-10-14  
**Gravedad:** 🟡 Medio / Alto

**Síntoma:**
Al ingresar credenciales en la pantalla de inicio de sesión tras un período de inactividad, la petición `POST /api/auth/login` tardaba hasta 20 segundos en responder, transmitiendo una sensación de congelamiento o fallo al usuario.

**Causa:**
1. **Cold Start de Render:** Las instancias del plan gratuito se suspenden tras 15 minutos sin tráfico; la primera petición HTTP entrante levanta el contenedor desde cero.
2. **Chequeo redundante de columnas:** `DbInitializer.EnsureColumnsMigratedAsync` realizaba múltiples consultas secuenciales de comprobación de columnas en Supabase.
3. **Escaneo secuencial (Seq Scan):** La validación de credenciales realizaba `WHERE LOWER(username) = LOWER(@user)` sin contar con un índice funcional en PostgreSQL.
4. **Pool de conexiones sin mantener:** Cada nueva consulta tras reposo requería negociar un handshake SSL completo con el pooler de Supabase.

**Solución aplicada:**
Se implementó una estrategia integral de optimización y precalentamiento:
1. **Precalentamiento inteligente (Warmup):** En `LoginComponent.ngOnInit()`, se invoca `authService.warmup()`, disparando una petición `GET /api/ping` en segundo plano en cuanto se carga la pantalla de login. Mientras el usuario digita sus credenciales, Render se despierta en paralelo.
2. **Endpoint ultraligero `/api/ping`:** Diseñado en `Program.cs` para responder inmediatamente en memoria (~2 ms) sin tocar la base de datos.
3. **Indicador visual de sincronización:** Se integró en `login.component.html` una píldora reactiva con estados (`⏳ Sincronizando con el servidor...` / `⚡ Conexión instantánea activa`) y animaciones CSS de pulso.
4. **Fast-path en inicializador:** `DbInitializer.cs` ahora verifica la columna `session_token` en una única consulta; si ya existe, omite el resto del escaneo, reduciendo el arranque de 4.5s a ~130ms (mejora del 97%).
5. **Índice funcional en PostgreSQL:** Se creó el índice único `CREATE UNIQUE INDEX IF NOT EXISTS uq_usuarios_lower_username ON usuarios (LOWER(username));`.
6. **Connection Pooling afinado:** Parámetros optimizados en la cadena de conexión (`Port=5432` Session Mode, `Minimum Pool Size=2`, `Maximum Pool Size=15`, `Keepalive=30`, `Tcp Keepalive=true`).
7. **Timeout en reCAPTCHA:** Limitado a 4 segundos en `GoogleRecaptchaService` para evitar bloqueos por dependencias externas.

**Archivos afectados:**
- `backend/LMS.API/Program.cs`
- `backend/LMS.API/Data/DbInitializer.cs`
- `backend/LMS.API/appsettings.json`
- `frontend/src/app/core/services/auth.service.ts`
- `frontend/src/app/features/auth/login/login.component.ts`
- `frontend/src/app/features/auth/login/login.component.html`
- `frontend/src/app/features/auth/login/login.component.css`

---

### #21 — Desfase y traducción incompleta en vistas dinámicas de Angular (i18n)

**Categoría:** Frontend / Internacionalización (i18n)  
**Fecha:** 2026-10-12  
**Gravedad:** 🟡 Medio

**Síntoma:**
Al cambiar de idioma (Español, Inglés, Chino, Japonés, Portugués), componentes dinámicos como tablas, modales emergentes, toasts y tarjetas de cursos no se traducían o generaban parpadeos y desalineaciones visuales al navegar entre paneles.

**Causa:**
Las traducciones estáticas convencionales no interceptaban los elementos creados dinámicamente en el DOM tras la inicialización del componente ni los datos asíncronos recibidos desde los servicios REST.

**Solución aplicada:**
Se desarrolló un motor de traducción profunda en `LanguageService`:
1. **DOM Walker recursivo:** Recorre los nodos de texto de los elementos renderizados aplicando traducciones de forma contextual e inmediata.
2. **`MutationObserver` reactivo:** Escucha cambios e inserciones dinámicas en el DOM para traducir automáticamente elementos añadidos después de la carga inicial (como modales de sesión, confirmaciones y mensajes flotantes).
3. **Diccionario multilingüe estructurado:** Mapeo comprehensivo para Español (ES), Inglés (EN), Chino (ZH), Japonés (JA) y Portugués (PT).
4. **Persistencia en `localStorage`:** Almacena la selección del usuario para mantener la preferencia en futuras visitas y recargas.

**Archivos afectados:**
- `frontend/src/app/core/services/language.service.ts`
- `frontend/src/app/shared/components/language-selector/`
- Paneles de Docente, Estudiante y Administrador

---

### #22 — Bloqueo de inicio de sesión por fallo en reCAPTCHA (Localhost y Servidor)

**Categoría:** Seguridad / Frontend & Backend / reCAPTCHA  
**Fecha:** 2026-10-19  
**Gravedad:** 🔴 Alta (Bloqueaba acceso al sistema)

**Síntoma:**
Al intentar iniciar sesión o registrarse, el sistema indicaba *"Por favor completa la verificación de seguridad reCAPTCHA ("No soy un robot")"* o *"La verificación de seguridad (reCAPTCHA) falló o expiró"*, imposibilitando el acceso de usuarios, docentes y estudiantes.

**Causas identificadas:**
1. **Restricción de dominios en Google reCAPTCHA v2 (Localhost):** La clave pública de producción no tenía autorizado el dominio `localhost` en la consola de Google, mostrando el error interno *"Localhost is not supported by default. To enable it, add it to the list of supported domains"*, lo que impedía que el checkbox se activara en desarrollo local y bloqueaba la generación del token.
2. **Fail-Closed estricto sin clave en servidor:** Tras la purga de secretos, `Recaptcha:SecretKey` quedó vacía en `appsettings.json`. Si en Render no se había asignado manualmente la variable `Recaptcha__SecretKey`, el servicio rechazaba todas las peticiones con código 400.
3. **Sombreado de variables de entorno:** La expresión `_configuration["Recaptcha:SecretKey"] ?? Environment.GetEnvironmentVariable(...)` no evaluaba el fallback porque una cadena vacía `""` no es nula.
4. **Falta de soporte de bypass en frontend:** La API tenía soporte para `DEV_TEST_TOKEN` en desarrollo, pero el formulario de Angular exigía obligatoriamente la resolución de reCAPTCHA externo sin fallback para el desarrollador ni detección de bloqueadores de anuncios (adblockers).

**Solución aplicada:**
1. **Claves oficiales de prueba para desarrollo local:** Se configuró en `environment.ts` y `appsettings.Development.json` la clave oficial de prueba de Google reCAPTCHA v2 (`6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI`), que funciona sin restricciones en `localhost` y `127.0.0.1`.
2. **Resolución robusta con `FirstNonEmpty`:** En `GoogleRecaptchaService`, se evalúan las variables `RECAPTCHA_SECRET_KEY`, `Recaptcha__SecretKey` y la configuración ignorando cadenas vacías o espacios en blanco.
3. **Tolerancia y resiliencia en servidor:** Si el servidor en producción no tiene clave configurada pero el cliente envió un token válido (e interactuó con la interfaz, además de superar honeypot y rate limiting), el servicio registra una advertencia en logs en lugar de derribar el acceso a toda la institución escolar.
4. **Bypass automático y botón de desarrollo:** En Angular (`LoginComponent`), si `!environment.production`, se autocompleta con `DEV_TEST_TOKEN` o se permite activar la verificación local con un clic. Además, se añadió detección de bloqueo por adblockers con avisos claros y descriptivos.
5. **Renderizado dinámico por pestaña:** Se separó la inicialización de los widgets de login y registro para evitar problemas al renderizar dentro de contenedores ocultos (`hidden`).

> **Actualización 2026-10-25:** el punto 3 describía una tolerancia de producción que ya **no
> existe**. El código actual es **fail-closed**: si `Recaptcha:SecretKey` no está configurada,
> rechaza login y registro. El bypass solo funciona en Development y con
> `Recaptcha:AllowDevBypass=true`. Ver `SECURITY.md`.

**Archivos afectados:**
- [ICaptchaService.cs](file:///c:/Users/SNEIDER/Documents/LMS/backend/LMS.API/Services/ICaptchaService.cs)
- [appsettings.Development.json](file:///c:/Users/SNEIDER/Documents/LMS/backend/LMS.API/appsettings.Development.json)
- [GoogleRecaptchaServiceTests.cs](file:///c:/Users/SNEIDER/Documents/LMS/backend/LMS.API.Tests/GoogleRecaptchaServiceTests.cs)
- [environment.ts](file:///c:/Users/SNEIDER/Documents/LMS/frontend/src/environments/environment.ts)
- [login.component.ts](file:///c:/Users/SNEIDER/Documents/LMS/frontend/src/app/features/auth/login/login.component.ts)
- [login.component.html](file:///c:/Users/SNEIDER/Documents/LMS/frontend/src/app/features/auth/login/login.component.html)
- [login.component.css](file:///c:/Users/SNEIDER/Documents/LMS/frontend/src/app/features/auth/login/login.component.css)

---

### #22 — BOLA/IDOR en asignación de contenidos y matrícula escolar cruzada

**Categoría:** Backend / Seguridad & Autorización  
**Fecha:** 2026-10-28  
**Gravedad:** 🔴 Crítico

**Síntoma:**
Un docente malicioso o cuenta comprometida podía asociar contenidos (`CreateContent`) o asignar tareas a cursos que pertenecían a otros profesores manipulando el identificador numérico de curso (`CourseId`). De igual manera, un estudiante podía matricularse en cursos correspondientes a grados distintos al suyo alterando la llamada a `EnrollInCourse`.

**Causa:**
`TeacherController.cs` carecía de verificación de titularidad (`c.DocenteId == currentUser.Id` o rol Admin) al resolver cursos automáticos o validar pertenencia en la creación de contenidos. En `StudentController.cs`, la acción `EnrollInCourse` verificaba la existencia del curso pero no contrastaba su `GradoId` con el `GradoId` asignado en el perfil del estudiante.

**Solución aplicada:**
- En `TeacherController.cs`, se implementó validación de autorización con `CanManageCourseAsync(courseId, currentUser)` en `CreateContent` y resolución restringida de cursos por `DocenteId`.
- En `StudentController.cs`, se añadió una comprobación estricta: `if (course.GradoId != student.GradoId) return Forbid(...)`.
- Se crearon suites unitarias dedicadas en `TeacherControllerSecurityTests.cs` y `StudentControllerSecurityTests.cs`.

**Archivos afectados:**
- `backend/LMS.API/Controllers/TeacherController.cs`
- `backend/LMS.API/Controllers/StudentController.cs`
- `backend/LMS.API.Tests/TeacherControllerSecurityTests.cs`
- `backend/LMS.API.Tests/StudentControllerSecurityTests.cs`

---

### #23 — Persistencia de sesiones previas activas tras cambio de contraseña

**Categoría:** Backend & Frontend / Autenticación & Sesión  
**Fecha:** 2026-10-28  
**Gravedad:** 🔴 Alta

**Síntoma:**
Cuando un usuario cambiaba su contraseña tras sospechar un acceso no autorizado, los tokens JWT previamente emitidos para otros dispositivos permanecían vigentes hasta su tiempo natural de expiración (8 horas).

**Causa:**
El método `ChangePassword` en `AuthController.cs` actualizaba únicamente el hash de la contraseña (`PasswordHash`), sin rotar el campo `user.SessionToken`.

**Solución aplicada:**
- En `AuthController.cs`, se regeneró el identificador de sesión `user.SessionToken = Guid.NewGuid().ToString("N")` al cambiar la clave y se emitió un nuevo JWT firmado en la respuesta.
- En `auth.service.ts`, el cliente actualiza inmediatamente el token en `localStorage` tras la respuesta de éxito.
- Se verificó mediante prueba unitaria en `AuthControllerSecurityTests.cs` y `auth.service.spec.ts`.

**Archivos afectados:**
- `backend/LMS.API/Controllers/AuthController.cs`
- `frontend/src/app/core/services/auth.service.ts`
- `backend/LMS.API.Tests/AuthControllerSecurityTests.cs`
- `frontend/src/app/core/services/auth.service.spec.ts`

---

### #24 — Fuga potencial de credenciales JWT hacia recursos de terceros (CWE-200)

**Categoría:** Frontend / Seguridad HTTP  
**Fecha:** 2026-10-28  
**Gravedad:** 🟠 Media-Alta

**Síntoma:**
Cualquier llamada HTTP saliente a URLs que contuvieran subcadenas como `onrender.com` o `/api/` (ej. avatares externos o CDNs) recibía automáticamente la cabecera `Authorization: Bearer <token>`.

**Causa:**
La comprobación `isApiUrl` en `jwt.interceptor.ts` utilizaba `req.url.includes('onrender.com') || req.url.includes('/api/')`, coincidiendo con dominios de terceros ajenos a la escuela.

**Solución aplicada:**
- Se implementó la función `isTrustedApiUrl(url: string)` que compara estrictamente el origen canónico del backend (`getApiBaseUrl()`, `localhost:5000` o `https://lms-primaria.onrender.com`) y el prefijo `/api/`.
- Se crearon pruebas automatizadas en `jwt.interceptor.spec.ts` simulando URLs legítimas y maliciosas.

**Archivos afectados:**
- `frontend/src/app/core/interceptors/jwt.interceptor.ts`
- `frontend/src/app/core/interceptors/jwt.interceptor.spec.ts`

---

### #25 — Desbalance de contraste en Modo Oscuro, colisión de controles y enlaces `javascript:`

**Categoría:** Frontend / UI, Accesibilidad & CSP  
**Fecha:** 2026-10-28  
**Gravedad:** 🟡 Media

**Síntoma:**
1. En pantallas móviles (`<= 480px`), el botón flotante de tema recuperaba `position: fixed` por especificidad de Angular, solapándose sobre el botón del formulario de login.
2. Cajas y textos en los dashboards usaban colores fijos (`#0f172a`, `#e2e8f0`) que en modo oscuro resultaban invisibles o de contraste deficiente.
3. El uso de `href="javascript:void(0)"` generaba advertencias de accesibilidad y violaba directivas estrictas de CSP.

**Causa:**
Falta de variables semánticas en plantillas HTML y falta de sincronización del `<meta name="color-scheme">`.

**Solución aplicada:**
- Se adoptó `<meta name="color-scheme" content="light dark">` y se mapeó en CSS con `:root`.
- Se reemplazaron colores fijos en línea por variables CSS (`var(--text-main)`, `var(--bg-surface-alt)`, `var(--border-color)`).
- Se reemplazaron los enlaces por botones accesibles `<button type="button" class="btn-inline-link">`.
- Se añadió prueba de accesibilidad automatizada en `login.accessibility.spec.ts`.

**Archivos afectados:**
- `frontend/src/index.html`
- `frontend/src/index.production.html`
- `frontend/src/styles.css`
- `frontend/src/app/features/auth/login/login.component.html`
- `frontend/src/app/features/admin/admin-dashboard.component.html`
- `frontend/src/app/features/student/student-dashboard.component.html`
- `frontend/src/app/features/teacher/teacher-dashboard.component.html`
- `frontend/src/app/shared/components/theme-toggle/theme-toggle.component.css`
- `frontend/src/app/features/auth/login/login.accessibility.spec.ts`

---

| Categoría | Total de Errores |
|:---|:---:|
| 🔴 Backend / Base de Datos | 2 |
| 🔴 Backend / CORS | 2 |
| 🔴 Backend / Render / Startup Lifecycle | 3 |
| 🟡 Backend & Frontend / Rendimiento & Auth | 2 |
| 🔴 Frontend / SPA / GitHub Pages | 3 |
| 🔴 Frontend / PWA / Service Worker | 2 |
| 🟡 Frontend / Angular | 1 |
| 🔴 Backend / Autenticación & Sesión | 2 |
| 🟢 Frontend / UX | 1 |
| 🟡 Frontend / Deploy (GitHub Pages) | 1 |
| 🟡 Frontend / System Inspector | 1 |
| 🟢 Frontend / Avatares / Assets | 1 |
| 🟡 Frontend / Internacionalización (i18n) | 1 |
| 🔴 Seguridad / reCAPTCHA & Auth Bypass | 1 |
| 🔴 Backend / Autorización & IDOR | 1 |
| 🟠 Frontend / Seguridad HTTP & Tokens | 1 |
| 🟡 Frontend / Accesibilidad & Dark Mode | 1 |
| **Total** | **26** |

---

> **Mantenido por:** Sneider M. · LMS Primaria · 2026  
> _Este documento se actualiza conforme se encuentran y resuelven nuevos errores en el proyecto._

