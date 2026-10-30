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
| **Base de datos** | PostgreSQL (Supabase Cloud / Pooler IPv4) | 17.6 |
| **Internacionalización** | i18n Multilingüe en tiempo real | ES, EN, ZH, JA, PT |
| **Hosting Frontend** | GitHub Pages (SPA + PWA) | — |
| **Hosting Backend** | Render (Docker / Web Service) | — |
| **Exportación** | jsPDF + jspdf-autotable (PDF) · Excel `.xls` y CSV con `Blob` UTF-8 | — |
| **Almacenamiento de archivos** | Local privado (`App_Data/uploads`); Supabase Storage pendiente de configuración en Render | — |
| **Cumplimiento** | Ley 1581 de 2012, Decreto 1074 de 2015, MSPI (MinTIC), NTC 5854, ISO/IEC 25010 | — |

---

## 🛡️ Arquitectura de Seguridad y Cumplimiento Normativo Colombiano

La arquitectura de **LMS Primaria** implementa controles técnicos de privacidad desde el diseño (*Privacy by Design*) y seguridad por defecto (*Security by Default*) en cumplimiento estricto de la legislación colombiana: **Ley Estatutaria 1581 de 2012** (Régimen General de Protección de Datos Personales), **Decreto Reglamentario 1377 de 2013** (incorporado en el Decreto Único 1074 de 2015), **Ley 1098 de 2006** (Código de la Infancia y la Adolescencia — Art. 7 y 33) y la **Circular Externa 002 de 2015 de la Superintendencia de Industria y Comercio (SIC)**.

### 1. Cumplimiento de Habeas Data y Protección de Menores (Ley 1581 de 2012, Decreto 1377 de 2013 y Ley 1098 de 2006)

#### Modelo de Evidencia Probatoria (`ConsentimientoHabeasData`)
Conforme al principio de **Responsabilidad Demostrada (*Accountability*)** exigido por los artículos 9 y 12 de la Ley 1581 de 2012 y el artículo 12 del Decreto 1377 de 2013, el tratamiento de datos personales de niños, niñas y adolescentes (NNA) se respalda en la entidad relacional `ConsentimientoHabeasData` (`consentimientos_habeas_data`), vinculada a `Alumno` y `Usuario`:

| Campo | Propósito Legal y Técnico |
| :--- | :--- |
| `Id` / `AlumnoId` / `UsuarioId` | Identificador único del consentimiento y trazabilidad con el estudiante menor de edad y la cuenta asociada. |
| `AcudienteNombre` / `TipoDocumento` / `AcudienteDocumento` | Identificación del padre, madre o representante legal que otorga la autorización. El documento se almacena enmascarado (`ConsentService.MaskDocument`) para minimizar exposición de PII. |
| `VersionPolitica` / `HashTextoPolitica` | Versión normativa aceptada y huella criptográfica **SHA-256** del texto íntegro de la política vigente, garantizando no repudio e integridad probatoria ante auditorías de la SIC. |
| `AutorizaServicioEducativo` (`bool`, obligatorio) | Finalidad **esencial**: gestión académica, matrícula en cursos, entrega/calificación de tareas y seguimiento pedagógico (Semáforo Escolar). |
| `AutorizaDatosOpcionales` (`bool`, opcional) | Finalidades **no esenciales** separadas explícitamente: actividades extracurriculares, reconocimientos y comunicaciones no académicas (prohibición de consentimientos en bloque). |
| `AutorizaTransmisionInternacional` (`bool`) | Autorización expresa para la transmisión internacional de datos (Art. 26 Ley 1581 de 2012 y Art. 24-25 Decreto 1377 de 2013) hacia encargados tecnológicos cloud (**Render**, **Supabase Cloud** y **GitHub Pages**). |
| `IpOrigen` / `UserAgent` | Metadatos forenses del dispositivo desde el cual el representante legal suscribió la autorización. |
| `FechaAutorizacionUtc` / `FechaRevocatoriaUtc` | Marca de tiempo UTC de otorgamiento y de revocatoria (cuando aplica). La propiedad calculada `EstaVigente` verifica que `AutorizaServicioEducativo == true` y `FechaRevocatoriaUtc == null`. |

