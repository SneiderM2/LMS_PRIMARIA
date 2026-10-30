# LMS Primaria — Backend API (.NET 8 Web API) 🛡️🚀

Servicio backend de alto rendimiento para la plataforma educativa **LMS Primaria**, desarrollado en **ASP.NET Core 8 (.NET 8)** con arquitectura limpia, Entity Framework Core 8 sobre PostgreSQL (Supabase Cloud), seguridad defensiva OWASP y cumplimiento integral de la normativa colombiana de protección de datos personales (Ley 1581 de 2012, Ley 1098 de 2006 y Circular Externa 002 de 2015 de la SIC).

---

## 🏛️ Arquitectura del Servicio

- **Framework:** .NET 8 Web API (`net8.0`)
- **ORM & Driver:** Entity Framework Core 8 + Npgsql
- **Persistencia:** PostgreSQL 17.6 administrado en Supabase Cloud con Connection Pooling en Session Mode (Puerto 5432).
- **Autenticación:** JWT Bearer tokens con firma simétrica HMAC-SHA256 y Google OAuth 2.0 (Google Identity Services).
- **Control de Acceso:** Rol-based Access Control (RBAC) con roles canónicos: `Admin`, `Teacher`, `Student`.
- **Criptografía de Contraseñas:** BCrypt.Net-Next (work factor 12).
- **Firmado de Evidencias de Privacidad:** HMAC-SHA256 con clave simétrica dedicada (`EvidenceHmacKey`).
- **Antifraude y Antibots:** Google reCAPTCHA con política fail-closed en producción.

---

## 📜 Endpoints de Privacidad y Derechos ARCO (`PrivacyController`)

La ruta base `/api/privacy` expone las operaciones legales y técnicas conforme a la Ley 1581 de 2012 y el Decreto 1377 de 2013:

| Método | Endpoint | Autorización | Propósito y Descripción |
| :--- | :--- | :---: | :--- |
| `GET` | `/api/privacy/policy` | Anónimo | Retorna la política de tratamiento, aviso de privacidad, datos del responsable institucional y vigencia. |
| `POST` | `/api/privacy/consent` | JWT Bearer | Registra la manifestación de consentimiento suscrita por el titular o acudiente con evidencia HMAC y metadatos forenses. |
| `POST` | `/api/privacy/consent/revoke` | JWT Bearer | Revoca la vigencia del consentimiento actual (`FechaRevocatoriaUtc = DateTime.UtcNow`) e invalida la operación del perfil. |
| `POST` | `/api/privacy/arco` | JWT Bearer | Radica formalmente un derecho ARCO (`CONOCER`, `ACTUALIZAR`, `RECTIFICAR`, `SUPRIMIR`, `REVOCAR`) calculando el vencimiento perentorio. |
| `GET` | `/api/privacy/arco` | JWT Bearer | Consulta el listado histórico de solicitudes ARCO radicadas por el titular o su acudiente con su respectivo SLA. |
| `GET` | `/api/privacy/my-data` | JWT Bearer | Descarga el expediente digital completo en JSON (derecho de acceso y portabilidad). |
| `PUT` | `/api/privacy/my-data` | JWT Bearer | Permite la rectificación de nombres y apellidos del titular registrado. |
| `POST` | `/api/privacy/delete-account`| JWT Bearer | Ejecuta la supresión y anonimización de la cuenta, previa verificación de credenciales y conservación disociada de historia académica. |

---

## 🛡️ Filtro de Protección de Menores (`MinorConsentActionFilter`)

El filtro de acción **`MinorConsentActionFilter`** intercepta las solicitudes entrantes dirigidas a los endpoints de estudiantes menores de edad (`StudentController`):

### Mecánica de Validación
1. Extrae el `UserId` desde los claims autenticados del token JWT (`ClaimTypes.NameIdentifier`).
2. Verifica en la base de datos que el usuario con rol de estudiante cuente con un registro en `consentimientos_habeas_data` con:
   - `AutorizaServicioEducativo == true`
   - `FechaRevocatoriaUtc == null` (consentimiento activo y no revocado)
   - `VersionPolitica` coincidente con la versión institucional declarada en configuración (`Privacy:PolicyVersion`).
3. **Respuesta Preventiva ante Incumplimiento:** Si no se satisface la condición legal, el filtro corta inmediatamente la ejecución de la petición y devuelve un código de estado HTTP **`403 Forbidden`** con el payload estructurado:

