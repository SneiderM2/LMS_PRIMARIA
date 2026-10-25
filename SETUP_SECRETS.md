# Configuración de secretos y despliegue seguro

> **Nunca** subas secretos al repositorio. El archivo `backend/LMS.API/appsettings.Local.json`
> está en `.gitignore` y en `.dockerignore`, y no se publica en la imagen.

---

## 1. Variables de entorno requeridas

ASP.NET Core lee las variables con doble guion bajo (`__`) para navegar la configuración.

| Variable | Obligatoria | Ejemplo / valor |
| :--- | :---: | :--- |
| `ConnectionStrings__DefaultConnection` o `DATABASE_URL` | Sí | `Host=...;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<secreto>;SSL Mode=Require;Trust Server Certificate=false;` |
| `Jwt__SecretKey` | Sí | Cadena aleatoria de **48+ caracteres** (recomendado 64, Base64). La API no arranca sin ella. |
| `Jwt__Issuer` | No | `LMS.API` |
| `Jwt__Audience` | No | `LMS.Client` |
| `Jwt__ExpirationMinutes` | No | `480` (8 horas); máximo 1440 |
| `Recaptcha__SecretKey` | Sí en producción | Clave secreta de reCAPTCHA v2. Sin ella el login/registro se rechazan (*fail-closed*). |
| `Recaptcha__ExpectedHostname` | Recomendada | `sneiderm2.github.io` |
| `Recaptcha__AllowDevBypass` | No | `false`; solo se acepta en Development. |
| `Authentication__Google__ClientId` | Solo si se usa Google | `xxxx.apps.googleusercontent.com` |
| `Privacy__EvidenceHmacKey` | Sí | Clave aleatoria de **32+ caracteres**, distinta de la JWT, para firmar consentimientos. |
| `Privacy__EnableInactivityAnonymization` | No | `false` por defecto. Activar solo con aprobación del responsable del tratamiento. |
| `Privacy__InactivityAnonymizationDryRun` | No | `true` por defecto; simula sin modificar datos. |
| `Privacy__InactivityAnonymizationMonths` | No | `36` meses por defecto. |
| `Registration__TeacherInviteCode` | Recomendada | Código entregado a docentes; vacío deshabilita su auto-registro |
| `Supabase__ProjectRef` | Si la URL no incluye el usuario completo | `abcdefghijklmnop` |
| `Cors__AllowedOrigins__0` | Sí | `https://sneiderm2.github.io` |
| `Cors__AllowedOrigins__1` | No | `http://localhost:4200` |
| `Storage__Provider` o `STORAGE_PROVIDER` | Recomendada | `Local` (por defecto) o `Supabase` para almacenamiento persistente en la nube |
| `Storage__Supabase__Url` o `SUPABASE_URL` | Si Provider=Supabase | URL del proyecto Supabase (ej. `https://<ref>.supabase.co`) |
| `Storage__Supabase__ServiceRoleKey` o `SUPABASE_SERVICE_ROLE_KEY` | Si Provider=Supabase | Service Role Key (privada, para escribir en el bucket privado) |
| `Storage__Supabase__Bucket` o `SUPABASE_STORAGE_BUCKET` | No | Nombre del bucket (`lms-uploads` por defecto) |
| `Storage__AllowLocalFallback` | No | `false` por defecto. No habilitar en producción con disco efímero. |
| `Storage__RootPath` | No | Ruta privada para almacenamiento local; por defecto `App_Data/uploads` |
| `Database__TrustServerCertificate` | No | `false` por defecto. No habilitar salvo diagnóstico controlado. |
| `Seed__Enabled` | No | `false` (nunca `true` en producción salvo instalación inicial) |
| `Features__EnableDatabaseRestore` | No | `false` |
| `Features__AllowRestoreInProduction` | No | `false` |
| `Features__AllowLogPurge` | No | `false` |

---

## 2. Desarrollo local

1. Copia el archivo de ejemplo:

   ```powershell
   Copy-Item backend\LMS.API\appsettings.Example.json backend\LMS.API\appsettings.Local.json
   ```

