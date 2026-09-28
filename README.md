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

### Administración y Auditoría (`/api/admin` & `/api/systemfiles`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/admin/metrics` | Métricas generales del sistema (usuarios activos, cursos, estado BD) |
| `GET` | `/api/admin/users` | Listado completo de usuarios con filtros por rol y estado |
| `POST` | `/api/admin/users` | Registro administrativo directo de nuevos usuarios |
| `PUT` | `/api/admin/users/{id}/toggle-status` | Suspensión lógica o reactivación de cuentas (Soft Delete) |
| `GET` | `/api/systemfiles/tree` | Árbol seguro de directorios y archivos de auditoría |
| `GET` | `/api/systemfiles/content` | Visualización en tiempo real de logs del sistema (`access.log`, `error.log`, `app.log`) |

---

## 📁 Estructura Completa del Proyecto

A continuación se detalla la arquitectura de directorios del monorepo, separada por capas de backend (.NET 10), frontend (Angular 18), persistencia en base de datos y scripts de automatización:

```
LMS/
│
├── 📂 backend/                                   # Capa de servicios y lógica de negocio
│   ├── 📂 LMS.API/                               # Proyecto ASP.NET Core 10 Web API
│   │   ├── 📂 Controllers/                       # Controladores REST API
│   │   │   ├── AdminController.cs                # Métricas globales, gestión de usuarios (CRUD/soft delete)
│   │   │   ├── AuthController.cs                 # Registro con captcha, login JWT, validación de sesión
│   │   │   ├── ContentsController.cs             # Publicación y descarga de contenidos y recursos
│   │   │   ├── StudentController.cs              # Dashboard alumno, consulta de tareas y entrega con archivos
│   │   │   ├── SystemFilesController.cs          # Auditoría e inspección protegida de logs y configuraciones
│   │   │   └── TeacherController.cs              # Matrícula, gestión de cursos, semáforo y calificaciones
│   │   │
│   │   ├── 📂 Data/                              # Acceso a datos con Entity Framework Core
│   │   │   ├── DbInitializer.cs                  # Seeding automático de roles y grados escolares
│   │   │   └── LMSDbContext.cs                   # Mapeo Fluent API relacional estricto en snake_case
│   │   │
│   │   ├── 📂 DTOs/                              # Contratos de transferencia de datos tipados (Request/Response)
│   │   │   ├── AdminDtos.cs                      # Métricas de administración y auditoría
│   │   │   ├── AuthDtos.cs                       # Login, Registro, Captcha y perfil de usuario
│   │   │   ├── DashboardDtos.cs                  # Respuestas para dashboards de estudiante y profesor
│   │   │   ├── StudentDtos.cs                    # Entregas de tareas y estado del alumno
│   │   │   └── TeacherDtos.cs                    # Listado de alumnos, creación de cursos y matrícula
│   │   │
│   │   ├── 📂 Entities/                          # Modelos de dominio mapeados a tablas MySQL
│   │   │   ├── Alumno.cs                         # Entidad de alumnos vinculada a usuario y grado
│   │   │   ├── ArchivoEntrega.cs                 # Registro de adjuntos subidos en tareas
│   │   │   ├── Curso.cs                          # Materias/cursos creados por docentes
│   │   │   ├── Entrega.cs                        # Tareas enviadas por estudiantes y sus notas
│   │   │   ├── Grado.cs                          # Grados escolares (1° a 5° de primaria)
│   │   │   ├── Inscripcion.cs                    # Relación alumno-curso (matrícula)
│   │   │   ├── Material.cs                       # Recursos y guías compartidas por el docente
│   │   │   ├── Perfil.cs                         # Información biográfica y de perfil
│   │   │   ├── Rol.cs                            # Roles del sistema (ADMINISTRADOR, DOCENTE, ALUMNO)
│   │   │   ├── SemaforoStatus.cs                 # Cálculo de alertas académicas (Verde, Amarillo, Rojo)
│   │   │   ├── Tarea.cs                          # Asignaciones creadas para los cursos
│   │   │   └── Usuario.cs                        # Credenciales, rol, estado activo y datos principales
│   │   │
│   │   ├── 📂 Middleware/                        # Componentes del pipeline HTTP de Kestrel
│   │   │   ├── GlobalExceptionMiddleware.cs      # Manejo estandarizado de excepciones no controladas en JSON
│   │   │   ├── RequestLoggingMiddleware.cs       # FileLogger thread-safe con rotación automática de archivos .log
│   │   │   └── SecurityHeadersMiddleware.cs      # Inyección de cabeceras CSP, HSTS, X-Frame-Options, etc.
│   │   │
│   │   ├── 📂 Migrations/                        # Migraciones de EF Core (Pomelo MySQL)
│   │   │
│   │   ├── 📂 Services/                          # Servicios de lógica de negocio desacoplados
│   │   │   ├── ICaptchaService.cs / Captcha...   # Generación y validación de retos matemáticos antibot
│   │   │   ├── IFileStorageService.cs            # Interfaz de gestión de almacenamiento de archivos
│   │   │   ├── LocalFileStorageService.cs        # Implementación física para guardar adjuntos en disco
│   │   │   ├── ISemaforoService.cs               # Interfaz del algoritmo de semáforo escolar
│   │   │   ├── SemaforoService.cs                # Lógica de cálculo de desempeño por entregas y atrasos
│   │   │   ├── ITokenService.cs                  # Interfaz para generación de JWT
│   │   │   └── TokenService.cs                   # Creación de tokens Bearer con claims de rol y usuario
│   │   │
│   │   ├── 📂 logs/                              # Logs generados en tiempo de ejecución (access, error, app)
│   │   ├── 📂 wwwroot/uploads/                   # Directorio estático para adjuntos y entregas de tareas
│   │   ├── appsettings.json                      # Configuración de cadena de conexión MySQL, JWT y Kestrel
│   │   ├── LMS.API.csproj                        # Definición del proyecto .NET 10 y dependencias NuGet
│   │   └── Program.cs                            # Configuración de DI, CORS, autenticación JWT, Swagger/Scalar y pipeline
│   │
│   ├── migration_unify_perfiles_usuarios.sql     # Script SQL de migración y compatibilidad de perfiles
│   └── test_all_endpoints.ps1                    # Script PowerShell de pruebas automatizadas E2E de la API
│
├── 📂 frontend/                                  # Aplicación cliente moderna en Angular 18 (Standalone Components)
│   ├── 📂 src/
│   │   ├── 📂 app/
│   │   │   ├── 📂 core/                          # Núcleo de la aplicación (singleton, transversal)
│   │   │   │   ├── 📂 guards/                    # Guardianes de enrutamiento
│   │   │   │   │   ├── auth.guard.ts             # Protege rutas que requieren inicio de sesión
│   │   │   │   │   └── role.guard.ts             # Controla accesos según el rol (ADMIN, DOCENTE, ALUMNO)
│   │   │   │   ├── 📂 interceptors/              # Interceptores HTTP de Angular
│   │   │   │   │   └── jwt.interceptor.ts        # Adjunta el Bearer Token automáticamente en cada petición
│   │   │   │   ├── 📂 models/                    # Definiciones TypeScript de entidades y contratos
│   │   │   │   │   ├── content.model.ts          # Modelos de tareas, entregas y materiales
│   │   │   │   │   ├── semaforo.model.ts         # Modelo de estados del semáforo escolar
│   │   │   │   │   └── user.model.ts             # Modelo de usuario, rol y sesión
│   │   │   │   └── 📂 services/                  # Servicios HTTP y lógica de estado del frontend
│   │   │   │       ├── admin.service.ts          # Métricas de administración y gestión de usuarios
│   │   │   │       ├── api.config.ts             # URLs base y configuración del backend
│   │   │   │       ├── auth.service.ts           # Login, registro dinámico, estado de sesión y claims
│   │   │   │       ├── report-export.service.ts  # Exportación de reportes a PDF, Excel y CSV
│   │   │   │       ├── session-timeout.service.ts# Detección de inactividad con temporizador configurable
│   │   │   │       ├── student.service.ts        # Peticiones del dashboard de alumno y subida de tareas
│   │   │   │       ├── system-files.service.ts   # Inspección segura de archivos de log del sistema
│   │   │   │       ├── teacher.service.ts        # Gestión de alumnos, cursos, matrícula y calificaciones
│   │   │   │       └── theme.service.ts          # Gestión reactiva de tema claro y oscuro (Dark Mode)
│   │   │   │
│   │   │   ├── 📂 features/                      # Vistas y flujos funcionales del sistema
│   │   │   │   ├── 📂 admin/                     # Módulo de Administración
│   │   │   │   │   ├── admin-dashboard.component.* # Panel de métricas, altas/bajas de usuarios y auditoría
│   │   │   │   │   └── 📂 system-inspector/      # Visor protegido de logs y estado del sistema
│   │   │   │   ├── 📂 auth/                      # Módulo de Autenticación
│   │   │   │   │   └── 📂 login/                 # Formulario dual de Login y Registro dinámico con Captcha
│   │   │   │   ├── 📂 student/                   # Módulo del Estudiante
│   │   │   │   │   ├── student-dashboard.component.* # Vista de cursos, notas y tareas asignadas
│   │   │   │   │   └── 📂 components/            # Componentes internos del estudiante
│   │   │   │   │       └── 📂 file-drop-zone/    # Zona interactiva Drag & Drop para subir archivos
│   │   │   │   └── 📂 teacher/                   # Módulo del Docente
│   │   │   │       └── teacher-dashboard.component.* # Panel docente (cursos, matrícula, tareas, semáforo)
│   │   │   │
│   │   │   ├── 📂 shared/                        # Componentes y utilidades compartidas
│   │   │   │   └── 📂 components/
│   │   │   │       ├── 📂 data-policy-modal/     # Modal de política de tratamiento de datos y privacidad
│   │   │   │       ├── 📂 session-warning-modal/ # Modal de alerta por expiración de sesión
│   │   │   │       └── 📂 theme-toggle/          # Botón interactivo para alternar modo claro/oscuro
│   │   │   │
│   │   │   ├── app.component.*                   # Componente raíz con contenedor principal y modales globales
│   │   │   ├── app.config.ts                     # Configuración de proveedores (HTTP Client, Router, Interceptors)
│   │   │   └── app.routes.ts                     # Definición de rutas protegidas y redirecciones
│   │   │
│   │   ├── index.html                            # Plantilla HTML base con fuentes tipográficas
│   │   ├── main.ts                               # Punto de entrada de inicialización de Angular
│   │   └── styles.css                            # Sistema de diseño con variables CSS, animaciones y temas
│   │
│   ├── angular.json                              # Configuración de compilación del CLI de Angular
│   ├── package.json                              # Dependencias de npm y scripts de ejecución
│   ├── standalone-preview.html                   # Prototipo HTML visual interactivo ejecutable sin Node.js
│   └── tsconfig.json                             # Configuración del compilador de TypeScript
│
├── 📂 Scripts y Base de Datos (Raíz)
│   ├── bd.txt                                    # Script DDL SQL canónico completo para MySQL (`lms_scikids`)
│   ├── MIGRACION_BD.txt                          # Guía paso a paso para la migración de base de datos
│   ├── 1_INICIAR_BACKEND.bat                     # Script batch para compilar y ejecutar el Web API (.NET 10)
│   ├── 2_INICIAR_FRONTEND.bat                    # Script batch para compilar y servir la app Angular (`ng serve`)
│   ├── ABRIR_MYSQL_WORKBENCH.bat                 # Script de acceso rápido a MySQL Workbench
│   ├── CONSULTAR_TABLAS_MYSQL.bat                # Script rápido para consultar el conteo de tablas desde consola
│   ├── VER_FRONTEND_INMEDIATO.bat                # Abre directamente el prototipo en el navegador web
│   └── README.md                                 # Documentación técnica integral del proyecto
```