```json
{
  "error": "MINOR_CONSENT_REQUIRED",
  "message": "Se requiere la autorización vigente de su representante legal (Ley 1581 de 2012 / Ley 1098 de 2006) para operar en la plataforma.",
  "policyVersion": "2026-10-19"
}
```

---

## 🧪 Pruebas Automatizadas y de Seguridad

La suite de pruebas automatizadas garantiza la robustez, mitigación contra IDOR/BOLA, sanitización de auditoría y cálculo de días hábiles legales:

- **Estado de Pruebas:** **125 pruebas unitarias y de integración superadas (LMS.API.Tests)**.
- **Cobertura de la Suite:**
  - `AuthControllerSecurityTests`: Pruebas de fuerza bruta (bloqueo por 5 intentos fallidos), honeypots, validación fail-closed de reCAPTCHA y rotación de sesión.
  - `AdminControllerSecurityTests`: Prevención de auto-bloqueo del último admin y bloqueo de comandos SQL peligrosos en restauración.
  - `TeacherControllerSecurityTests`: Protección contra IDOR (un docente no puede modificar cursos, actividades o calificaciones de otro docente).
  - `StudentControllerSecurityTests`: Protección contra BOLA (un estudiante no puede inscribirse en cursos de otros grados ni entregar tareas ajenas).
  - `ConsentServiceTests`: Cálculo de 10 y 15 días hábiles colombianos con Ley Emiliani (Ley 51 de 1983) y algoritmo de Pascua dinámico (Meeus/Jones/Butcher), enmascaramiento de documentos y verificación HMAC.
  - `MiddlewareSecurityTests`: Cabeceras CSP, X-Frame-Options, detección de sesiones simultáneas y sanitización de PII en logs.

### Ejecución de Pruebas:
```bash
dotnet test backend/LMS.API.Tests/LMS.API.Tests.csproj --configuration Release
```

---

## ⚙️ Variables de Entorno y Configuración Segura

El backend lee su configuración a través de `IConfiguration` y variables de entorno del sistema. En producción (**Render**) se deben configurar las siguientes llaves sin literales ni credenciales quemadas:

```env
# Entorno y Puerto
ASPNETCORE_ENVIRONMENT=Production
PORT=8080

# Base de Datos PostgreSQL Supabase (Pooler IPv4)
ConnectionStrings__DefaultConnection=Host=aws-0-us-east-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<REEMPLAZAR_EN_RENDER>;Password=<REEMPLAZAR_EN_RENDER>;SSL Mode=Require;Trust Server Certificate=false;
DATABASE_URL=<REEMPLAZAR_EN_RENDER>
SUPABASE_PROJECT_REF=<REEMPLAZAR_EN_RENDER>

# Almacenamiento Supabase Storage (Bucket Privado)
STORAGE_PROVIDER=Supabase
Storage__Provider=Supabase
SUPABASE_URL=https://<REEMPLAZAR_EN_RENDER>.supabase.co
SUPABASE_SERVICE_ROLE_KEY=<REEMPLAZAR_EN_RENDER>
SUPABASE_STORAGE_BUCKET=lms-uploads

# Criptografía y JWT
Jwt__SecretKey=<REEMPLAZAR_EN_RENDER>
Jwt__Issuer=LMS.API
Jwt__Audience=LMS.Client
Jwt__ExpirationMinutes=480

# Evidencia Criptográfica Habeas Data
EVIDENCE_HMAC_KEY=<REEMPLAZAR_EN_RENDER>
Privacy__EvidenceHmacKey=<REEMPLAZAR_EN_RENDER>
Privacy__PolicyVersion=2026-10-19

# Antibots reCAPTCHA
RECAPTCHA_SITE_KEY=<REEMPLAZAR_EN_RENDER>
RECAPTCHA_SECRET_KEY=<REEMPLAZAR_EN_RENDER>
Recaptcha__ExpectedHostname=sneiderm2.github.io
Recaptcha__AllowDevBypass=false

# Sembrado Inicial de Producción
SEED_DEFAULT_PASSWORD=<REEMPLAZAR_EN_RENDER>

# CORS Estricto
CORS_ALLOWED_ORIGINS=https://sneiderm2.github.io
```

---

## 🚀 Ejecución en Desarrollo

```bash
cd backend/LMS.API
dotnet run
```
La API escuchará en `http://localhost:5000` con Swagger activo únicamente en modo `Development`.