#### Bloqueo Preventivo de Perfiles de Menores sin Consentimiento Vigente
Ningún perfil con rol `Student` (`ALUMNO`) puede operar en la plataforma sin contar con un registro activo y vigente en `ConsentimientoHabeasData` firmado por su representante legal:

- **Backend (.NET 8):**
  - **`MinorConsentActionFilter`** (registrado sobre `StudentController`) y **`SingleSessionMiddleware`** verifican en cada petición autenticada que el alumno posea un registro en `ConsentimientosHabeasData` con `FechaRevocatoriaUtc == null`, `AutorizaServicioEducativo == true` y `VersionPolitica` igual a la versión institucional vigente.
  - En caso de ausencia o revocatoria del consentimiento, el backend interrumpe la ejecución y responde con estado HTTP **`403 Forbidden`** y código de error estructurado **`MINOR_CONSENT_REQUIRED`**.
- **Frontend (Angular 18):**
  - El guard funcional **`minorConsentGuard`** (`frontend/src/app/core/guards/minor-consent.guard.ts`) protege todas las rutas de estudiante (`/student/**`), redirigiendo automáticamente a `/login?reason=minor_consent_required` y abriendo el componente standalone **`GuardianConsentNoticeComponent`** (Aviso de Privacidad en lenguaje claro conforme a la guía de la SIC) si `hasActiveGuardianConsent` o `dataPolicyAccepted` son `false`.

---

### 2. Gestión de Derechos ARCO y SLA Legal de la SIC (Art. 14 y 15 Ley 1581 de 2012)

El controlador `PrivacyController` (`backend/LMS.API/Controllers/PrivacyController.cs`) y la entidad `SolicitudArco` (`solicitudes_arco`) gestionan formalmente el ciclo de vida de los derechos de **Conocer, Actualizar, Rectificar y Suprimir (ARCO)** y la revocatoria de autorización:

| Método | Endpoint | Descripción Legal y Funcional |
| :--- | :--- | :--- |
| `POST` | `/api/privacy/arco` | Radica una solicitud formal ARCO (`CONOCER`, `ACTUALIZAR`, `RECTIFICAR`, `SUPRIMIR` o `REVOCAR`), enmascara el documento del solicitante y calcula automáticamente la fecha límite legal (`FechaLimiteLegalUtc`). |
| `GET` | `/api/privacy/arco` | Consulta el historial de solicitudes ARCO radicadas por el titular o representante legal autenticado, incluyendo su estado (`RADICADA`, `EN_TRAMITE`, `RESUELTA`) y vencimiento legal. |
| `POST` | `/api/privacy/consent/revoke` | Revoca inmediatamente el consentimiento vigente en `ConsentimientoHabeasData` (`FechaRevocatoriaUtc = DateTime.UtcNow`) y desmarca `DataPolicyAccepted`. |

#### Motor de Cálculo de Días Hábiles Colombianos (`SolicitudArco.CalcularFechaLimiteLegalColombia`)
Para garantizar el cumplimiento de los términos legales perentorios de la SIC sin depender de listas estáticas anuales, `SolicitudArco` implementa un motor de calendario laboral colombiano para cualquier año:
- **Consultas (`CONOCER` / `ACCESO` — Art. 14 Ley 1581 de 2012):** **10 días hábiles** contados a partir del día hábil siguiente a la fecha de radicación.
- **Reclamos (`ACTUALIZAR`, `RECTIFICAR`, `SUPRIMIR`, `REVOCAR` — Art. 15 Ley 1581 de 2012):** **15 días hábiles** contados a partir del día hábil siguiente a la fecha de radicación.
- **Exclusión automática de fines de semana y los 18 festivos nacionales de Colombia (`ObtenerFestivosColombia(int year)`):**
  1. **Festivos de fecha fija:** Año Nuevo (1 ene), Día del Trabajo (1 may), Día de la Independencia (20 jul), Batalla de Boyacá (7 ago), Inmaculada Concepción (8 dic) y Navidad (25 dic).
  2. **Festivos trasladables al lunes siguiente (Ley 51 de 1983 — *Ley Emiliani*):** Reyes Magos (6 ene), San José (19 mar), San Pedro y San Pablo (29 jun), Asunción de la Virgen (15 ago), Día de la Raza (12 oct), Todos los Santos (1 nov) e Independencia de Cartagena (11 nov), calculados mediante `TrasladarALunesEmiliani(DateTime fecha)`.
  3. **Festivos litúrgicos móviles basados en Pascua dinámica:** cálculo exacto del Domingo de Pascua para cualquier año mediante el **algoritmo astronómico de Meeus/Jones/Butcher (`CalcularDomingoPascua`)**, derivando Jueves Santo ($-3$ días), Viernes Santo ($-2$ días) y trasladando por Ley Emiliani la Ascensión del Señor ($+39$ días), Corpus Christi ($+60$ días) y Sagrado Corazón de Jesús ($+68$ días).

