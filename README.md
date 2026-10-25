# LMS Primaria — Plataforma Educativa para Básica Primaria 🎒✨

Sistema de Gestión de Aprendizaje (LMS) diseñado con arquitectura limpia en **.NET 8 Web API**, frontend moderno en **Angular 18** y persistencia en **PostgreSQL (Supabase Cloud)** mediante **Npgsql Entity Framework Core**. Desplegado como **PWA** (Progressive Web App) en **GitHub Pages** con backend en **Render**.

---

## 🌐 Despliegue en Producción

| Servicio | URL |
| :--- | :--- |
| **Frontend (GitHub Pages)** | `https://sneiderm2.github.io/LMS_PRIMARIA/` |
| **Backend API (Render)** | `https://lms-primaria.onrender.com/api` |
| **Health & Warmup API** | `https://lms-primaria.onrender.com/api/ping` |
| **Documentación Swagger** | Solo en desarrollo: `http://localhost:5000/swagger` (deshabilitado en producción) |

---

## 🏗️ Stack Tecnológico

| Capa | Tecnología | Versión |
| :--- | :--- | :---: |
| **Backend** | ASP.NET Core Web API | .NET 8 (net8.0) — EOL 10/11/2026 |
| **ORM** | Entity Framework Core + Npgsql PostgreSQL | 8.0.11 |
| **Autenticación** | JWT Bearer + Google OAuth 2.0 | 8.0.11 / 1.76.0 |
| **Documentación API** | Swagger / OpenAPI (Swashbuckle) | 6.6.2 |
| **Hashing** | BCrypt.Net-Next | 4.0.3 |
| **Frontend** | Angular 18 (Standalone Components) | 18.x — EOL |
| **Base de datos** | PostgreSQL (Supabase Cloud / Pooler IPv4) | 16.x |
| **Internacionalización** | i18n Multilingüe en tiempo real | ES, EN, ZH, JA, PT |
| **Hosting Frontend** | GitHub Pages (SPA + PWA) | — |
| **Hosting Backend** | Render (Docker / Web Service) | — |
| **Exportación** | jsPDF + html2canvas + PapaParse | — |
| **Cumplimiento** | Ley 1581 de 2012, Decreto 1074 de 2015, MSPI (MinTIC), NTC 5854, ISO/IEC 25010 | — |

---

## 🛡️ Seguridad y cumplimiento normativo

La plataforma incorpora controles alineados con la **Ley 1581 de 2012**, el **Decreto 1074 de 2015**,
los lineamientos de la **SIC**, el **MSPI** de MinTIC, **ISO/IEC 27001**, **ISO/IEC 25010** y **NTC 5854**.

| Tema | Implementación | Documento |
| :--- | :--- | :--- |
| Secretos | Variables de entorno + `appsettings.Local.json` ignorado por Git y Docker | [SETUP_SECRETS.md](SETUP_SECRETS.md) |
| Consentimiento | Evidencia firmada (HMAC), versión de política y autorización de acudiente | [Política](frontend/public/privacidad.html) |
| Derechos del titular | Descarga de datos, rectificación y supresión/anonimización | `/api/privacy/*` |
| Archivos | Almacenamiento privado, validación de firma binaria y descarga autorizada | `FilesController` |
| Autorización | Roles canónicos y validación de pertenencia de cursos (anti-IDOR) | `SECURITY.md` |
| Auditoría | Logs con redacción de datos sensibles y retención configurable | `/api/admin/audit-logs` |
| Incidentes | Plan de respuesta y notificación | [Plan](docs/PLAN_RESPUESTA_INCIDENTES.md) |
| Riesgos | Matriz y plan de tratamiento | [Matriz](docs/MATRIZ_RIESGOS.md) |
| Calidad y accesibilidad | ISO/IEC 25010 y NTC 5854 | [Calidad](docs/CALIDAD_Y_ACCESIBILIDAD.md) |
| Cumplimiento | Trazabilidad norma → control | [Matriz normativa](docs/CUMPLIMIENTO_NORMATIVO_CO.md) |

