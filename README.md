# LMS Web - Plataforma Educativa Primaria (MySQL `lms_scikids`) 🎒✨

Sistema de Gestión de Aprendizaje (LMS) diseñado con arquitectura limpia en **.NET 10 Web API**, frontend moderno en **Angular 18** y persistencia relacional estricta en **MySQL** basada en el esquema definido en `bd.txt`.

---

## 🏗️ Stack Tecnológico

| Capa | Tecnología | Versión |
| :--- | :--- | :---: |
| **Backend** | ASP.NET Core Web API | .NET 10 |
| **ORM** | Entity Framework Core + Pomelo MySQL | 9.0.0 |
| **Autenticación** | JWT Bearer | 9.0.0 |
| **Documentación API** | Scalar (reemplaza Swagger UI) | 2.6.0 |
| **Hashing** | BCrypt.Net-Next | 4.0.3 |
| **Frontend** | Angular CLI | 18.x |
| **Base de datos** | MySQL Server | 8.0 |

---

## ✨ Funcionalidades Principales

1. **Migración Estricta a MySQL (`bd.txt`):**
   - Base de datos: **`lms_scikids`** con codificación `utf8mb4`.
   - 11 tablas mapeadas en Entity Framework Core con nombres snake_case, llaves foráneas en cascada/restricción e índices únicos:
     - `roles`, `grados`, `usuarios`, `perfiles`, `alumnos`, `cursos`, `inscripciones`, `tareas`, `entregas`, `archivos_entrega`, `materiales`.

2. **Seeding Automático de Catálogos:**
   - Al iniciar el backend, se crean automáticamente los `roles` (ADMINISTRADOR, DOCENTE, ALUMNO) y los `grados` (1° a 5°) si no existen.
   - No hay usuarios dummy predeterminados; se crean dinámicamente desde el registro.

3. **Registro Público Dinámico desde el Login:**
   - Endpoint: `POST /api/auth/register`.
   - Interfaz Angular con pestaña **"Crear Cuenta"** — permite elegir perfil (Estudiante 🎒, Docente 👩‍🏫, Administrador 👑) y grado escolar.

4. **CRUD Docente — Gestión de Alumnos y Cursos:**
   - Pestaña **"Gestión de Alumnos y Matrícula"** en el `TeacherDashboardComponent`.
   - Alta de alumnos, listado con materias inscritas, creación de cursos y matrícula con 1 clic.

5. **Semáforo Escolar:**
   - Calcula el estado de asistencia/entregas por alumno: 🟢 Verde, 🟡 Amarillo, 🔴 Rojo.

---

## 🖥️ Requisitos del Entorno