2. Completa los valores reales en `appsettings.Local.json`.
3. Ejecuta:

   ```powershell
   dotnet run --project backend\LMS.API\LMS.API.csproj
   ```

   El perfil `LMS.API` usa `ASPNETCORE_ENVIRONMENT=Development` y `http://localhost:5000`.

4. Alternativa con User Secrets (solo desarrollo):

   ```powershell
   cd backend\LMS.API
   dotnet user-secrets init
   dotnet user-secrets set "Jwt:SecretKey" "<clave>"
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cadena>"
   ```

> `appsettings.Local.json` se carga **solo** cuando `ASPNETCORE_ENVIRONMENT=Development`.
> En cualquier otro entorno use variables de entorno o un proveedor de secretos. En Docker el
> archivo no se copia.

### Generar una clave JWT segura

```powershell
$bytes = New-Object byte[] 64
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes); $rng.Dispose()
[Convert]::ToBase64String($bytes)
```

---

## 3. Producción en Render

1. En **Render → Environment**, define todas las variables de la tabla.
2. No uses `DATABASE_URL` con la contraseña en texto plano en el repositorio; usa el panel de Render.
3. Verifica que `Cors__AllowedOrigins__0` sea exactamente `https://sneiderm2.github.io` (sin `/` final).
4. Configura `Recaptcha__ExpectedHostname=sneiderm2.github.io`.
5. Despliega con el `Dockerfile`; el contenedor corre como usuario sin privilegios `lmsapp`.
6. **Pendiente:** define `Storage__Provider=Supabase` con URL, Service Role Key y bucket. El plan
   Free de Render **no admite discos persistentes**; usa `Local` solo en desarrollo o con un disco
   montado en un plan superior. No uses `Local` con disco efímero para archivos reales.
7. Comprueba:
   - `GET /api/ping` → 200.
   - `GET /api/health` → 200, `database: "Healthy"` y `storage` acorde al proveedor configurado.
   - `GET /swagger` → 404 en producción (Swagger deshabilitado).
   - Cabeceras `Strict-Transport-Security` y `X-Content-Type-Options` presentes.

### 3.1 Estado verificado de producción (25/10/2026)

| Elemento | Estado observado | Implicación |
| :--- | :--- | :--- |
| Render `LMS_PRIMARIA` | Plan **Free**, región US West según el CNAME `gcp-us-west1-1.origin.onrender.com`; auto-deploy desde `main`. Última verificación sobre `11938e0f`. | La instancia se suspende por inactividad; los 503 iniciales son *cold start*. |
| Disco de Render | No hay disco. El plan Free no admite discos persistentes. | `Storage:Provider=Local` es efímero y no sirve para archivos reales. |
| `/api/health` de producción | `database: "Healthy"`, `storage: "Local"`. | La API funciona, pero el almacenamiento de archivos no es persistente. |
| Variables de entorno en Render | No fue posible leer el panel desde este entorno. La API arranca y responde, lo que confirma que JWT y HMAC de evidencias están configurados. No es posible verificar aquí la clave de reCAPTCHA; si faltara, login y registro fallarían cerrados. El único efecto observable es que `Storage` sigue en `Local`. | Falta configurar el proveedor de almacenamiento y su Service Role Key. |
| Supabase | PostgreSQL **17.6**; proyecto activo y saludable. Bucket `lms-uploads`: privado (`public=false`), límite de 25 MB (`26214400` bytes). | El bucket está listo; falta conectar la API y programar respaldos. |
| Datos | 18 usuarios; 10 con consentimiento vigente (55,6 %). | La cobertura está por debajo de la meta del 95 %; revisar consentimientos pendientes. |
| Tabla `audit_logs` | Activa con 125 registros; último 25/10/2026 22:31 UTC; 45 nuevos desde 21:50 UTC. RLS habilitado, 0 políticas y 3 índices (`audit_logs_pkey`, `idx_audit_logs_timestamp`, `idx_audit_logs_level`). | La auditoría persiste en producción; solo el propietario de la tabla escribe. |
| Respaldos | Supabase Free no muestra backups automáticos ni PITR. | No se cumple el RPO/RTO documentado; falta un respaldo lógico programado y cifrado. |
| Frontend publicado | GitHub Pages sirve `main-EVFOMQUY.js` y `styles-T7YOE4RF.css`, con CSP por `<meta>`. | El código endurecido ya está desplegado. La página `privacidad.html` aún dice «12 caracteres» y «Por definir». |

