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



| Categoría | Total de Errores |
|:---|:---:|
| 🔴 Backend / Base de Datos | 2 |
| 🔴 Backend / CORS | 2 |
| 🟡 Backend / Render / Hosting | 2 |
| 🔴 Frontend / SPA / GitHub Pages | 3 |
| 🔴 Frontend / PWA / Service Worker | 2 |
| 🟡 Frontend / Angular | 1 |
| 🔴 Backend / Autenticación | 1 |
| 🟢 Frontend / UX | 1 |
| 🟡 Frontend / Deploy (GitHub Pages) | 1 |
| 🟡 Frontend / System Inspector | 1 |
| 🟢 Frontend / Avatares / Assets | 1 |
| **Total** | **18** |

---

> **Mantenido por:** Sneider M. · LMS Primaria · 2026  
> _Este documento se actualiza conforme se encuentran y resuelven nuevos errores en el proyecto._