---

### 3. Seguridad Aplicativa (OWASP / Circular Externa 002 de 2015 SIC)

#### Controles Anti-IDOR / BOLA (OWASP API1:2023)
- **`TeacherController`:** Validación estricta del `UserId` extraído de los claims firmados del JWT frente a la propiedad del recurso antes de permitir cualquier operación de lectura o escritura:
  - `CreateContent`, `EnrollStudent`, `GetGradebook`: verifican que el `CursoId` pertenezca a `DocenteId == teacher.Id`.
  - `GetAssignmentSubmissions`, `UpdateActivity`, `GradeSubmission`: validan que la `Tarea` o `Entrega` pertenezca a un curso asignado al docente autenticado.
  - `GetStudentProgressHistory`, `GetStudentSemaforoStatusById`: comprueban que el `AlumnoId` esté inscrito en un curso o grado asignado al docente antes de exponer calificaciones o el estado del semáforo escolar (`SemaforoStatus`).
- **`StudentController`:**
  - `GetStudentDashboard`, `EnrollInCourse`, `WithdrawFromCourse`, `UploadAssignment`: resuelven la identidad del estudiante exclusivamente desde el token JWT (`ClaimTypes.NameIdentifier`), bloquean matrículas o entregas cruzadas entre grados distintos (`403 Forbidden`) y verifican el consentimiento vigente del representante legal.

#### Sanitización y Enmascaramiento de Logs de Auditoría
- **`RequestLoggingMiddleware.SanitizeSensitiveText` y `AuditLogDbBackgroundService`:** Antes de escribir en disco o persistir en la tabla `audit_logs` de PostgreSQL, un sanitizador basado en expresiones regulares redacta y reemplaza por `***REDACTED***` cualquier campo sensible en JSON, querystrings o texto libre: `password`, `currentPassword`, `newPassword`, `token`, `accessToken`, `sessionToken`, `captchaToken`, `secret`, `documento`, `guardianDocument`, `acudienteDocumento`, `solicitanteDocumento`, `observaciones`, `feedback` y `retroalimentacion`.

#### Rate Limiting, CORS Estricto y Security Headers
- **`RateLimiter` nativo de .NET 8 (`Program.cs`):**
  - Política `"auth"` aplicada mediante `[EnableRateLimiting("auth")]` a los endpoints críticos de `AuthController` (`login`, `google-login`, `register`, `change-password`), limitando ráfagas de fuerza bruta y credential stuffing por dirección IP con respuesta `429 Too Many Requests`.
- **Normalización estricta de orígenes CORS (`CORS_ALLOWED_ORIGINS`):**
  - `Program.cs` parsea la variable de entorno `CORS_ALLOWED_ORIGINS` separando por `,` o `;` y aplicando `.Trim().TrimEnd('/')` a cada URL configurada, evitando rechazos de preflight (`OPTIONS`) cuando el navegador envía el encabezado `Origin` sin barra diagonal final desde **GitHub Pages** (`https://sneiderm2.github.io`).
- **`SecurityHeadersMiddleware`:**
  - Inyecta en todas las respuestas `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Strict-Transport-Security: max-age=31536000; includeSubDomains` y una política **`Content-Security-Policy` (CSP)** endurecida que autoriza únicamente los dominios requeridos por **Google reCAPTCHA** (`https://www.google.com`, `https://www.gstatic.com`) y **Supabase** (`https://*.supabase.co`) en `connect-src`, `script-src` y `frame-src`.

