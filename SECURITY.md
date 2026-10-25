# Política de Seguridad de la Información — LMS Primaria

Este documento describe cómo reportar vulnerabilidades y resume los controles de seguridad
implementados en la plataforma. Aplica al backend (`backend/LMS.API`), al frontend
(`frontend/`) y a la infraestructura de despliegue (Render, Supabase, GitHub Pages).

---

## 1. Reporte responsable de vulnerabilidades

Si identificas una vulnerabilidad, **no la publiques** ni explotes datos de menores.
Escríbenos de forma privada a:

- **Correo de seguridad:** `seguridad@lmsprimaria.edu.co` (reemplazar por el buzón institucional real).
- **Asunto sugerido:** `[SECURITY] <componente> — <resumen>`.
- Incluye: descripción, pasos de reproducción, impacto estimado y, si es posible, una prueba de concepto **sin datos reales**.

Compromisos:

| Actividad | Plazo objetivo |
| :--- | :--- |
| Acuse de recibo | 2 días hábiles |
| Evaluación inicial y clasificación | 5 días hábiles |
| Plan de corrección para hallazgos críticos | 10 días hábiles |
| Comunicación al titular afectado (si aplica) | Inmediata, según el plan de incidentes |

No se tomarán represalias contra personas que reporten de buena fe. No se aceptan pruebas que
impliquen denegación de servicio, ingeniería social, acceso a cuentas de terceros o extracción
masiva de datos personales.

---

## 2. Versiones soportadas

| Componente | Versión soportada |
| :--- | :--- |
| Backend | .NET 8 (LTS) — **fin de soporte 10/11/2026** |
| Frontend | Angular 18 — **fuera de soporte; migración mayor pendiente** |
| Base de datos | PostgreSQL 17.6 (Supabase) |
| Navegadores | Dos últimas versiones de Chrome, Edge, Firefox y Safari |

> La migración a .NET 10 y a una versión soportada de Angular no se ejecutó en la auditoría
> 2026-10. Es una deuda de plataforma con fecha límite y debe planificarse antes del EOL de .NET 8.

### 2.1 Estado verificado de producción (25/10/2026)

