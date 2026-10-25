# Modelo de Seguridad y Privacidad de la Información (MSPI)

**Marco de referencia:** Decreto 1078 de 2015 (sector TIC), MSPI de MinTIC, ISO/IEC 27001:2022,
ISO/IEC 27002:2022, NIST SP 800-63B, OWASP ASVS 4.0.3 y OWASP Top 10 2021.

---

## 1. Alcance y activos

| Activo | Descripción | Criticidad |
| :--- | :--- | :---: |
| Base de datos PostgreSQL (Supabase) | Usuarios, matrículas, tareas, entregas, calificaciones | Crítica |
| API .NET | Lógica de negocio, autenticación, autorización y archivos | Crítica |
| Frontend Angular | Interfaz y token de sesión del usuario | Alta |
| Archivos adjuntos | Tareas y recursos educativos | Alta |
| Logs de auditoría | Trazabilidad de accesos y operaciones | Alta |
| Secretos (JWT, BD, reCAPTCHA) | Claves de acceso | Crítica |
| Respaldos | Copia de la base de datos | Crítica |

**Datos personales:** identificadores escolares, nombres, información académica, archivos y
autorizaciones de acudientes. **Titulares:** menores de edad y personal educativo.

---

## 2. Arquitectura y flujo de datos

```
Navegador (GitHub Pages, Angular)
        │  HTTPS + JWT (Authorization: Bearer)
        ▼
Render (ASP.NET Core 8, contenedor no-root)
        │  TLS + Npgsql
        ▼
Supabase PostgreSQL (pooler, SSL obligatorio)
```

- El frontend no almacena datos de negocio; conserva el token y el perfil mínimo.
- Los archivos se guardan en almacenamiento privado (`App_Data/uploads`) y se sirven por
  `/api/files/...` con autorización.
- Los logs se escriben en el sistema de archivos del contenedor y se consultan por API de
  administración.
- No hay integraciones de pago ni analítica de terceros.

---

## 3. Roles y segregación de funciones

| Rol | Permisos | Restricciones |
| :--- | :--- | :--- |
| Administrador | Usuarios, cursos, métricas, auditoría, respaldos | No puede suspenderse a sí mismo; no puede eliminar su cuenta desde el portal |
| Docente | Sus cursos, matrícula, tareas, calificaciones, recursos | Solo cursos asignados; no ve cursos ajenos ni descarga entregas ajenas |
| Estudiante | Sus cursos, tareas, entregas, notas | Solo su grado/cursos; no accede a archivos de otros estudiantes |
| Acudiente | Autorización y ejercicio de derechos | Acceso indirecto a través del estudiante o del DPO |

El registro público solo crea estudiantes. Los docentes requieren código de invitación. Los
administradores solo se crean desde el panel administrativo autenticado.

---

## 4. Controles técnicos

### 4.1 Gestión de identidades y accesos (ISO 27001 A.5/A.8)

- Roles canónicos y políticas de autorización.
- Contraseñas con BCrypt (coste 12), política de 8 a 64 caracteres y bloqueo de contraseñas comunes.
- Bloqueo temporal de 15 minutos tras 5 fallos y limitación de tasa por IP.
- JWT firmado con HMAC-SHA256, expiración de 8 horas, `jti`, `iat` y `nbf`.
- Revocación por sesión única (token de sesión en base de datos).
- Revisión periódica de cuentas y roles; cuentas demo desactivadas en producción.

### 4.2 Protección de datos y criptografía (A.8/A.10)

- TLS 1.2+ en tránsito; `SSL Mode=Require` y validación de certificado en PostgreSQL.
- Hash de contraseñas con BCrypt; no se almacenan contraseñas en claro.
- Evidencia de consentimiento firmada con HMAC-SHA256.
- Validación de firma binaria de archivos y cálculo de SHA-256.
- Secretos fuera del código y del repositorio; rotación documentada.

### 4.3 Seguridad de aplicaciones (A.8/A.14)