#### Privacidad en Equipos Compartidos (Angular 18 PWA)
Pensado para salas de cómputo escolares y dispositivos compartidos en básica primaria:
- **Cero PII en `localStorage`:** Los datos personales del estudiante (`currentUser`) residen en un `signal<User | null>` en memoria RAM dentro de `AuthService`. Las preferencias de notificaciones se aíslan bajo una clave anónima con hash (`lms_ReadNotifs_h_<hash>`).
- **Rehidratación segura al recargar (`F5`):** Al recargar el navegador, `AuthService.ensureUserHydrated$()` restaura el estado de sesión desde `sessionStorage` (`lms_session_user`) o consulta `/api/auth/me` con el JWT activo antes de que `minorConsentGuard` evalúe la ruta, evitando cierres de sesión accidentales sin exponer PII en almacenamiento persistente.
- **Purga automática tras 15 minutos de inactividad (`SessionTimeoutService`):** Monitorea eventos de interacción del usuario y, al cumplirse **15 minutos de inactividad** (o al cerrar sesión), ejecuta `AuthService.clearAllSensitiveStorage()`, limpiando `localStorage`, `sessionStorage` y todas las cachés del Service Worker (`caches.keys()`).

---

### 4. Guía de Variables de Entorno y Despliegue Seguro (CI/CD)

El repositorio no almacena credenciales ni claves reales. La plantilla [`.env.example`](.env.example) documenta todas las variables requeridas utilizando exclusivamente el marcador `<REEMPLAZAR_EN_RENDER>`, y [`.gitguardian.yaml`](.gitguardian.yaml) protege el pipeline CI/CD frente a falsos positivos en pruebas unitarias.

#### Variables de Entorno Principales (`.env.example`)

| Variable de Entorno | Propósito y Requisitos de Seguridad |
| :--- | :--- |
| `DATABASE_URL` / `ConnectionStrings__DefaultConnection` | Cadena de conexión PostgreSQL hacia **Supabase Session Pooler** (puerto `5432`, IPv4 compatible con Render) con `SSL Mode=Require`. |
| `SUPABASE_URL` | URL base del proyecto en Supabase (`https://<PROJECT_REF>.supabase.co`). |
| `SUPABASE_SERVICE_ROLE_KEY` | Clave privada `service_role` de Supabase para operaciones autenticadas del backend sobre el bucket privado `lms-uploads`. |
| `Jwt__SecretKey` | Clave simétrica de alta entropía (**mínimo 48 caracteres**, recomendado 64 bytes en Base64) para firma de tokens JWT (`HS256`). |
| `EVIDENCE_HMAC_KEY` / `Privacy__EvidenceHmacKey` | Clave criptográfica dedicada (**mínimo 32 caracteres**, distinta de `Jwt__SecretKey`) para firmar con `HMAC-SHA256` las evidencias de consentimiento de Habeas Data. |
| `RECAPTCHA_SECRET_KEY` / `Recaptcha__SecretKey` | Clave secreta del servidor para validación *fail-closed* de tokens Google reCAPTCHA en login y registro público. |
| `CORS_ALLOWED_ORIGINS` | Lista de orígenes autorizados separados por coma (ej. `https://sneiderm2.github.io`); normalizados automáticamente con `.TrimEnd('/')`. |
| `SEED_DEFAULT_PASSWORD` | Credencial inicial leída por `DbInitializer` cuando se habilita el sembrado controlado (`Seed:Enabled=true`). En desarrollo, si no se define, se genera dinámicamente en tiempo de ejecución. |

#### Comandos de Verificación (Suite de 125 Pruebas de Seguridad y Build de Producción)

```powershell
# 1. Ejecutar las 125 pruebas unitarias y de seguridad del backend (.NET 8 + xUnit)
dotnet test backend/LMS.API.Tests/LMS.API.Tests.csproj --configuration Release

# 2. Compilar el bundle de producción del frontend (Angular 18 PWA)
cd frontend
npx ng build --configuration production
```

---

## 🛡️ Resumen de Controles y Documentación Normativa

