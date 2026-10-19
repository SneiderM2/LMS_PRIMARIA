# Configuración de secretos y despliegue seguro

> **Nunca** subas secretos al repositorio. El archivo `backend/LMS.API/appsettings.Local.json`
> está en `.gitignore` y en `.dockerignore`, y no se publica en la imagen.

---

## 1. Variables de entorno requeridas

ASP.NET Core lee las variables con doble guion bajo (`__`) para navegar la configuración.

| Variable | Obligatoria | Ejemplo / valor |
| :--- | :---: | :--- |
| `ConnectionStrings__DefaultConnection` o `DATABASE_URL` | Sí | `Host=...;Port=5432;Database=postgres;Username=postgres.<ref>;Password=<secreto>;SSL Mode=Require;Trust Server Certificate=false;` |
| `Jwt__SecretKey` | Sí | Cadena aleatoria de **64+ caracteres** (Base64). Sin valor por defecto. |
| `Jwt__Issuer` | No | `LMS.API` |
| `Jwt__Audience` | No | `LMS.Client` |
| `Jwt__ExpirationMinutes` | No | `480` (8 horas); máximo 1440 |
| `Recaptcha__SecretKey` | Sí (si se usa CAPTCHA) | Clave secreta de reCAPTCHA v2 |
| `Recaptcha__ExpectedHostname` | Recomendada | `sneiderm2.github.io` |
| `Authentication__Google__ClientId` | Solo si se usa Google | `xxxx.apps.googleusercontent.com` |
| `Privacy__EvidenceHmacKey` | Recomendada | Clave aleatoria distinta de la JWT para firmar consentimientos |
| `Registration__TeacherInviteCode` | Recomendada | Código entregado a docentes; vacío deshabilita su auto-registro |
| `Supabase__ProjectRef` | Si la URL no incluye el usuario completo | `abcdefghijklmnop` |
| `Cors__AllowedOrigins__0` | Sí | `https://sneiderm2.github.io` |
| `Cors__AllowedOrigins__1` | No | `http://localhost:4200` |
| `Storage__RootPath` | No | Ruta privada; por defecto `App_Data/uploads` |
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

> `appsettings.Local.json` se carga en cualquier entorno si existe, pero las variables de
> entorno tienen prioridad. En Docker el archivo no se copia.

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
5. Despliega con el `Dockerfile`; el contenedor corre como usuario `appuser`.
6. Comprueba:
   - `GET /api/ping` → 200.
   - `GET /api/health` → 200 y `database: connected`.
   - `GET /swagger` → 404 en producción (Swagger deshabilitado).
   - Cabeceras `Strict-Transport-Security` y `X-Content-Type-Options` presentes.

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
- [ ] `Jwt__SecretKey` tiene 64+ caracteres y es única por entorno.
- [ ] CORS no contiene `*` ni `SetIsOriginAllowed(_ => true)`.
- [ ] Swagger no está expuesto en producción.
- [ ] `Seed__Enabled=false` y las cuentas demo están rotadas o desactivadas.
- [ ] `Features__EnableDatabaseRestore=false`.
- [ ] HTTPS/HSTS activo y certificado de base de datos validado.
- [ ] Se ejecutó `scripts/security_smoke_test.ps1`.
- [ ] Se revisaron dependencias vulnerables (`dotnet list package --vulnerable`, `npm audit`).
- [ ] El responsable del tratamiento aprobó la política y completó los datos de `privacidad.html`.