| Comprobación | Resultado |
| :--- | :--- |
| Repositorio | `main` = `fix/audit-2026-10` = `11938e0f`; CI y CodeQL en `success`. |
| Backend Release | 0 errores, 0 advertencias; 55/55 pruebas. |
| Frontend | Build de producción correcto; 6/6 pruebas Karma; Pages sirve `main-EVFOMQUY.js` y `styles-T7YOE4RF.css`. |
| Salud de la API | `/api/ping` 200; `/api/health` 200 (`database: Healthy`, `storage: Local`); `/api/health/live` 200. |
| Swagger | `404` en producción. |
| CORS | Origen `https://sneiderm2.github.io` permitido; preflight `204`; origen ajeno sin `Access-Control-Allow-Origin`. |
| Cabeceras | HSTS, CSP, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy`, COOP/CORP y `Permissions-Policy`. |
| Base de datos | PostgreSQL 17.6; 18 usuarios; 10 con consentimiento vigente (55,6 %). |
| Auditoría | `audit_logs` activa: 125 registros, último 25/10/2026 22:31 UTC, 45 nuevos desde 21:50 UTC; RLS habilitado, 0 políticas y 3 índices. |
| Dependencias | NuGet sin paquetes vulnerables; `npm audit --omit=dev --audit-level=critical` con código 0; 9 vulnerabilidades de Angular 18 (4 moderadas, 5 altas, 0 críticas). |
| Respaldos | Supabase Free sin backups automáticos visibles; no hay respaldo lógico programado. |

---

## 3. Controles implementados

### Autenticación y sesión

- JWT firmado con HMAC-SHA256; clave de al menos 48 caracteres obligatoria, sin valores por defecto.
- Expiración configurable (8 h por defecto) y validación estricta de emisor, audiencia, firma y vigencia.
- Revocación por **sesión única**: cada inicio de sesión invalida el token anterior. El servidor
  rechaza tokens revocados, nulos, de cuentas inactivas o anonimizadas.
- Bloqueo temporal de 15 minutos tras 5 intentos fallidos; mensajes de error uniformes.
- Contraseñas con BCrypt (factor 12) y política de **8 a 64 caracteres** con mayúscula, minúscula,
  número, símbolo y bloqueo de contraseñas comunes o datos predecibles del titular.
- CAPTCHA obligatorio en login y registro (reCAPTCHA), con *fail-closed* si no está configurado.
  El bypass solo existe en Development y requiere habilitación explícita.
- Google OAuth solo con validación criptográfica del ID token y cuenta institucional preexistente.
- La aceptación de la versión vigente de la política se exige en el servidor para los endpoints
  distintos de los de consentimiento, consulta de datos y cierre de sesión.

### Autorización

- Roles canónicos `Admin`, `Teacher`, `Student` y políticas de autorización.
- Validación de pertenencia en cursos, tareas, entregas y calificaciones (prevención de IDOR).
- El registro público **no permite** crear administradores; los docentes requieren código de invitación.
- Descarga de archivos autenticada y autorizada por propietario, docente del curso o administrador.

### Protección de datos y privacidad

- Consentimiento informado con evidencia firmada (HMAC-SHA256) usando una clave dedicada
  (`Privacy:EvidenceHmacKey`, ≥32 caracteres, distinta de la JWT): versión, fecha, IP, agente de
  usuario y acudiente enmascarado.
- Autorización del acudiente obligatoria para menores (Ley 1581 de 2012, art. 7).
- Endpoints de acceso/portabilidad, rectificación y supresión (`/api/privacy/*`). La supresión
  elimina también la evidencia de consentimiento, el último acceso y los contadores de intentos.
- Retención por inactividad configurable, deshabilitada y en modo simulación por defecto.
- Minimización de datos: exportaciones sin contraseñas ni tokens; logs con redacción de parámetros sensibles.
- Archivos fuera de `wwwroot`, con validación de firma binaria y nombre saneado.

### Aplicación y API

- CORS con lista blanca explícita (sin comodines ni credenciales).
- Limitación de solicitudes: 30/min en autenticación, 60/h en subidas y 600/min global por IP.
- Cabeceras de seguridad: CSP restrictiva, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy`, `Permissions-Policy`, COOP/CORP.
- HSTS con `max-age` de un año en producción.
- Errores genéricos en producción; sin trazas ni mensajes internos en las respuestas.
- Swagger deshabilitado en producción.
- Validación de modelo y límites de tamaño de archivo y de cuerpo de la petición.

### Infraestructura

- Secretos por variables de entorno; `appsettings.Local.json` ignorado por Git y por Docker y
  cargado únicamente en Development.
- Contenedor ejecutado como usuario sin privilegios (`lmsapp`), con `Server` header deshabilitado.
- TLS obligatorio en la base de datos (`SSL Mode=Require`); `Trust Server Certificate` es `false`
  por defecto y solo puede habilitarse explícitamente por configuración.
- Respaldos con advertencia de datos personales y procedimiento de restauración controlado; el
  respaldo automático de Supabase Free sigue pendiente.
- Auditoría en dos destinos: tabla `audit_logs` en PostgreSQL (RLS, 3 índices) y archivos `.log`
  del contenedor con retención configurable y salida a stdout para Render.

---

## 4. Riesgos conocidos y pendientes

| Riesgo | Estado | Mitigación / siguiente paso |
| :--- | :--- | :--- |
| Token JWT almacenado en `localStorage` | Aceptado con mitigaciones | El frontend aplica CSP por `<meta>` y evita `innerHTML`; evaluar cookies `HttpOnly` + `SameSite` con un proxy del mismo sitio. |
| GitHub Pages no permite cabeceras HTTP | Aceptado | Se añadió CSP por `<meta>` solo en el build de producción (`src/index.production.html`); `frame-ancestors` y `sandbox` solo pueden aplicarse desde un hosting con cabeceras. |
| Secreto JWT y contraseña de BD estuvieron en el historial de Git | **Acción obligatoria** | Rotar credenciales y clave JWT; ver `SETUP_SECRETS.md`. |
| Cuentas `admin`, `docente`, `estudiante` con contraseñas iniciales débiles | **Acción obligatoria** | Rotar contraseñas o desactivar cuentas; el sistema ya no las crea ni reactiva. |
| .NET 8 sin soporte desde 10/11/2026 y Angular 18 fuera de soporte | **Riesgo alto** | Planificar migración mayor a .NET 10 y Angular soportado; no se ejecutó por falta de autorización. |
| Vulnerabilidades altas/moderadas de Angular 18 | Aceptado temporalmente | Verificado el 25/10/2026: 9 vulnerabilidades (4 moderadas, 5 altas, 0 críticas) en dependencias de producción; el resto exige actualización mayor. `dompurify` crítico corregido por override. |
| `Storage:Provider=Local` en producción | **Acción obligatoria** | Verificado de nuevo el 25/10/2026: Render está en plan Free, sin disco, y `/api/health` devuelve `storage: Local`. El bucket privado `lms-uploads` ya existe en Supabase (25 MB, sin acceso público); falta configurar `Storage__Provider=Supabase`, la URL y la Service Role Key en Render y desplegar. El fallback silencioso está deshabilitado. |
| `audit_logs` ausente en producción | **Resuelto y verificado** | La tabla está activa en Supabase desde el 25/10/2026: 125 registros, último 22:31 UTC, 45 nuevos desde 21:50 UTC, RLS habilitado, 3 índices y 0 políticas (solo el propietario escribe). `DbInitializer` la crea de forma idempotente y habilita RLS en entornos nuevos. |
| Supabase Free sin backups automáticos | **Acción obligatoria** | El panel muestra «No backups». No existe respaldo lógico programado ni copia cifrada externa. Definirlos, o pasar a un plan con backups/PITR, antes de usar datos reales. |
| Anonimización por inactividad deshabilitada | Configurable | Activar solo con aprobación del responsable del tratamiento; iniciar en modo simulación. |
| Política pública de privacidad desactualizada | **Acción obligatoria** | `frontend/public/privacidad.html` todavía indica «12 caracteres» y conserva «Por definir»; la implementación real exige 8–64. Corregir el texto antes de una auditoría legal. |
| Política de privacidad con datos institucionales incompletos | **Acción obligatoria** | Completar NIT, razón social, dirección, teléfono, correo, DPO y encargados antes de una auditoría legal. |
| Migraciones de esquema en tiempo de ejecución | Deuda técnica | Migrar a EF Core Migrations y aprobar cambios por revisión. |
| Pruebas automatizadas limitadas | En progreso | Verificado el 25/10/2026: 55/55 pruebas backend (xUnit) y 6/6 frontend (Karma/Jasmine); pendiente integración/E2E y OWASP ZAP. |

---

## 5. Buenas prácticas para contribuyentes

1. No subir secretos, tokens, archivos `.env` ni `appsettings.Local.json`.
2. No registrar datos personales en `console.log`, `Debug.WriteLine` ni mensajes de error.
3. Usar `[Authorize]` y validar pertenencia de recursos en cada endpoint nuevo.
4. Validar entradas con DataAnnotations y límites de tamaño.
5. Ejecutar `dotnet build`, `dotnet test`, `npm run build` y `npm test` antes de abrir un pull request.
6. Revisar dependencias con `dotnet list package --vulnerable` y `npm audit`.

---

## 6. Divulgación

Este documento puede compartirse con auditores, la SIC, el ICBF o el oficial de protección de
datos de la institución. No contiene credenciales ni detalles explotables.