| Programa | Estado | Descripción |
| :--- | :---: | :--- |
| **.NET 10 SDK** | ✅ Instalado | v10.0.400 (`C:\Program Files\dotnet\`) |
| **MySQL Server 8.0** | ✅ Instalado | Servicio Windows: `MySQL80` (Puerto 3306) |
| **MySQL Workbench 8.0** | ✅ Instalado | Interfaz gráfica para gestionar `lms_scikids` |
| **Node.js v24 & npm 11** | ✅ Instalado | Node v24.20.0 — `node_modules` ya instalados |

---

## 🚀 Guía de Ejecución Rápida

### PASO 1 — Verificar contraseña de MySQL

Abre `backend/LMS.API/appsettings.json` y confirma la cadena de conexión:

```json
"DefaultConnection": "Server=localhost;Port=3306;Database=lms_scikids;Uid=root;Pwd=root;CharSet=utf8mb4;"
```

> Si tu contraseña de MySQL es distinta a `root`, cámbiala en `Pwd=...`.

---

### PASO 2 — Crear la base de datos `lms_scikids`

Tienes dos opciones equivalentes:

#### Opción A — MySQL Workbench (recomendada):
1. Abre **MySQL Workbench** y conecta con tu usuario `root`.
2. Abre una nueva pestaña de Query (`Ctrl + T`).
3. Copia y pega **todo el contenido** del archivo `bd.txt`.
4. Ejecuta con **`Ctrl + Shift + Enter`** (ejecuta todo el script).

#### Opción B — Dejar que EF Core lo cree automáticamente:
El backend llama a `EnsureCreatedAsync()` al arrancar y crea la BD y las tablas si no existen. Solo inicia el backend directamente en el PASO 3.

---

### PASO 3 — Iniciar el Backend (.NET 10)

Haz doble clic en:
```
1_INICIAR_BACKEND.bat
```

Disponible en:
- **API REST:** `http://localhost:5000`
- **Scalar API Docs:** `http://localhost:5000/scalar/v1`

> Scalar es la interfaz interactiva moderna que reemplaza a Swagger UI. Permite probar todos los endpoints con autenticación JWT.

---

### PASO 4 — Iniciar el Frontend (Angular 18)

#### Opción Angular CLI (recomendada):
Haz doble clic en:
```
2_INICIAR_FRONTEND.bat
```
Se servirá en: `http://localhost:4200`

> Los `node_modules` ya están instalados. El arranque es inmediato.

#### Opción Inmediata (sin servidor de desarrollo):
Haz doble clic en:
```
VER_FRONTEND_INMEDIATO.bat
```
Abre el archivo `standalone-preview.html` directamente en el navegador.

---

## 🧪 Endpoints Principales de la API

> Explora y prueba todos los endpoints de forma interactiva en: **`http://localhost:5000/scalar/v1`**

### Autenticación y Registro (`/api/auth`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `POST` | `/api/auth/register` | Crea un nuevo usuario (Rol, Nombre, Contraseña, Grado) |
| `POST` | `/api/auth/login` | Autentica y emite JWT Bearer token |
| `GET` | `/api/auth/me` | Retorna los datos del usuario en sesión |

### Gestión Docente (`/api/teacher`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/teacher/students` | Lista alumnos con sus materias inscritas |
| `POST` | `/api/teacher/students` | Crea un nuevo alumno |
| `GET` | `/api/teacher/courses` | Lista materias del docente con conteo de inscritos |
| `POST` | `/api/teacher/courses` | Crea una nueva materia/curso |
| `POST` | `/api/teacher/courses/{courseId}/enroll` | Matricula un alumno en un curso |
| `GET` | `/api/teacher/students-status` | Semáforo escolar (Verde / Amarillo / Rojo) |
| `GET` | `/api/teacher/metrics` | Métricas cuantitativas de asistencia |
| `POST` | `/api/teacher/contents` | Publica tareas entregables o recursos educativos |

### Estudiantes (`/api/student`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/student/dashboard` | Materias y tareas asignadas al alumno |
| `POST` | `/api/student/upload-assignment` | Entrega de tareas mediante subida de archivos |

---

## 📁 Estructura del Proyecto

```
LMS/
├── backend/
│   └── LMS.API/
│       ├── Controllers/        # AuthController, TeacherController, StudentController, ContentsController
│       ├── Data/               # LMSDbContext, DbInitializer
│       ├── DTOs/               # Objetos de transferencia de datos
│       ├── Entities/           # 12 entidades mapeadas a MySQL
│       ├── Middleware/         # SecurityHeaders, RequestLogging, GlobalException
│       ├── Migrations/         # Migración InitialMySqlSchema
│       ├── Services/           # TokenService, SemaforoService, FileStorageService
│       ├── wwwroot/uploads/    # Archivos de entregas de tareas
│       ├── logs/               # Archivos .log generados automáticamente
│       ├── appsettings.json    # Conexión MySQL + JWT config
│       └── Program.cs          # Bootstrap de la aplicación + pipeline middleware
├── frontend/
│   └── src/app/
│       ├── core/               # Guards, Interceptors, Services, Models
│       └── features/           # Auth, Teacher, Student, Admin dashboards
├── bd.txt                      # Script SQL completo del esquema MySQL
├── 1_INICIAR_BACKEND.bat       # Lanzador del backend
├── 2_INICIAR_FRONTEND.bat      # Lanzador del frontend Angular
└── VER_FRONTEND_INMEDIATO.bat  # Abre el HTML standalone sin Node.js
```

---

## ⚙️ Configuración JWT

El token Bearer se configura en `appsettings.json`:

```json
"Jwt": {
  "SecretKey": "LmsPrimarySchoolSuperSecretKey2026!@#$%^&*()_+",
  "Issuer": "LMS.API",
  "Audience": "LMS.Client"
}
```

El frontend adjunta automáticamente el token en cada petición HTTP mediante el `JwtInterceptor` de Angular.

---

## 🔒 Middlewares de Seguridad y Logging

El pipeline HTTP incluye 3 middlewares personalizados registrados en `Program.cs`, ubicados en `Middleware/`:

### `SecurityHeadersMiddleware`

Inyecta cabeceras de seguridad HTTP en todas las respuestas:

| Cabecera | Valor |
| :--- | :--- |
| `X-Frame-Options` | `SAMEORIGIN` — previene clickjacking |
| `X-Content-Type-Options` | `nosniff` — previene MIME-sniffing |
| `X-XSS-Protection` | `1; mode=block` — protección XSS en navegadores antiguos |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Content-Security-Policy` | Política restrictiva por defecto |
| `Permissions-Policy` | Bloquea cámara, micrófono, geolocalización, pagos |

### `RequestLoggingMiddleware`

Registra cada petición HTTP en archivos `.log` con rotación automática:

| Archivo | Contenido |
| :--- | :--- |
| `logs/access.log` | Todas las peticiones: IP, método, ruta, status, tiempo, User-Agent |
| `logs/error.log` | Solo respuestas 4xx/5xx y excepciones con stack trace |
| `logs/app.log` | Eventos de la aplicación (inicio del servidor, logs manuales) |

**Características del sistema de logs (`FileLogger`):**
- **6 niveles de severidad:** `DEBUG`, `INFO`, `WARNING`, `ERROR`, `FATAL`
- **Rotación automática** por tamaño (5 MB por archivo, máximo 10 archivos rotados)
- **Thread-safe** mediante `SemaphoreSlim` (seguro para concurrencia)
- **Registro de excepciones** con stack trace completo
- Los archivos se generan automáticamente en `bin/Debug/net10.0/logs/`

**Uso directo desde cualquier servicio o controlador:**

```csharp
await FileLogger.InfoAsync("Usuario autenticado", new { userId = 42 });
await FileLogger.ErrorAsync("Consulta SQL falló", new { query = sql });
await FileLogger.ExceptionAsync(ex, "Error en proceso de matrícula");
```

### `GlobalExceptionMiddleware`

Captura excepciones no controladas y devuelve respuestas JSON estandarizadas:

```json
{
  "status": 500,
  "title": "Error interno del servidor",
  "message": "Ocurrió un error inesperado. Intente de nuevo más tarde.",
  "timestamp": "2026-09-11T12:00:00Z",
  "path": "POST /api/auth/login",
  "traceId": "0HN7..."
}
```

> En modo `Development`, la respuesta incluye el mensaje real de la excepción y el stack trace en el campo `detail`.

---

## 📝 Changelog

### v1.1.0 — 2026-09-11

- ✅ Añadido `Middleware/SecurityHeadersMiddleware.cs` — inyecta cabeceras de seguridad HTTP (`X-Frame-Options`, `X-Content-Type-Options`, `CSP`, `Permissions-Policy`).
- ✅ Añadido `Middleware/RequestLoggingMiddleware.cs` + `FileLogger` — sistema de logging a archivos `.log` con rotación automática, registro de accesos HTTP y errores.
- ✅ Añadido `Middleware/GlobalExceptionMiddleware.cs` — captura excepciones no controladas y devuelve respuestas JSON estandarizadas con soporte de stack trace en desarrollo.
- ✅ Integrados los 3 middlewares en `Program.cs` con orden correcto de ejecución.
- 🗑️ Eliminados `backend/.htaccess` y `backend/helpers/logger.php` (incompatibles con el stack .NET/Kestrel).