> ⚠️ **Antes de desplegar:** rote la contraseña de la base de datos, la clave JWT y la clave de
> reCAPTCHA (estuvieron versionadas), cambie las contraseñas de las cuentas demo y complete los datos
> del responsable del tratamiento. Ver [SETUP_SECRETS.md](SETUP_SECRETS.md).

### Estado de seguridad (auditoría 2026-10)

- La API **no arranca** sin `Jwt:SecretKey` (≥48 caracteres) ni sin `Privacy:EvidenceHmacKey`
  (≥32 caracteres, distinta de la JWT). Ya no existen claves de respaldo en el código.
- El CAPTCHA es **fail-closed**: si `Recaptcha__SecretKey` no está configurada en producción, el
  login y el registro se rechazan. El bypass solo existe en Development y debe habilitarse
  explícitamente con `Recaptcha__AllowDevBypass=true`.
- La sesión única y la aceptación vigente de la política se validan en el servidor para cada
  petición autenticada, no solo en el frontend.
- `Storage:Provider=Local` sigue disponible para desarrollo, pero en producción el disco de Render
  es efímero. El servicio actual usa el plan Free, que **no admite discos persistentes**. El bucket
  privado `lms-uploads` ya existe en Supabase (25 MB), pero la API todavía reporta `storage: "Local"`
  hasta configurar `Storage__Provider=Supabase`, la URL y la Service Role Key en Render, y desplegar.
  Si se migra a un plan con disco, use `Storage__RootPath` apuntando al disco montado. El fallback
  silencioso de Supabase a local está deshabilitado (`Storage:AllowLocalFallback=false`).
- **Deuda de plataforma:** .NET 8 alcanza su fin de soporte el **10/11/2026** y Angular 18 ya está
  fuera de soporte. La migración mayor no se ejecutó en esta auditoría; debe planificarse antes de
  la fecha de EOL de .NET. Mientras tanto, `npm audit --omit=dev` reporta vulnerabilidades altas y
  moderadas de Angular 18 sin corrección dentro de esa versión mayor.
- La anonimización por inactividad está **deshabilitada y en modo simulación** por defecto. Actívela
  solo cuando el responsable del tratamiento apruebe la retención declarada
  (`Privacy:EnableInactivityAnonymization` y `Privacy:InactivityAnonymizationDryRun`).

---

## ✨ Funcionalidades Principales

### 🔐 Autenticación y Registro
- Login con usuario/contraseña + JWT Bearer automático
- **Precalentamiento inteligente (Warmup):** Ping invisible en segundo plano a `/api/ping` que despierta a Render al cargar la pantalla de login
- **Píldora interactiva de conexión:** Estado reactivo del servidor en tiempo real (`⏳ Sincronizando con el servidor...` / `⚡ Conexión instantánea activa`)
- **Búsqueda instantánea en BD:** Índice funcional único PostgreSQL `uq_usuarios_lower_username` para validación case-insensitive inmediata
- Registro público con reCAPTCHA v2, honeypot y **autorización del acudiente** para menores de edad
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
- **💾 Descargar Backup BD** — genera un dump `.sql` de Supabase; contiene datos personales: debe cifrarse y almacenarse con acceso restringido
- **📥 Restaurar Backup BD** — deshabilitado por defecto para evitar ejecución remota de SQL; el procedimiento seguro con `psql` está en [OPERACION_BACKUP.md](docs/OPERACION_BACKUP.md)

### ⚡ Rendimiento y Arquitectura de Despliegue
- **Arranque asíncrono no bloqueante:** Kestrel abre el socket de escucha de inmediato al llamar a `app.Run()`; la verificación de esquema e inicialización (`DbInitializer.SeedAsync()`) se ejecuta en segundo plano vía `app.Lifetime.ApplicationStarted.Register()`, garantizando respuestas a health-checks de Render en `< 500ms`.
- **Fast-path de base de datos:** verificación instantánea de la columna `anonymized_at` (marcador de esquema actual) que reduce el chequeo de 4.5 s a ~130 ms (mejora del 97 %).
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

