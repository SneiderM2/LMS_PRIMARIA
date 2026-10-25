# Operación de respaldos y restauración

**Objetivo:** garantizar la disponibilidad e integridad de la información académica y personal,
con copias cifradas, verificables y restaurables.

| Parámetro | Valor propuesto |
| :--- | :--- |
| RPO (pérdida máxima de datos) | 24 horas |
| RTO (tiempo máximo de recuperación) | 4 horas |
| Retención | 30 días (diarios) + 12 meses (mensual) |
| Cifrado | AES-256 en reposo y TLS en tránsito |
| Acceso | Solo administrador del sistema y DPO |
| Verificación | Restauración de prueba mensual en entorno aislado |

> ⚠️ **Estado actual (25/10/2026):** estos parámetros son el objetivo, no la operación vigente.
> Supabase Free no muestra backups automáticos ni PITR, no existe un `pg_dump` programado y no se
> ha ejecutado una prueba de restauración. El RPO/RTO **no se cumple** todavía. Los adjuntos, además,
> están en el disco efímero de Render (`storage: Local`), por lo que tampoco se respaldan.

---

## 1. Estrategia

1. **Supabase:** habilitar copias automáticas y *Point-in-Time Recovery* según el plan contratado.
2. **Copia externa diaria:** `pg_dump` en formato custom, cifrada y almacenada fuera del proveedor.
3. **Copia mensual:** retención de 12 meses para recuperación ante corrupción lógica.
4. **Registro:** cada respaldo se registra con fecha, tamaño, hash SHA-256, responsable y ubicación.
5. **Prueba:** restauración mensual en una base de datos de prueba y verificación de integridad.

> El contenedor de Render tiene sistema de archivos efímero. Los archivos adjuntos guardados en
> `App_Data/uploads` **no sobreviven a un redespliegue**. Para producción se recomienda migrar a
> almacenamiento de objetos (Supabase Storage, S3 o equivalente) y respaldarlo por separado.

### 1.1 Alcance del respaldo

- **PostgreSQL:** todas las tablas, incluida `audit_logs` y la evidencia de consentimiento
  (`usuarios.data_policy_evidence`). El dump contiene datos personales y hashes de contraseñas.
- **Adjuntos:** hoy en `App_Data/uploads` (efímero). Cuando se habilite Supabase Storage, respaldar
  el bucket `lms-uploads` por separado, ya que `pg_dump` no incluye objetos.
- **Secretos:** no se incluyen en el dump; se conservan en el gestor de secretos de Render/Supabase.
- **Configuración:** documentar las variables de entorno necesarias para reconstruir el servicio.

---

## 2. Respaldo manual recomendado (CLI)

Requisitos: `pg_dump` 17+ (la instancia es PostgreSQL 17.6), variables de entorno y directorio
cifrado. Ejecutar desde una máquina autorizada, nunca desde el contenedor efímero de Render.

```powershell
# 1. Configurar la conexión (nunca en el repositorio)
$env:PGPASSWORD = "<password>"
$conn = "postgresql://postgres.<ref>:<password>@aws-0-us-east-1.pooler.supabase.com:5432/postgres"

# 2. Generar el respaldo en formato custom
$fecha = Get-Date -Format "yyyyMMdd_HHmmss"
pg_dump --format=custom --no-owner --no-privileges --verbose `
  --file="backup_lms_$fecha.dump" $conn

# 3. Calcular el hash de integridad
Get-FileHash "backup_lms_$fecha.dump" -Algorithm SHA256 |
  Select-Object Hash, Path | Format-List

# 4. Cifrar con AES-256 (GPG) antes de almacenar
gpg --symmetric --cipher-algo AES256 --output "backup_lms_$fecha.dump.gpg" "backup_lms_$fecha.dump"