- Validación de entrada con DataAnnotations y límites de tamaño.
- Consultas parametrizadas mediante EF Core (sin SQL dinámico de usuario).
- Prevención de IDOR con validación de pertenencia.
- CORS con lista blanca; sin `AllowCredentials` innecesario.
- Cabeceras de seguridad y CSP restrictiva en la API.
- Rate limiting por política y global.
- Manejo global de excepciones sin filtrar detalles en producción.
- Swagger deshabilitado en producción.
- Validación de archivos: extensión + firma real + tamaño + nombre saneado.

### 4.4 Operaciones y auditoría (A.12)

- Registro de accesos, autenticación, cambios administrativos, descargas y eventos de privacidad.
- Redacción de tokens, contraseñas y parámetros sensibles en logs.
- Rotación de logs por tamaño y retención configurable (90 días por defecto).
- Prohibición de borrar la auditoría desde la aplicación en producción.
- Respaldos con advertencia de datos personales y procedimiento controlado.

### 4.5 Seguridad de la cadena de suministro (A.15)

- Dependencias fijadas por versión.
- Revisión de vulnerabilidades en CI (`dotnet list package --vulnerable`, `npm audit`).
- Dependabot para actualizaciones.
- Imagen Docker mínima y ejecución como usuario sin privilegios.

---

## 5. Mapeo OWASP ASVS 4.0.3 (nivel 1)

| Capítulo | Control | Estado |
| :--- | :--- | :---: |
| V1 Arquitectura | Separación de capas, DTOs, configuración por entorno | ✅ |
| V2 Autenticación | Longitud mínima, bloqueo, mensajes uniformes, sin credenciales por defecto | ✅ |
| V3 Sesión | Expiración, revocación, token firmado | ✅ |
| V4 Control de acceso | RBAC, pertenencia de recursos, denegación por defecto | ✅ |
| V5 Validación | DataAnnotations, límites, tipos | ✅ |
| V6 Criptografía | BCrypt, TLS, HMAC | ✅ |
| V7 Errores y logs | Manejo global, sin detalles en producción, redacción | ✅ |
| V8 Protección de datos | Minimización, retención, exportación y supresión | ✅ |
| V9 Comunicaciones | HTTPS/HSTS, CORS | ✅ |
| V10 Archivos | Firma binaria, almacenamiento privado, descarga autorizada | ✅ |
| V11 Lógica de negocio | Validación de notas, matrícula, estados | ✅ |
| V12 Archivos y recursos | Sin rutas públicas, sin path traversal | ✅ |
| V13 API | Swagger solo en desarrollo, rate limiting | ✅ |
| V14 Configuración | Secretos por entorno, sin valores por defecto | ✅ |

---

## 6. Ciclo MSPI

| Fase | Actividad | Evidencia |
| :--- | :--- | :--- |
| Diagnosticar | Inventario de activos, análisis de riesgos, brechas | `docs/MATRIZ_RIESGOS.md` |
| Planificar | Plan de tratamiento, responsables y plazos | Matriz de riesgos y plan de acción |
| Implementar | Controles técnicos y documentales | Código, políticas, CI |
| Monitorear | Logs, métricas, revisiones, pruebas | `/api/admin/compliance`, smoke test |
| Mejorar | Lecciones aprendidas, actualización de controles | Revisión anual y post-incidente |

---

## 7. Indicadores

| Indicador | Meta |
| :--- | :--- |
| Cobertura de consentimiento vigente | ≥ 95 % |
| Incidentes críticos sin tratamiento | 0 |
| Solicitudes ARCO atendidas en plazo | 100 % |
| Vulnerabilidades críticas abiertas | 0 |
| Cobertura de pruebas de humo de seguridad | 100 % de los controles del script |
| Disponibilidad mensual | ≥ 99 % |

---

## 8. Responsabilidades

- **Rectoría:** aprueba la política y asigna recursos.
- **DPO:** vigila el cumplimiento y atiende derechos.
- **TI:** opera los controles, respaldos y parches.
- **Docentes:** usan la plataforma conforme a la política y reportan incidentes.
- **Proveedores:** mantienen sus controles y notifican incidentes.

---

## 9. Revisión

Este modelo se revisa anualmente o cuando cambie la normativa, la arquitectura o se produzca un
incidente relevante. La versión aprobada se conserva con acta de comité.