### PASO 1 — Configurar secretos (nunca en el repositorio)

El proyecto **no incluye credenciales**. Crea el archivo local (ignorado por Git):

```powershell
Copy-Item backend\LMS.API\appsettings.Example.json backend\LMS.API\appsettings.Local.json
```

Completa `ConnectionStrings:DefaultConnection`, `Jwt:SecretKey` (mínimo 48 caracteres, recomendado 64),
`Recaptcha:SecretKey`, `Privacy:EvidenceHmacKey` (mínimo 32 caracteres, distinta de la JWT),
`Registration:TeacherInviteCode` y `Supabase:ProjectRef`. En producción usa variables de entorno
(`ConnectionStrings__DefaultConnection`, `Jwt__SecretKey`, etc.). `appsettings.Local.json` solo se
carga en Development. Detalle completo en [SETUP_SECRETS.md](SETUP_SECRETS.md).

### PASO 2 — Iniciar el Backend (.NET 8)

```bash
cd backend/LMS.API
dotnet run
```

- **API REST:** `http://localhost:5000`
- **Swagger Docs:** `http://localhost:5000/swagger` (solo Development)

El perfil `LMS.API` de `Properties/launchSettings.json` establece `ASPNETCORE_ENVIRONMENT=Development`.
Las cuentas demo **ya no se crean automáticamente**; usa el panel administrativo o habilita
`Seed:Enabled=true` con contraseñas fuertes en un entorno controlado.

### PASO 3 — Iniciar el Frontend (Angular 18)

```bash
cd frontend
npm ci
npm start
```

Se servirá en: `http://localhost:4200`

### PASO 4 — Build para producción (GitHub Pages)

```bash
cd frontend
npm run build -- --base-href "/LMS_PRIMARIA/"
```

El flujo `.github/workflows/deploy.yml` compila y publica automáticamente en GitHub Pages al hacer
push a `main` (solo cuando cambian archivos de `frontend/`).

### PASO 5 — Pruebas y verificación

```powershell
# Pruebas unitarias del backend (55)
dotnet test backend\LMS.API.Tests\LMS.API.Tests.csproj -c Release

# Pruebas unitarias del frontend (6, ChromeHeadless)
cd frontend
npm test

# Con el backend en ejecución: cabeceras, CORS, privacidad y archivos
powershell -ExecutionPolicy Bypass -File scripts\security_smoke_test.ps1
```

---

## 🧪 Endpoints Principales de la API

### Salud y Diagnóstico (`/api`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/ping` | Wake-up y health-check ultraligero en memoria (~2ms, sin BD) |
| `GET` | `/api/health` | Estado del servicio y verificación de conexión activa a PostgreSQL |
| `GET` | `/api/health/live` | Liveness probe sin acceso a base de datos |

### Autenticación (`/api/auth`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `POST` | `/api/auth/register` | Registro de estudiantes (con autorización de acudiente) y docentes con código de invitación. **No crea administradores.** |
| `POST` | `/api/auth/login` | Login → emite JWT Bearer |
| `POST` | `/api/auth/google-login` | Login con Google OAuth 2.0 (solo cuentas registradas y token validado) |
| `GET` | `/api/auth/me` | Perfil del usuario en sesión |
| `POST` | `/api/auth/accept-data-policy` | Aceptación de política con evidencia (compatibilidad) |
| `POST` | `/api/auth/logout` | Revoca la sesión |
| `GET` | `/api/auth/validate-session` | Verifica sesión única |

### Privacidad (`/api/privacy`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/privacy/policy` | Política y aviso de privacidad (público) |
| `POST` | `/api/privacy/consent` | Registra consentimiento firmado y autorización de acudiente |
| `GET` | `/api/privacy/my-data` | Acceso y portabilidad (JSON descargable) |
| `PUT` | `/api/privacy/my-data` | Rectificación de nombres y apellidos |
| `POST` | `/api/privacy/delete-account` | Supresión con anonimización y eliminación de archivos |

