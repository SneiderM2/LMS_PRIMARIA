# LMS Primaria — Plataforma Educativa para Básica Primaria 🎒✨

Sistema de Gestión de Aprendizaje (LMS) diseñado con arquitectura limpia en **.NET 8 Web API**, frontend moderno en **Angular 18** y persistencia en **PostgreSQL (Supabase Cloud)** mediante **Npgsql Entity Framework Core**. Desplegado como **PWA** (Progressive Web App) en **GitHub Pages** con backend en **Render**.

---

## 🌐 Despliegue en Producción

| Servicio | URL |
| :--- | :--- |
| **Frontend (GitHub Pages)** | `https://sneiderm2.github.io/LMS_PRIMARIA/` |
| **Backend API (Render)** | `https://lms-primaria.onrender.com/api` |
| **Health & Warmup API** | `https://lms-primaria.onrender.com/api/ping` |
| **Documentación Swagger** | `https://lms-primaria.onrender.com/swagger` |

---

## 🏗️ Stack Tecnológico

| Capa | Tecnología | Versión |
| :--- | :--- | :---: |
| **Backend** | ASP.NET Core Web API | .NET 8 (net8.0) |
| **ORM** | Entity Framework Core + Npgsql PostgreSQL | 8.0.11 |
| **Autenticación** | JWT Bearer + Google OAuth 2.0 | 8.0.11 / 1.76.0 |
| **Documentación API** | Swagger / OpenAPI (Swashbuckle) | 6.6.2 |
| **Hashing** | BCrypt.Net-Next | 4.0.3 |
| **Frontend** | Angular 18 (Standalone Components) | 18.x |
| **Base de datos** | PostgreSQL (Supabase Cloud / Pooler IPv4) | 16.x |
| **Internacionalización** | i18n Multilingüe en tiempo real | ES, EN, ZH, JA, PT |
| **Hosting Frontend** | GitHub Pages (SPA + PWA) | — |
| **Hosting Backend** | Render (Docker / Web Service) | — |
| **Exportación** | jsPDF + html2canvas + PapaParse | — |

---

## ✨ Funcionalidades Principales

### 🔐 Autenticación y Registro
- Login con usuario/contraseña + JWT Bearer automático
- **Precalentamiento inteligente (Warmup):** Ping invisible en segundo plano a `/api/ping` que despierta a Render al cargar la pantalla de login
- **Píldora interactiva de conexión:** Estado reactivo del servidor en tiempo real (`⏳ Sincronizando con el servidor...` / `⚡ Conexión instantánea activa`)
- **Búsqueda instantánea en BD:** Índice funcional PostgreSQL `idx_usuarios_lower_username` para validación case-insensitive inmediata
- Registro público con captcha matemático antibot y timeout seguro de 4s en Google reCAPTCHA
- Inicio de sesión con **Google OAuth 2.0** (Google Identity Services)
- Protección de rutas por rol: `Admin`, `Teacher`, `Student` (`RoleGuard`)
- Timeout de sesión automático con modal de alerta configurable

### 🌐 Soporte Multilingüe (i18n)
- **5 Idiomas disponibles:** Español 🇪🇸, Inglés 🇺🇸, Chino mandarín 🇨🇳, Japonés 🇯🇵 y Portugués 🇧🇷
- **Motor de traducción profunda:** DOM Walker recursivo y `MutationObserver` para traducir contenido estático y dinámico (modales, tablas, alertas, toasts)
- **Selector accesible:** Ubicado en la barra superior con persistencia de preferencia en `localStorage`

### 🎓 Panel de Estudiante
- Dashboard con materias inscritas, tareas y calificaciones
- Entrega de tareas con **Drag & Drop** de archivos
- Zona de subida con validación de tipo y tamaño

### 👩‍🏫 Panel de Docente
- Gestión completa de cursos y materias
- Matrícula de alumnos con 1 clic
- **Semáforo escolar** de desempeño: 🟢 Verde / 🟡 Amarillo / 🔴 Rojo
- Publicación de tareas, recursos y materiales educativos
- Calificación de entregas de estudiantes
- Exportación de reportes en **PDF** y **CSV**