**Decisión de almacenamiento:** mientras Render sea Free, usar **Supabase Storage** con el bucket
privado `lms-uploads` ya creado. Falta definir en Render `Storage__Provider=Supabase`,
`Storage__Supabase__Url=https://<ref>.supabase.co`,
`Storage__Supabase__Bucket=lms-uploads` y `Storage__Supabase__ServiceRoleKey` (copiada desde
**Project Settings → API Keys → Secret keys**; nunca en el repositorio ni en el chat), y desplegar
el código que usa `SupabaseStorageService`. Si se sube de plan y se monta un disco, se puede volver
a `Local` apuntando `Storage__RootPath` al disco. No habilitar `Storage:AllowLocalFallback` en
producción.

---

## 4. Rotación obligatoria de credenciales

Los siguientes secretos estuvieron versionados en el historial de Git y deben considerarse
**comprometidos**:

- Contraseña del usuario `postgres` de Supabase.
- Clave `Jwt:SecretKey` anterior.
- Clave secreta de reCAPTCHA.

Procedimiento:

1. **Supabase:** *Project Settings → Database → Reset database password*. Actualiza
   `ConnectionStrings__DefaultConnection` en Render.
2. **JWT:** genera una clave nueva y actualiza `Jwt__SecretKey`. Todos los usuarios deberán
   iniciar sesión de nuevo (comportamiento esperado).
3. **reCAPTCHA:** crea un par de claves nuevo en Google reCAPTCHA y actualiza
   `Recaptcha__SecretKey` y la *site key* del frontend (`environment*.ts`).
4. **Google OAuth:** si se usa, restringe el Client ID al dominio autorizado.
5. **Cuentas por defecto:** inicia sesión como administrador y cambia las contraseñas de
   `admin`, `docente` y `estudiante`, o desactívalas si no se usan.
6. Purga el historial de Git solo con autorización y coordinación del equipo:

   ```bash
   # Ejemplo con git-filter-repo (requiere instalación y respaldo previo)
   git filter-repo --path backend/LMS.API/appsettings.json --invert-paths
   ```

   La rotación de secretos es obligatoria incluso después de purgar el historial.

---

## 5. Lista de verificación antes de publicar

- [ ] No hay secretos en `git grep` ni en `git log -p`.
- [ ] `appsettings.Local.json` está ignorado y fuera del contexto Docker.
- [ ] `Jwt__SecretKey` tiene 48+ caracteres (recomendado 64) y es única por entorno.
- [ ] `Privacy__EvidenceHmacKey` tiene 32+ caracteres y es distinta de la JWT.
- [ ] `Recaptcha__SecretKey` está configurada; no se depende de bypass en producción.
- [ ] `Storage__Provider` apunta a almacenamiento persistente y `AllowLocalFallback=false`.
- [ ] `audit_logs` existe, tiene RLS habilitado y recibe registros reales.
- [ ] Existe un respaldo lógico programado, cifrado y con prueba de restauración.
- [ ] `Privacy__EnableInactivityAnonymization` y `InactivityAnonymizationDryRun` fueron revisados.
- [ ] `Database__TrustServerCertificate=false` y el certificado TLS es válido.
- [ ] CORS no contiene `*` ni `SetIsOriginAllowed(_ => true)`.
- [ ] Swagger no está expuesto en producción.
- [ ] `Seed__Enabled=false` y las cuentas demo están rotadas o desactivadas.
- [ ] `Features__EnableDatabaseRestore=false`.
- [ ] HTTPS/HSTS activo y certificado de base de datos validado.
- [ ] Se ejecutó `scripts/security_smoke_test.ps1`.
- [ ] Se revisaron dependencias vulnerables (`dotnet list package --vulnerable`, `npm audit`).
- [ ] El responsable del tratamiento aprobó la política y completó los datos de `privacidad.html`
      (NIT, razón social, dirección, teléfono, correo, DPO y encargados).
- [ ] `privacidad.html` refleja la política real de contraseñas (8–64 caracteres, no 12).