### Archivos (`/api/files`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/files/{**key}` | Descarga autenticada y autorizada de tareas y recursos (JWT obligatorio) |

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
| `GET` | `/api/admin/backup-database` | 💾 Genera y descarga dump `.sql` de Supabase (contiene datos personales: almacenar cifrado) |
| `POST` | `/api/admin/restore-backup` | 📥 Restauración **deshabilitada por defecto** (`Features:EnableDatabaseRestore=false`) |
| `GET` | `/api/admin/compliance` | Resumen de cumplimiento: cobertura de consentimiento y alertas |
| `GET` | `/api/admin/audit-logs` | Consulta paginada de la auditoría |

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
│       │   ├── AdminController.cs         # Métricas, CRUD usuarios/cursos, backup, cumplimiento
│       │   ├── AuthController.cs          # Registro, login JWT, Google OAuth
│       │   ├── ContentsController.cs      # Publicación y descarga de contenidos
│       │   ├── FilesController.cs         # Descarga autenticada de adjuntos
│       │   ├── PrivacyController.cs       # Consentimiento, acceso, rectificación y supresión
│       │   ├── StudentController.cs       # Dashboard alumno y entrega de tareas
│       │   ├── SystemFilesController.cs   # Auditoría de logs del sistema
│       │   └── TeacherController.cs       # Cursos, matrícula, semáforo, calificaciones
│       ├── 📂 Configuration/
│       │   └── PrivacyOptions.cs          # Política, retención, almacenamiento y registro
│       ├── 📂 Data/
│       │   ├── DbInitializer.cs           # Roles/grados y cuentas demo opcionales
│       │   └── LMSDbContext.cs            # Modelo relacional Fluent API
│       ├── 📂 DTOs/                       # Contratos tipados Request/Response
│       ├── 📂 Entities/                   # Modelos de dominio (Usuario, Curso, Tarea…)
│       ├── 📂 Middleware/
│       │   ├── GlobalExceptionMiddleware.cs
│       │   ├── RequestLoggingMiddleware.cs # Logs con redacción de datos sensibles
│       │   ├── SecurityHeadersMiddleware.cs
│       │   └── SingleSessionMiddleware.cs
│       ├── 📂 Services/
│       │   ├── AdminService.cs            # Lógica de backup/restore y gestión institucional
│       │   ├── ConsentService.cs          # Evidencia de consentimiento firmada (HMAC)
│       │   ├── FileSignatureValidator.cs  # Validación de firma binaria de archivos
│       │   ├── LocalFileStorageService.cs # Almacenamiento privado de adjuntos
│       │   ├── PasswordPolicy.cs          # Política de contraseñas
│       │   ├── SemaforoService.cs         # Algoritmo semáforo escolar
│       │   ├── TokenService.cs            # Generación JWT
│       │   └── ...
│       ├── 📂 Properties/launchSettings.json
│       ├── appsettings.json               # Configuración sin secretos
│       ├── appsettings.Example.json       # Plantilla de configuración local
│       └── Program.cs                     # DI, CORS, JWT, rate limiting, pipeline
│
├── 📂 docs/
│   ├── CUMPLIMIENTO_NORMATIVO_CO.md       # Matriz norma → control
│   ├── POLITICA_TRATAMIENTO_DATOS_PERSONALES.md
│   ├── MODELO_SEGURIDAD.md                # MSPI / ISO 27001 / OWASP ASVS
│   ├── MATRIZ_RIESGOS.md
│   ├── PLAN_RESPUESTA_INCIDENTES.md
│   ├── OPERACION_BACKUP.md
│   └── CALIDAD_Y_ACCESIBILIDAD.md         # ISO/IEC 25010 y NTC 5854
│
├── 📂 frontend/
│   ├── 📂 src/
│   │   ├── 📂 app/
│   │   │   ├── 📂 core/
│   │   │   │   ├── 📂 guards/             # auth.guard, role.guard
│   │   │   │   ├── 📂 interceptors/       # jwt.interceptor
│   │   │   │   └── 📂 services/
│   │   │   │       ├── auth.service.ts
│   │   │   │       ├── privacy.service.ts # Derechos del titular
│   │   │   │       ├── file-download.service.ts
│   │   │   │       └── ...
│   │   │   └── 📂 shared/
│   │   │       ├── 📂 data-policy-modal/  # Consentimiento con acudiente
│   │   │       └── ...
│   │   └── index.html
│   └── 📂 public/
│       ├── privacidad.html                # Política pública
│       ├── .well-known/security.txt
│       └── service-worker.js
│
├── SECURITY.md                            # Divulgación y controles
├── SETUP_SECRETS.md                       # Configuración y rotación de secretos
├── Dockerfile
└── README.md
```

---

## ⚙️ Configuración JWT

La clave **no** está en el repositorio. Se configura por entorno:

```powershell
# Desarrollo (appsettings.Local.json o User Secrets)
$env:Jwt__SecretKey = "<clave aleatoria de 64+ caracteres>"