### 🛡️ Panel de Administrador
- Métricas institucionales en tiempo real (usuarios, docentes, alumnos, cursos)
- **Directorio completo de usuarios** con filtros por rol, estado y búsqueda
- **Registro administrativo** de nuevos usuarios (Admin/Docente/Alumno)
- **Suspensión lógica (Soft Delete)** de cuentas sin pérdida de historial
- **CRUD de Materias y Cursos** institucional con asignación de docentes
- **Auditoría e Inspección de Logs** del sistema en tiempo real (`SystemInspector`) con cobertura de más de 50 categorías de eventos
- **Exportación de reportes** a PDF y CSV desde cualquier sección
- **💾 Descargar Backup BD** — genera y descarga un dump `.sql` completo de Supabase PostgreSQL
- **📥 Restaurar Backup BD** — sube un archivo `.sql` y restaura la base de datos desde el panel sin línea de comandos

### ⚡ Rendimiento y Arquitectura de Despliegue
- **Arranque asíncrono no bloqueante:** Kestrel abre el socket de escucha de inmediato al llamar a `app.Run()`; la verificación de esquema e inicialización (`DbInitializer.SeedAsync()`) se ejecuta en segundo plano vía `app.Lifetime.ApplicationStarted.Register()`, garantizando respuestas a health-checks de Render en `< 500ms`.
- **Fast-path de base de datos:** Verificación instantánea de columna `session_token` en Supabase que reduce el tiempo de chequeo de 4.5s a ~130ms (mejora del 97%).
- **Connection Pooling optimizado:** Supabase Pooler en Session Mode (Puerto 5432) con `Minimum Pool Size=2`, `Maximum Pool Size=15`, `Keepalive=30s` y `Tcp Keepalive=true`.

### 🎨 UX y Diseño
- **Modo Oscuro / Claro** con alternancia reactiva (persistido en localStorage)
- Animaciones de entrada, micro-animaciones y glassmorphism
- **PWA instalable** con Service Worker (soporte offline básico)
- Tipografías Fredoka + Nunito para público infantil
- Toasts flotantes de notificación para todas las operaciones
- Modal de política de tratamiento de datos personales

---

## 🖥️ Requisitos del Entorno (Desarrollo Local)

| Programa | Estado | Descripción |
| :--- | :---: | :--- |
| **.NET 8 SDK** | ✅ Requerido | v8.0.x / v10.0.x compatible |
| **Supabase PostgreSQL** | ☁️ Activo | Instancia cloud con Connection Pooler IPv4 |
| **Node.js v20/v24 & npm** | ✅ Requerido | Para compilar y servir el frontend Angular |

---

## 🚀 Guía de Ejecución Local

### PASO 1 — Cadena de Conexión a Supabase (PostgreSQL)

Abre `backend/LMS.API/appsettings.json` y confirma:

```json
"DefaultConnection": "Host=aws-0-us-east-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres...;Password=..."
```

### PASO 2 — Iniciar el Backend (.NET 8)

```bash
cd backend/LMS.API
dotnet run
```

- **API REST:** `http://localhost:5000`
- **Swagger Docs:** `http://localhost:5000/swagger`

### PASO 3 — Iniciar el Frontend (Angular 18)

```bash
cd frontend
npm install
npm run dev
```

Se servirá en: `http://localhost:4200`

### PASO 4 — Build para producción (GitHub Pages)

```bash
cd frontend
npm run build -- --base-href "/LMS_PRIMARIA/"
# Copiar dist/lms-primaria/browser/* a la raíz del repo
git push origin main
```

---

## 🧪 Endpoints Principales de la API

### Salud y Diagnóstico (`/api`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/ping` | Wake-up y health-check ultraligero en memoria (~2ms, sin BD) |
| `GET` | `/api/health` | Estado del servicio y verificación de conexión activa a PostgreSQL |

### Autenticación (`/api/auth`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `POST` | `/api/auth/register` | Registro con captcha matemático |
| `POST` | `/api/auth/login` | Login → emite JWT Bearer |
| `POST` | `/api/auth/google-login` | Login con Google OAuth 2.0 |
| `GET` | `/api/auth/me` | Perfil del usuario en sesión |

### Administración (`/api/admin`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/admin/metrics` | Métricas globales del sistema |
| `GET` | `/api/admin/users` | Listado con filtros de rol y estado |
| `POST` | `/api/admin/users` | Crear usuario administrativamente |
| `PUT` | `/api/admin/users/{id}` | Editar datos de usuario |
| `PUT` | `/api/admin/users/{id}/toggle-status` | Suspender / reactivar cuenta |
| `GET` | `/api/admin/courses` | Listado de todos los cursos |
| `POST` | `/api/admin/courses` | Crear curso institucional |
| `PUT` | `/api/admin/courses/{id}` | Editar curso |
| `PUT` | `/api/admin/courses/{id}/toggle-status` | Activar / desactivar curso |
| `GET` | `/api/admin/download-backup` | 💾 Genera y descarga dump `.sql` de Supabase |
| `POST` | `/api/admin/restore-backup` | 📥 Restaura la BD desde un archivo `.sql` |

