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
| Backend | .NET 8 (LTS), última versión desplegada |
| Frontend | Angular 18 |
| Base de datos | PostgreSQL 16 (Supabase) |
| Navegadores | Dos últimas versiones de Chrome, Edge, Firefox y Safari |

---

## 3. Controles implementados

### Autenticación y sesión

- JWT firmado con HMAC-SHA256; clave de al menos 48 caracteres obligatoria, sin valores por defecto.
- Expiración configurable (8 h por defecto) y validación estricta de emisor, audiencia, firma y vigencia.
- Revocación por **sesión única**: cada inicio de sesión invalida el token anterior.
- Bloqueo temporal de 15 minutos tras 5 intentos fallidos; mensajes de error uniformes.
- Contraseñas con BCrypt (factor 12) y política mínima de 12 caracteres, sin contraseñas comunes.
- CAPTCHA obligatorio en login y registro (reCAPTCHA), con *fail-closed* si no está configurado.
- Google OAuth solo con validación criptográfica del ID token y cuenta institucional preexistente.

### Autorización

- Roles canónicos `Admin`, `Teacher`, `Student` y políticas de autorización.
- Validación de pertenencia en cursos, tareas, entregas y calificaciones (prevención de IDOR).
- El registro público **no permite** crear administradores; los docentes requieren código de invitación.
- Descarga de archivos autenticada y autorizada por propietario, docente del curso o administrador.

### Protección de datos y privacidad

- Consentimiento informado con evidencia firmada (HMAC-SHA256): versión, fecha, IP, agente de usuario y acudiente enmascarado.
- Autorización del acudiente obligatoria para menores (Ley 1581 de 2012, art. 7).
- Endpoints de acceso/portabilidad, rectificación y supresión (`/api/privacy/*`).
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

- Secretos por variables de entorno; `appsettings.Local.json` ignorado por Git y por Docker.
- Contenedor ejecutado como usuario sin privilegios, con `Server` header deshabilitado.
- TLS obligatorio en la base de datos (`SSL Mode=Require`, `Trust Server Certificate=false`).
- Respaldos con advertencia de datos personales y procedimiento de restauración controlado.
- Registro de auditoría con retención configurable.

---

## 4. Riesgos conocidos y pendientes

| Riesgo | Estado | Mitigación / siguiente paso |
| :--- | :--- | :--- |
| Token JWT almacenado en `localStorage` | Aceptado con mitigaciones | CSP no controlable en GitHub Pages; evaluar cookies `HttpOnly` + `SameSite` con un proxy del mismo sitio. |
| GitHub Pages no permite cabeceras HTTP | Aceptado | No alojar datos sensibles en el frontend; evaluar migrar a Cloudflare Pages/Netlify. |
| Secreto JWT y contraseña de BD estuvieron en el historial de Git | **Acción obligatoria** | Rotar credenciales y clave JWT; ver `SETUP_SECRETS.md`. |
| Cuentas `admin`, `docente`, `estudiante` con contraseñas iniciales débiles | **Acción obligatoria** | Rotar contraseñas o desactivar cuentas; el sistema ya no las crea ni reactiva. |
| Migraciones de esquema en tiempo de ejecución | Deuda técnica | Migrar a EF Core Migrations y aprobar cambios por revisión. |
| Pruebas automatizadas limitadas | En progreso | 26 pruebas unitarias (xUnit); pendiente integración/E2E y OWASP ZAP. |

---

## 5. Buenas prácticas para contribuyentes

1. No subir secretos, tokens, archivos `.env` ni `appsettings.Local.json`.
2. No registrar datos personales en `console.log`, `Debug.WriteLine` ni mensajes de error.
3. Usar `[Authorize]` y validar pertenencia de recursos en cada endpoint nuevo.
4. Validar entradas con DataAnnotations y límites de tamaño.
5. Ejecutar `dotnet build` y `npm run build` antes de abrir un pull request.
6. Revisar dependencias con `dotnet list package --vulnerable` y `npm audit`.

---

## 6. Divulgación

Este documento puede compartirse con auditores, la SIC, el ICBF o el oficial de protección de
datos de la institución. No contiene credenciales ni detalles explotables.