# Producción (Render → Environment)
Jwt__SecretKey=<clave aleatoria de 64+ caracteres>
Jwt__Issuer=LMS.API
Jwt__Audience=LMS.Client
Jwt__ExpirationMinutes=480
```

El backend rechaza el arranque si la clave falta, tiene menos de 48 caracteres o coincide con un
valor de ejemplo conocido. El frontend adjunta el token en cada petición mediante `JwtInterceptor`;
el token vencido se descarta automáticamente y la sesión única se valida en el servidor.

---

## 🔒 Middlewares de Seguridad y Logging

### `SecurityHeadersMiddleware`

| Cabecera | Valor |
| :--- | :--- |
| `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'; sandbox` |
| `X-Frame-Options` | `DENY` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `no-referrer` |
| `Cross-Origin-Opener-Policy` | `same-origin` |
| `Cross-Origin-Resource-Policy` | `same-site` |
| `Permissions-Policy` | Bloquea cámara, micrófono, geolocalización, pagos, USB, etc. |
| `Cache-Control` | `no-store` en rutas con datos personales |

### `RequestLoggingMiddleware`

| Archivo | Contenido |
| :--- | :--- |
| `logs/access.log` | Petición HTTP: IP, método, ruta, status, tiempo (sin tokens ni contraseñas) |
| `logs/error.log` | Respuestas 4xx/5xx y excepciones con stack trace |
| `logs/app.log` | Eventos del servidor y logs manuales |
| `logs/app-audit.log` | Auditoría estructurada JSON por evento |

- Thread-safe con `SemaphoreSlim`
- Rotación automática: 5 MB por archivo, máximo 10 archivos
- Redacción de `token`, `password`, `secret`, `authorization`, `captcha` y parámetros equivalentes
- Retención recomendada: 90 días (`Privacy:LogRetentionDays`)

### `GlobalExceptionMiddleware`

Devuelve respuestas JSON estandarizadas con `traceId`; en producción **no** expone mensajes ni
trazas internas.

### Limitación de solicitudes

| Política | Límite |
| :--- | :--- |
| Autenticación | 30 solicitudes/minuto por IP |
| Subidas | 60 solicitudes/hora por IP |
| Global | 600 solicitudes/minuto por IP |

---

## 📚 Documentación de cumplimiento