La plataforma incorpora controles alineados con la **Ley 1581 de 2012**, el **Decreto 1074 de 2015**,
los lineamientos de la **SIC**, el **MSPI** de MinTIC, **ISO/IEC 27001**, **ISO/IEC 25010** y **NTC 5854**.

| Tema | Implementación | Documento |
| :--- | :--- | :--- |
| Secretos | Variables de entorno + `appsettings.Local.json` ignorado por Git y Docker | [SETUP_SECRETS.md](SETUP_SECRETS.md) |
| Consentimiento | Evidencia firmada (HMAC), versión de política y autorización de acudiente | [Política](frontend/public/privacidad.html) |
| Derechos del titular | Descarga de datos, rectificación y supresión/anonimización | `/api/privacy/*` |
| Archivos | Almacenamiento privado, validación de firma binaria y descarga autorizada | `FilesController` |
| Autorización | Roles canónicos y validación de pertenencia de cursos (anti-IDOR) | `SECURITY.md` |
| Auditoría | Logs con redacción de datos sensibles y retención configurable; tabla `audit_logs` con RLS | `/api/admin/audit-logs` |
| Incidentes | Plan de respuesta y notificación | [Plan](docs/PLAN_RESPUESTA_INCIDENTES.md) |
| Riesgos | Matriz y plan de tratamiento | [Matriz](docs/MATRIZ_RIESGOS.md) |
| Calidad y accesibilidad | ISO/IEC 25010 y NTC 5854 | [Calidad](docs/CALIDAD_Y_ACCESIBILIDAD.md) |
| Cumplimiento | Trazabilidad norma → control | [Matriz normativa](docs/CUMPLIMIENTO_NORMATIVO_CO.md) |

> ⚠️ **Antes de desplegar:** rote la contraseña de la base de datos, la clave JWT y la clave de
> reCAPTCHA (estuvieron versionadas), cambie las contraseñas de las cuentas demo y complete los datos
> del responsable del tratamiento. Ver [SETUP_SECRETS.md](SETUP_SECRETS.md).

### Estado de seguridad (auditoría 2026-10, verificado el 28/10/2026)

> **Última verificación:** 2026-10-28, 04:30 UTC. Backend Release compilado con **0 errores y 0 advertencias** (**119/119 pruebas xUnit** de seguridad y penetración superadas); frontend con build de producción correcto (**23/23 pruebas Karma/Jasmine** aprobadas en ChromeHeadless). Se incorporó auditoría DAST automatizada mediante [OWASP ZAP Baseline Scan](.github/workflows/dast-scan.yml).

- **Hardening BOLA/IDOR:** Validación estricta de pertenencia y titularidad docente en `TeacherController.CreateContent` y restricción de grado institucional en `StudentController.EnrollInCourse`.
- **Rotación de Sesión Inmediata:** Al cambiar la contraseña en `AuthController.ChangePassword`, se genera un nuevo `SessionToken`, invalidando cualquier sesión abierta en otros dispositivos y emitiendo un JWT renovado.
- **Blindaje Path Traversal:** `FilesController` y `SystemFilesController` validan y rechazan secuencias de escape de directorio (`..`, separadores de ruta normalizados y nombres nulos).
- **Prevención de Fuga de Token JWT (CWE-200 / CWE-522):** En `jwt.interceptor.ts`, la función `isTrustedApiUrl` restringe estrictamente el envío del encabezado `Authorization: Bearer` al origen canónico del backend escolar, previniendo fugas hacia URLs externas.
- **Accesibilidad WCAG 2.1 AA & Modo Oscuro:** Sincronización de `<meta name="color-scheme" content="light dark">`, eliminación de pseudo-protocolos `javascript:void(0)` en favor de botones nativos accesibles, soporte de foco visible (`:focus-visible`) y armonización de variables semánticas en dashboards y gráficos SVG.

- La API **no arranca** sin `Jwt:SecretKey` (≥48 caracteres) ni sin `Privacy:EvidenceHmacKey`
  (≥32 caracteres, distinta de la JWT). Ya no existen claves de respaldo en el código.
