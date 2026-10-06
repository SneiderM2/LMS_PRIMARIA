# LMS Primaria — Plataforma Educativa para Básica Primaria 🎒✨

Sistema de Gestión de Aprendizaje (LMS) diseñado con arquitectura limpia en **.NET 10 Web API**, frontend moderno en **Angular 18** y persistencia en **PostgreSQL (Supabase Cloud)** mediante **Npgsql Entity Framework Core**. Desplegado como **PWA** (Progressive Web App) en **GitHub Pages** con backend en **Render**.

---

## 🌐 Despliegue en Producción

| Servicio | URL |
| :--- | :--- |
| **Frontend (GitHub Pages)** | `https://sneider m2.github.io/LMS_PRIMARIA/` |
| **Backend API (Render)** | `https://lms-primaria.onrender.com/api` |
| **Documentación Scalar** | `https://lms-primaria.onrender.com/scalar/v1` |

---

## 🏗️ Stack Tecnológico

| Capa | Tecnología | Versión |
| :--- | :--- | :---: |
| **Backend** | ASP.NET Core Web API | .NET 10 |
| **ORM** | Entity Framework Core + Npgsql PostgreSQL | 10.0.3 / 10.0.12 |
| **Autenticación** | JWT Bearer + Google OAuth 2.0 | 9.0.0 |
| **Documentación API** | Scalar (reemplaza Swagger UI) | 2.6.0 |
| **Hashing** | BCrypt.Net-Next | 4.0.3 |
| **Frontend** | Angular 18 (Standalone Components) | 18.x |
| **Base de datos** | PostgreSQL (Supabase Cloud / Pooler IPv4) | 16.x |
| **Hosting Frontend** | GitHub Pages (SPA + PWA) | — |
| **Hosting Backend** | Render (Docker / Web Service) | — |
| **Exportación** | jsPDF + html2canvas + PapaParse | — |

---

## ✨ Funcionalidades Principales

### 🔐 Autenticación y Registro
- Login con usuario/contraseña + JWT Bearer automático
- Registro público con captcha matemático antibot
- Inicio de sesión con **Google OAuth 2.0** (Google Identity Services)
- Protección de rutas por rol: `Admin`, `Teacher`, `Student` (`RoleGuard`)
- Timeout de sesión automático con modal de alerta configurable

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
- **Auditoría e Inspección de Logs** del sistema en tiempo real (`SystemInspector`)
- **Exportación de reportes** a PDF y CSV desde cualquier sección
- **💾 Descargar Backup BD** — genera y descarga un dump `.sql` completo de Supabase PostgreSQL
- **📥 Restaurar Backup BD** — sube un archivo `.sql` y restaura la base de datos desde el panel sin línea de comandos

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
| **.NET 10 SDK** | ✅ Requerido | v10.0.400 |
| **Supabase PostgreSQL** | ☁️ Activo | Instancia cloud con Connection Pooler IPv4 |
| **Node.js v24 & npm 11** | ✅ Requerido | Para compilar y servir el frontend Angular |

---

## 🚀 Guía de Ejecución Local

### PASO 1 — Cadena de Conexión a Supabase (PostgreSQL)

Abre `backend/LMS.API/appsettings.json` y confirma:

```json
"DefaultConnection": "Host=...supabase.com;Port=5432;Database=postgres;Username=postgres...;Password=..."
```

### PASO 2 — Iniciar el Backend (.NET 10)

```bash
cd backend/LMS.API
dotnet run
```

- **API REST:** `http://localhost:5000`
- **Scalar Docs:** `http://localhost:5000/scalar/v1`

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

### v6.0.0 — 2026-10-06 _(versión actual)_

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