| Documento | Contenido |
| :--- | :--- |
| [SETUP_SECRETS.md](SETUP_SECRETS.md) | Variables, despliegue seguro y rotación de credenciales |
| [SECURITY.md](SECURITY.md) | Divulgación responsable, controles y riesgos conocidos |
| [CUMPLIMIENTO_NORMATIVO_CO.md](docs/CUMPLIMIENTO_NORMATIVO_CO.md) | Matriz norma → control (Ley 1581, Decreto 1074, SIC, MSPI, ISO 27001, ISO 25010, NTC 5854) |
| [POLITICA_TRATAMIENTO_DATOS_PERSONALES.md](docs/POLITICA_TRATAMIENTO_DATOS_PERSONALES.md) | Política interna, RAT, ARCO y retención |
| [MODELO_SEGURIDAD.md](docs/MODELO_SEGURIDAD.md) | Modelo MSPI, mapeo ISO 27001 y OWASP ASVS |
| [MATRIZ_RIESGOS.md](docs/MATRIZ_RIESGOS.md) | Riesgos, nivel y tratamiento |
| [PLAN_RESPUESTA_INCIDENTES.md](docs/PLAN_RESPUESTA_INCIDENTES.md) | Detección, contención, notificación y mejora |
| [OPERACION_BACKUP.md](docs/OPERACION_BACKUP.md) | Respaldos cifrados, restauración y verificación |
| [CALIDAD_Y_ACCESIBILIDAD.md](docs/CALIDAD_Y_ACCESIBILIDAD.md) | ISO/IEC 25010, NTC 5854 y plan de pruebas |
| [Política pública](frontend/public/privacidad.html) | Aviso de privacidad para titulares y acudientes |