- El CAPTCHA es **fail-closed**: si `Recaptcha__SecretKey` no está configurada en producción, el
  login y el registro se rechazan. El bypass solo existe en Development y debe habilitarse
  explícitamente con `Recaptcha__AllowDevBypass=true`.
- La sesión única y la aceptación vigente de la política se validan en el servidor para cada
  petición autenticada, no solo en el frontend.
- **Auditoría persistente:** `audit_logs` está activa en Supabase (PostgreSQL **17.6**) con 125
  registros; el último es del 25/10/2026 22:31 UTC y 45 se generaron desde las 21:50 UTC durante la
  verificación. Tiene RLS habilitado, 3 índices y 0 políticas (solo el propietario de la tabla
  escribe; los roles anónimos quedan bloqueados).
- **Producción:** `/api/ping`, `/api/health` y `/api/health/live` responden `200`; `/api/health`
  informa `database: "Healthy"`. Swagger responde `404`. CORS permite
  `https://sneiderm2.github.io` (preflight `204`) y no devuelve `Access-Control-Allow-Origin` a
  orígenes ajenos. HSTS, CSP, `X-Frame-Options`, `nosniff` y `Referrer-Policy` están presentes.
- `Storage:Provider=Local` sigue disponible para desarrollo, pero en producción el disco de Render
  es efímero. El servicio actual usa el plan Free, que **no admite discos persistentes**. El bucket
  privado `lms-uploads` ya existe en Supabase (25 MB), pero la API todavía reporta `storage: "Local"`
  hasta configurar `Storage__Provider=Supabase`, la URL y la Service Role Key en Render, y desplegar.
  Si se migra a un plan con disco, use `Storage__RootPath` apuntando al disco montado. El fallback
  silencioso de Supabase a local está deshabilitado (`Storage:AllowLocalFallback=false`).
- **Dependencias:** `npm audit --omit=dev --audit-level=critical` termina con código 0; hay 9
  vulnerabilidades (4 moderadas, 5 altas) de Angular 18, sin críticas. NuGet no reporta paquetes
  vulnerables. Las actualizaciones disponibles de .NET/NuGet y Angular son mayores y no se
  ejecutaron por falta de autorización.
- **Deuda de plataforma:** .NET 8 alcanza su fin de soporte el **10/11/2026** y Angular 18 ya está
  fuera de soporte. La migración mayor no se ejecutó en esta auditoría; debe planificarse antes de
  la fecha de EOL de .NET.
- **Privacidad:** hay 18 usuarios y 10 con consentimiento vigente (55,6 %). La página pública
  `frontend/public/privacidad.html` todavía dice «12 caracteres» y conserva «Por definir» en los
  datos del responsable; la implementación real usa **8–64 caracteres**. Corregir la página y
  completar los datos institucionales sigue pendiente.
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
- Exportación de reportes en **PDF**, **Excel (.xls)** y **CSV**

### 🛡️ Panel de Administrador
- Métricas institucionales en tiempo real (usuarios, docentes, alumnos, cursos)
- **Directorio completo de usuarios** con filtros por rol, estado y búsqueda
- **Registro administrativo** de nuevos usuarios (Admin/Docente/Alumno)
- **Suspensión lógica (Soft Delete)** de cuentas sin pérdida de historial
- **CRUD de Materias y Cursos** institucional con asignación de docentes
- **Auditoría e Inspección de Logs** del sistema en tiempo real (`SystemInspector`) con cobertura de más de 50 categorías de eventos
- **Exportación de reportes** a PDF, Excel (.xls) y CSV según el panel
- **💾 Descargar Backup BD** — genera un dump `.sql` de Supabase; contiene datos personales: debe cifrarse y almacenarse con acceso restringido
- **📥 Restaurar Backup BD** — exige escribir `RESTAURAR` y solo acepta archivos `.sql` de hasta 5 MB. El backend está deshabilitado por defecto; en producción requiere `Features__EnableDatabaseRestore=true` y `Features__AllowRestoreInProduction=true` en Render. El botón se controla con `databaseRestoreEnabled` en `environment.prod.ts`. Procedimiento seguro con `psql` en [OPERACION_BACKUP.md](docs/OPERACION_BACKUP.md)

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
# Pruebas unitarias del backend (125)
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
| `POST` | `/api/privacy/consent/revoke` | Revoca la autorización vigente en `ConsentimientoHabeasData` |
| `POST` | `/api/privacy/arco` | Radica solicitud formal ARCO con cálculo automático de SLA legal (10 o 15 días hábiles en Colombia) |
| `GET` | `/api/privacy/arco` | Lista las solicitudes ARCO del usuario/acudiente autenticado |
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
| `POST` | `/api/admin/restore-backup` | 📥 Restauración con confirmación `RESTAURAR`; el backend está deshabilitado por defecto y en producción requiere las banderas `Features__EnableDatabaseRestore` y `Features__AllowRestoreInProduction` en Render |
| `GET` | `/api/admin/compliance` | Resumen de cumplimiento: cobertura de consentimiento y alertas |
| `GET` | `/api/admin/audit-logs` | Consulta paginada de la auditoría persistida en `audit_logs` |
| `GET` | `/api/admin/audit-logs/download` | Descarga del archivo consolidado de auditoría (`.log`) |

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