### 🧩 Desglose por Capas y Responsabilidades

| Capa / Módulo | Ubicación Principal | Responsabilidad |
| :--- | :--- | :--- |
| **Controladores REST** | `backend/LMS.API/Controllers/` | Exponen los endpoints HTTP clasificados por dominio (`/api/auth`, `/api/teacher`, `/api/student`, `/api/admin`, `/api/contents`, `/api/systemfiles`). |
| **Acceso a Datos (ORM)** | `backend/LMS.API/Data/` | `LMSDbContext` gestiona el modelo relacional en MySQL y `DbInitializer` ejecuta el seeding de roles y grados al arrancar. |
| **Lógica de Negocio** | `backend/LMS.API/Services/` | Algoritmo del semáforo escolar, generación y validación de tokens JWT, almacenamiento de archivos físicos y captcha. |
| **Pipeline & Seguridad** | `backend/LMS.API/Middleware/` | Inyección de cabeceras de seguridad (`SecurityHeaders`), auditoría a archivos rotativos (`RequestLogging` + `FileLogger`) y manejo global de errores (`GlobalException`). |
| **Núcleo Frontend** | `frontend/src/app/core/` | Guardianes de navegación por rol (`RoleGuard`), interceptor JWT automático, modelos de datos TypeScript y servicios HTTP centralizados. |
| **Módulos de Rol** | `frontend/src/app/features/` | Vistas específicas para cada tipo de actor del sistema (Administrador, Docente, Estudiante y Autenticación). |
| **Componentes Compartidos** | `frontend/src/app/shared/` | Componentes reutilizables entre vistas: cambio de tema claro/oscuro, alerta interactiva de timeout de sesión y políticas de privacidad. |
| **Persistencia Relacional** | `bd.txt` | Esquema estricto de 11 tablas en MySQL 8.0 con codificación `utf8mb4`, claves foráneas e integridad referencial. |

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