### Docente (`/api/teacher`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/teacher/students` | Lista alumnos con materias |
| `POST` | `/api/teacher/students` | Crear nuevo alumno |
| `GET` | `/api/teacher/courses` | Materias del docente |
| `POST` | `/api/teacher/courses` | Crear materia |
| `POST` | `/api/teacher/courses/{id}/enroll` | Matricular alumno |
| `GET` | `/api/teacher/students-status` | Semáforo escolar |
| `GET` | `/api/teacher/metrics` | Métricas cuantitativas |
| `POST` | `/api/teacher/contents` | Publicar tarea o material |

### Estudiante (`/api/student`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/student/dashboard` | Materias, tareas y notas |
| `POST` | `/api/student/upload-assignment` | Entregar tarea con archivos |

### Auditoría (`/api/systemfiles`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/systemfiles/tree` | Árbol de archivos de log |
| `GET` | `/api/systemfiles/content` | Contenido en vivo de logs |

---

## 📁 Estructura del Proyecto

```
LMS/
│
├── 📂 backend/
│   └── 📂 LMS.API/
│       ├── 📂 Controllers/
│       │   ├── AdminController.cs         # Métricas, CRUD usuarios/cursos, backup y restore BD
│       │   ├── AuthController.cs          # Registro, login JWT, Google OAuth
│       │   ├── ContentsController.cs      # Publicación y descarga de contenidos
│       │   ├── StudentController.cs       # Dashboard alumno y entrega de tareas
│       │   ├── SystemFilesController.cs   # Auditoría de logs del sistema
│       │   └── TeacherController.cs       # Cursos, matrícula, semáforo, calificaciones
│       ├── 📂 Data/
│       │   ├── DbInitializer.cs           # Seeding de roles y grados
│       │   └── LMSDbContext.cs            # Modelo relacional Fluent API
│       ├── 📂 DTOs/                       # Contratos tipados Request/Response
│       ├── 📂 Entities/                   # Modelos de dominio (Usuario, Curso, Tarea…)
│       ├── 📂 Middleware/
│       │   ├── GlobalExceptionMiddleware.cs
│       │   ├── RequestLoggingMiddleware.cs
│       │   └── SecurityHeadersMiddleware.cs
│       ├── 📂 Services/
│       │   ├── AdminService.cs            # Lógica de backup/restore, gestión de usuarios y cursos
│       │   ├── SemaforoService.cs         # Algoritmo semáforo escolar
│       │   ├── TokenService.cs            # Generación JWT
│       │   └── LocalFileStorageService.cs # Almacenamiento de adjuntos
│       └── Program.cs                     # DI, CORS, JWT, Scalar, pipeline
│
├── 📂 frontend/
│   ├── 📂 src/
│   │   ├── 📂 app/
│   │   │   ├── 📂 core/
│   │   │   │   ├── 📂 guards/             # auth.guard, role.guard
│   │   │   │   ├── 📂 interceptors/       # jwt.interceptor
│   │   │   │   └── 📂 services/
│   │   │   │       ├── admin.service.ts   # CRUD usuarios/cursos, backup/restore
│   │   │   │       ├── auth.service.ts    # Login, registro, Google OAuth
│   │   │   │       ├── report-export.service.ts
│   │   │   │       ├── session-timeout.service.ts
│   │   │   │       ├── student.service.ts
│   │   │   │       ├── system-files.service.ts
│   │   │   │       ├── teacher.service.ts
│   │   │   │       └── theme.service.ts
│   │   │   ├── 📂 features/
│   │   │   │   ├── 📂 admin/
│   │   │   │   │   ├── admin-dashboard.component.*   # Panel con backup/restore BD
│   │   │   │   │   └── 📂 system-inspector/
│   │   │   │   ├── 📂 auth/login/
│   │   │   │   ├── 📂 student/
│   │   │   │   │   ├── student-dashboard.component.*
│   │   │   │   │   └── 📂 components/file-drop-zone/
│   │   │   │   └── 📂 teacher/
│   │   │   │       └── teacher-dashboard.component.*
│   │   │   └── 📂 shared/
│   │   │       ├── 📂 data-policy-modal/
│   │   │       ├── 📂 session-warning-modal/
│   │   │       └── 📂 theme-toggle/
│   │   ├── index.html                    # PWA + Service Worker registro
│   │   └── styles.css                    # Design system global con dark mode
│   └── 📂 public/
│       └── service-worker.js             # SW v6 (sin skipWaiting para evitar loops)
│
├── 404.html                              # Redirect SPA para GitHub Pages
└── README.md
```