### Logs del sistema (`/api/systemfiles`)

| Método | Ruta | Descripción |
| :--- | :--- | :--- |
| `GET` | `/api/systemfiles/tree` | Árbol de archivos de log |
| `GET` | `/api/systemfiles/content` | Contenido en vivo de logs |

> La auditoría de negocio se consulta en `/api/admin/audit-logs` y se persiste en la tabla
> `audit_logs` de PostgreSQL. `/api/systemfiles` expone los archivos `.log` del contenedor.

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
| `Cross-Origin-Resource-Policy` | `cross-origin` |
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

**Verificación post-despliegue (25/10/2026)**

- ✅ `main`, `fix/audit-2026-10`, `origin/main` y `origin/fix/audit-2026-10` apuntan a `11938e0f`.
- ✅ CI y CodeQL en verde para `11938e0f`; Deploy Angular to GitHub Pages en verde para `d2dad0cf`.
- ✅ Backend Release: 0 errores, 0 advertencias y 55/55 pruebas.
- ✅ Frontend: `npm ci`, build de producción y 6/6 pruebas Karma; bundle inicial de 152,4 KB.
- ✅ API de producción: `/api/ping` 200, `/api/health` 200 (`database: Healthy`) y `/api/health/live`
  200; Swagger 404; CORS y cabeceras de seguridad verificados.
- ✅ Supabase PostgreSQL 17.6; `audit_logs` con 125 registros, RLS habilitado y 3 índices; 18 usuarios
  y 10 con consentimiento.
- ⚠️ Pendientes: `storage: Local` en Render, respaldos automáticos ausentes, datos institucionales
  incompletos y página pública de privacidad desactualizada (dice «12 caracteres»).

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
- ✅ **Exportación PDF** de reportes del docente con `jsPDF` + `html2canvas` (sustituido después por
  `jspdf-autotable`)
- ✅ **Exportación CSV** con `PapaParse` (sustituido después por exportación nativa con `Blob` UTF-8)
- ✅ **`ReportExportService`** — Servicio centralizado de exportación con tablas y tarjetas de resumen

### v1.1.0 — 2026-09-11

- ✅ `SecurityHeadersMiddleware` — Cabeceras HTTP de seguridad
- ✅ `RequestLoggingMiddleware` + `FileLogger` — Logging a archivos `.log` con rotación automática
- ✅ `GlobalExceptionMiddleware` — Manejo estandarizado de excepciones
- 🗑️ Eliminados `backend/.htaccess` y `backend/helpers/logger.php` (incompatibles con .NET/Kestrel)

### v1.0.0 — 2026-09-05 _(Sprint inicial)_

- ✅ Arquitectura base .NET 8 Web API + Angular 18 Standalone
- ✅ Autenticación JWT con `AuthController` y `TokenService`
- ✅ Panel de Docente — CRUD de alumnos, cursos y matrícula
- ✅ `LMSDbContext` con Fluent API en snake_case
- ✅ Seeding automático de roles (`Admin`, `Teacher`, `Student`) y grados (1° a 6°)
- ✅ Interceptor JWT automático en Angular

---

> **Desarrollado por:** Sneider M. · LMS Primaria · 2026