# 5. Eliminar el archivo en claro
Remove-Item "backup_lms_$fecha.dump"
```

Alternativa sin GPG: comprimir con 7-Zip y cifrar con AES-256:

```powershell
7z a -t7z -mhe=on -p"<passphrase>" "backup_lms_$fecha.7z" "backup_lms_$fecha.dump"
```

> La contraseña de cifrado debe guardarse en un gestor de secretos, **nunca** junto al respaldo.

---

## 3. Restauración

```powershell
# 1. Descifrar en un equipo autorizado
gpg --output backup_lms.dump --decrypt backup_lms_20261019_020000.dump.gpg

# 2. Verificar el contenido antes de restaurar
pg_restore --list backup_lms.dump | Select-Object -First 40

# 3. Restaurar en una base de prueba primero
$env:PGPASSWORD = "<password>"
pg_restore --clean --if-exists --no-owner --no-privileges `
  --dbname="postgresql://usuario:password@host:5432/postgres" backup_lms.dump

# 4. Verificar integridad
psql $env:TEST_CONN -c "SELECT COUNT(*) FROM usuarios;"
psql $env:TEST_CONN -c "SELECT COUNT(*) FROM entregas;"
psql $env:TEST_CONN -c "SELECT COUNT(*) FROM audit_logs;"
```

Pasos posteriores:

1. Ejecutar `/api/health` y `/api/ping`.
2. Iniciar sesión como administrador y revisar `/api/admin/compliance`.
3. Verificar la carga de un archivo y la descarga autorizada.
4. Documentar la restauración (fecha, responsable, duración, incidencias).

**Nunca** restaure directamente sobre producción sin un respaldo previo y una ventana de mantenimiento.

---

## 4. Endpoint de respaldo de la aplicación

`GET /api/admin/backup-database` genera un `.sql` con todas las tablas. Debe usarse solo como
respaldo de emergencia porque:

- se descarga en texto claro (sin cifrado);
- contiene datos personales y hashes de contraseñas;
- no reemplaza `pg_dump` para recuperación completa (roles, extensiones, secuencias).

El endpoint registra la descarga en auditoría y devuelve `Cache-Control: no-store`.
Almacene el archivo de forma cifrada y elimínelo al cumplir la retención.

La restauración desde la interfaz está **deshabilitada por defecto en el backend**
(`Features:EnableDatabaseRestore=false`) porque ejecutar SQL arbitrario desde una carga HTTP permite
borrar datos o ejecutar comandos del sistema operativo a través de PostgreSQL. El botón del panel
exige escribir `RESTAURAR` y solo acepta archivos `.sql` de hasta 5 MB; además, se controla con
`databaseRestoreEnabled` en `environment.prod.ts`.

Para habilitarla en producción, configure en Render `Features__EnableDatabaseRestore=true` y
`Features__AllowRestoreInProduction=true`. Hágalo solo con una copia de seguridad verificada, una
ventana de mantenimiento y aceptación formal del riesgo. Para recuperaciones reales se recomienda
`pg_dump`/`pg_restore` con un rol de mínimo privilegio en lugar del endpoint HTTP.

---

## 5. Control de acceso y auditoría

- Solo el administrador puede descargar respaldos.
- El DPO recibe copia cifrada de los respaldos mensuales.
- El acceso al almacenamiento se registra y se revisa trimestralmente.
- Los respaldos se destruyen de forma segura al vencer la retención.

---

## 6. Lista de verificación mensual

- [ ] Respaldo diario completado y con hash válido. **(Pendiente: no existe programación.)**
- [ ] Copia mensual cifrada almacenada fuera del proveedor. **(Pendiente.)**
- [ ] Restauración de prueba exitosa en entorno aislado. **(Pendiente.)**
- [ ] Conteos de tablas coherentes con la aplicación (`usuarios`, `entregas`, `audit_logs`).
- [ ] Adjuntos del bucket `lms-uploads` respaldados por separado. **(Pendiente: aún no se usa el bucket.)**
- [ ] Credenciales de cifrado vigentes.
- [ ] Registro de la prueba archivado.