---

## ⚙️ Configuración JWT

```json
"Jwt": {
  "SecretKey": "LmsPrimarySchoolSuperSecretKey2026!@#$%^&*()_+",
  "Issuer": "LMS.API",
  "Audience": "LMS.Client"
}
```

El frontend adjunta automáticamente el token en cada petición mediante el `JwtInterceptor`.

---

## 🔒 Middlewares de Seguridad y Logging

### `SecurityHeadersMiddleware`

| Cabecera | Valor |
| :--- | :--- |
| `X-Frame-Options` | `SAMEORIGIN` |
| `X-Content-Type-Options` | `nosniff` |
| `X-XSS-Protection` | `1; mode=block` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Content-Security-Policy` | Política restrictiva |
| `Permissions-Policy` | Bloquea cámara, micrófono, geo, pagos |

### `RequestLoggingMiddleware`

| Archivo | Contenido |
| :--- | :--- |
| `logs/access.log` | Toda petición HTTP: IP, método, ruta, status, tiempo |
| `logs/error.log` | Respuestas 4xx/5xx y excepciones con stack trace |
| `logs/app.log` | Eventos del servidor y logs manuales |

- Thread-safe con `SemaphoreSlim`
- Rotación automática: 5 MB por archivo, máximo 10 archivos

### `GlobalExceptionMiddleware`

Devuelve respuestas JSON estandarizadas para toda excepción no controlada.

---

## 📝 Changelog Completo

### v6.2.0 — 2026-10-14 _(versión actual)_

- 🚀 **Arranque asíncrono no bloqueante en Render:** `DbInitializer.SeedAsync()` desacoplado del inicio de Kestrel mediante `app.Lifetime.ApplicationStarted.Register()`. Deploys en Render reducidos de 8+ minutos a ~1 minuto sin bloqueos en health checks.
- ⚡ **Aceleración de Login & Precalentamiento (Warmup):** `LoginComponent` envía un ping invisible a `/api/ping` al abrir la pantalla, despertando a Render en background antes de enviar credenciales.
- 🟢 **Píldora interactiva de conexión en vivo:** Indicador visual dinámico en login (`⏳ Sincronizando con el servidor...` / `⚡ Conexión instantánea activa`) con estados reactivos.
- 🔍 **Índice Funcional en PostgreSQL:** Creación de `idx_usuarios_lower_username` para búsquedas case-insensitive sin Sequential Scan.
- 🗄️ **Fast-Path en DbInitializer & Connection Pooling:** Reducción de 4500ms a ~130ms (-97%) en comprobaciones de esquema, y pool configurado con `MinPool=2`, `MaxPool=15`, `Keepalive=30s`, `TcpKeepalive=true` en Session Mode (Puerto 5432).
- 🌐 **Soporte Multilingüe Completo (i18n):** Traducción profunda en 5 idiomas (ES, EN, ZH, JA, PT) con DOM Walker y `MutationObserver` reactivo para elementos dinámicos.
- 📊 **Auditoría con Cobertura Total:** Más de 50 categorías de eventos mapeadas y registradas en el sistema de logs (`access.log`, `app.log`, `error.log`).

### v6.0.0 — 2026-10-06

- ✅ **Fix definitivo: loop de recarga del Service Worker** — Eliminados `skipWaiting()` y `clients.claim()` del SW que causaban un ciclo infinito `activate → clients.claim → controllerchange → reload → activate…`
- ✅ **SW v6** — Nuevo service worker estable sin auto-recargas, con Network-First para JS/CSS y Cache-First para estáticos
- ✅ **index.html** — Eliminado todo código de `onupdatefound` y `window.location.reload()` del registro del SW

### v5.5.0 — 2026-10-06

- ✅ **Botón "📥 Restaurar Backup BD"** — Panel de administrador permite subir un archivo `.sql` y restaurar la base de datos PostgreSQL en Supabase desde la UI sin línea de comandos
- ✅ **Botón "💾 Descargar Backup BD"** — Genera y descarga dump completo `.sql` de todas las tablas de Supabase con timestamp en el nombre
- ✅ **`AdminController.cs`** — Nuevos endpoints `GET /api/admin/download-backup` y `POST /api/admin/restore-backup`
- ✅ **`AdminService.cs`** — Implementada lógica de conexión directa a Supabase PostgreSQL con `Npgsql` para ejecutar scripts SQL arbitrarios en restauración
- ✅ **Eliminado `window.location.reload()`** del flujo de restauración para evitar 404 en SPA de GitHub Pages; reemplazado por recarga en memoria (`loadMetrics`, `loadUsers`, `loadAdminCourses`)
- ✅ **`404.html`** — Agregado para manejo correcto de rutas SPA en GitHub Pages

### v5.0.0 — 2026-10-05

- ✅ **CRUD completo de Cursos** en el panel de administrador — Crear, editar, activar/desactivar materias institucionales con asignación de docentes
- ✅ **CRUD de Usuarios mejorado** — Edición de datos, cambio de contraseña opcional, filtros por rol y estado
- ✅ **Sección "Auditoría & Logs"** en panel admin con `SystemInspectorComponent` embebido
- ✅ **Exportación de reportes** PDF y CSV desde las 3 pestañas del panel admin (Usuarios, Materias, Auditoría)
- ✅ **Métricas institucionales** — 4 tarjetas con conteo de usuarios activos, docentes, alumnos y cursos
- ✅ **Toast flotante** unificado para feedback de operaciones asíncronas

### v4.0.0 — 2026-09-25

- ✅ **Google OAuth 2.0** — Login con cuenta de Google usando Google Identity Services (GSI)
- ✅ **reCAPTCHA v2** — Captcha matemático antibot en el registro público
- ✅ **Dark Mode** — Alternancia reactiva claro/oscuro persistida en localStorage con `ThemeService`
- ✅ **Modal de Política de Datos** — Cumplimiento de protección de datos personales
- ✅ **`session-warning-modal`** — Alerta interactiva antes de que expire la sesión JWT
- ✅ **PWA** — Service Worker, `manifest.webmanifest`, instalación como app en móviles y escritorio

### v3.0.0 — 2026-09-20

- ✅ **Despliegue en GitHub Pages** — Build Angular con `--base-href`, GitHub Actions workflow
- ✅ **Backend en Render** — Dockerfile configurado, variables de entorno de Supabase
- ✅ **Migración de MySQL a PostgreSQL (Supabase)** — Reemplazo de Pomelo por Npgsql EF Core
- ✅ **`SystemFilesController`** — Auditoría de logs del servidor en tiempo real desde el panel admin
- ✅ **`SystemInspectorComponent`** — Visor de árbol de archivos y contenido de logs en el frontend

### v2.0.0 — 2026-09-15

- ✅ **Dashboard del Estudiante** — Materias inscritas, tareas pendientes, estado de entregas y notas
- ✅ **Drag & Drop de archivos** (`FileDrop ZoneComponent`) para entrega de tareas
- ✅ **Semáforo escolar** (`SemaforoService`) — Algoritmo de desempeño 🟢/🟡/🔴 basado en entregas y fechas límite
- ✅ **Exportación PDF** de reportes del docente con `jsPDF` + `html2canvas`
- ✅ **Exportación CSV** con `PapaParse`
- ✅ **`ReportExportService`** — Servicio centralizado de exportación con tablas y tarjetas de resumen

### v1.1.0 — 2026-09-11

- ✅ `SecurityHeadersMiddleware` — Cabeceras HTTP de seguridad
- ✅ `RequestLoggingMiddleware` + `FileLogger` — Logging a archivos `.log` con rotación automática
- ✅ `GlobalExceptionMiddleware` — Manejo estandarizado de excepciones
- 🗑️ Eliminados `backend/.htaccess` y `backend/helpers/logger.php` (incompatibles con .NET/Kestrel)

### v1.0.0 — 2026-09-05 _(Sprint inicial)_

- ✅ Arquitectura base .NET 10 Web API + Angular 18 Standalone
- ✅ Autenticación JWT con `AuthController` y `TokenService`
- ✅ Panel de Docente — CRUD de alumnos, cursos y matrícula
- ✅ `LMSDbContext` con Fluent API en snake_case
- ✅ Seeding automático de roles (`Admin`, `Teacher`, `Student`) y grados (1° a 6°)
- ✅ Interceptor JWT automático en Angular

---

> **Desarrollado por:** Sneider M. · LMS Primaria · 2026