Prueba de humo de seguridad:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\security_smoke_test.ps1
```

---

## 📝 Changelog Completo

### v7.1.0 — 2026-10-25 _(auditoría de seguridad)_

**Correcciones críticas**

- 🔐 **Sin claves de respaldo:** la API no arranca sin `Jwt:SecretKey` (≥48 caracteres) ni sin
  `Privacy:EvidenceHmacKey` (≥32, dedicada). La evidencia de consentimiento ya no reutiliza la JWT.
- 🧯 **CAPTCHA fail-closed:** sin `Recaptcha:SecretKey` en producción se rechaza el acceso; el
  bypass solo existe en Development con habilitación explícita.
- 👤 **Sesión y política en el servidor:** se rechazan tokens revocados, cuentas inactivas o
  anonimizadas y usuarios sin la versión vigente de la política.
- 🗑️ **Supresión completa:** al eliminar una cuenta se borran evidencia de consentimiento, último
  acceso y contadores de intentos.
- 📦 **Almacenamiento sin fallback silencioso:** un fallo de Supabase ya no guarda el archivo en un
  disco efímero; `Storage:AllowLocalFallback=false` por defecto.
- 🐳 **Docker no-root:** el proceso corre como `lmsapp` y solo escribe en `logs` y `App_Data`.
- 🧪 **Pruebas:** 55 backend + 6 frontend; CI ejecuta pruebas y auditorías.
- 🧹 **Integridad:** índice único `uq_usuarios_lower_username` y validación estricta de grados.
- ♿ **Accesibilidad:** foco al contenido principal, `aria-live` y `prefers-reduced-motion`.
- 🛡️ **CSP del frontend:** política por `<meta>` compatible con GitHub Pages.

### v7.0.0 — 2026-10-19

**Seguridad**

- 🔐 **Eliminación de secretos del repositorio:** contraseña de Supabase, clave JWT y clave de
  reCAPTCHA salen de `appsettings.json`; se cargan por variables de entorno o
  `appsettings.Local.json` (ignorado por Git y Docker). El arranque falla si falta una clave válida.
- 🌐 **CORS corregido:** se elimina `SetIsOriginAllowed(_ => true)`; ahora hay lista blanca explícita
  y no se permiten credenciales.
- 🚫 **Endpoints peligrosos retirados:** `GET /api/auth/diagnostic` y `POST /api/auth/seed-users`
  (permitían enumerar datos y restablecer cuentas sin autenticación).
- 🧑‍🎓 **Registro seguro:** no se pueden crear administradores desde el formulario público; los
  docentes requieren código de invitación y los estudiantes la autorización de su acudiente.
- 🔑 **Google OAuth endurecido:** se elimina el fallback que leía el ID token sin validar la firma;
  se exige audiencia, correo verificado y cuenta preexistente.
- 🧯 **CAPTCHA sin bypass en producción:** el token de prueba solo funciona en Development con
  `Recaptcha:AllowDevBypass=true`; la verificación falla cerrada si no hay clave.
- ⏱️ **Rate limiting:** 30/min en autenticación, 60/h en subidas y 600/min global por IP.
- 🧱 **Cabeceras:** HSTS, CSP restrictiva, `X-Frame-Options: DENY`, COOP/CORP, `no-store` en rutas
  con datos personales y eliminación de `Server`.
- 🔒 **JWT:** expiración configurable (8 h), validación de algoritmo, `iat`/`nbf`/`jti`, sin clave por
  defecto y con revocación por sesión única.
- 🗄️ **Restauración SQL deshabilitada por defecto:** evitar ejecución remota de SQL; se documenta el
  procedimiento con `psql` y un rol de mínimo privilegio.

**Privacidad (Ley 1581 de 2012)**

- 📜 **Consentimiento con evidencia:** versión de política, fecha, IP, agente de usuario y firma
  HMAC-SHA256; autorización del acudiente para menores.
- 🧾 **Derechos del titular:** `GET /api/privacy/my-data` (acceso/portabilidad),
  `PUT /api/privacy/my-data` (rectificación) y `POST /api/privacy/delete-account`
  (supresión/anonimización con conservación de historia académica).
- 🧭 **Política pública:** nueva página `frontend/public/privacidad.html` y endpoint
  `GET /api/privacy/policy`.
- 📊 **Panel de cumplimiento:** `GET /api/admin/compliance` con cobertura de consentimiento y alertas.

**Archivos y autorización**

- 📁 **Adjuntos privados:** se guardan fuera de `wwwroot` (`App_Data/uploads`), con validación de
  firma binaria, SHA-256, nombre saneado y metadatos.
- 🔐 **Descarga autenticada:** `/api/files/{**key}` valida propietario, docente del curso o admin;
  se elimina la exposición pública de `/uploads`.
- 🛡️ **Anti-IDOR:** validación de pertenencia en matrícula, cursos, tareas, entregas y calificaciones;
  límite de nota según puntaje máximo.
- 🎯 **Contenidos filtrados:** los estudiantes solo ven tareas y materiales de su grado o cursos.

**Calidad y operación**

- 🧪 **CI:** compilación de backend/frontend, 61 pruebas unitarias, auditoría de dependencias,
  verificación de secretos, CodeQL y Dependabot.
- 🐳 **Docker endurecido:** usuario sin privilegios, `.dockerignore` y exclusión de secretos del publish.
- 🧹 **Repositorio limpio:** se dejan de versionar 40.000 archivos de `node_modules`.
- ♿ **Accesibilidad:** modal con `role="dialog"`, etiquetas, campos de acudiente y enlace a la política.
- 📚 **Documentación:** matriz normativa, política de tratamiento, modelo de seguridad, riesgos,
  incidentes, respaldos, calidad y accesibilidad.

### v6.2.0 — 2026-10-14

- 🚀 **Arranque asíncrono no bloqueante en Render:** `DbInitializer.SeedAsync()` desacoplado del inicio de Kestrel mediante `app.Lifetime.ApplicationStarted.Register()`. Deploys en Render reducidos de 8+ minutos a ~1 minuto sin bloqueos en health checks.
- ⚡ **Aceleración de Login & Precalentamiento (Warmup):** `LoginComponent` envía un ping invisible a `/api/ping` al abrir la pantalla, despertando a Render en background antes de enviar credenciales.
- 🟢 **Píldora interactiva de conexión en vivo:** Indicador visual dinámico en login (`⏳ Sincronizando con el servidor...` / `⚡ Conexión instantánea activa`) con estados reactivos.
- 🔍 **Índice Funcional en PostgreSQL:** Creación de `uq_usuarios_lower_username` para búsquedas case-insensitive sin Sequential Scan y sin duplicados.
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
