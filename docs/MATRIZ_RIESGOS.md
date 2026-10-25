# Matriz de riesgos de seguridad y privacidad

**Metodología:** probabilidad (1-5) × impacto (1-5). Nivel: bajo (1-6), medio (7-12), alto (13-19), crítico (20-25).
**Fecha de revisión:** 2026-10-25 · **Revisión:** anual o tras incidente.

> **Estado verificado el 25/10/2026:** backend y frontend desplegados; `audit_logs` activa en
> PostgreSQL 17.6 (125 registros, RLS y 3 índices); CI/CodeQL en verde; 55/55 pruebas backend y
> 6/6 frontend. Persisten `storage: Local`, la ausencia de respaldos automáticos y los datos
> institucionales incompletos.

| ID | Riesgo | Prob. | Impacto | Nivel | Tratamiento | Control implementado | Responsable | Estado |
| :---: | :--- | :---: | :---: | :---: | :--- | :--- | :--- | :--- |
| R-01 | Robo de credenciales de base de datos y acceso masivo a datos de menores | 4 | 5 | **Crítico** | Mitigar | TLS obligatorio, secretos fuera del repo, acceso restringido, auditoría; **rotación de la contraseña pendiente de confirmar** | TI / DPO | En tratamiento |
| R-02 | Fuga de la clave JWT y suplantación de administradores | 3 | 5 | **Crítico** | Mitigar | Clave de 48+ caracteres, expiración, sesión única, revocación; **rotación pendiente de confirmar** | TI | En tratamiento |
| R-03 | Ejecución remota de SQL mediante la restauración de respaldos | 2 | 5 | **Crítico** | Evitar | Endpoint deshabilitado por defecto, confirmación, lista negra, solo desarrollo | Desarrollo | Mitigado |
| R-04 | Acceso no autorizado a archivos de tareas de menores | 4 | 4 | **Alto** | Mitigar | Almacenamiento privado, descarga autenticada, autorización por propietario/docente, firma binaria | Desarrollo | Mitigado (acceso); persistencia pendiente en R-23 |
| R-05 | IDOR en calificaciones, matrículas o cursos de otros docentes | 4 | 3 | **Alto** | Mitigar | Validación de pertenencia de curso en cada endpoint | Desarrollo | Mitigado |
| R-06 | Auto-registro como administrador o docente | 3 | 5 | **Crítico** | Evitar | Registro público solo estudiantes; docentes con código; admin solo desde panel | Desarrollo | Mitigado |
| R-07 | Fuerza bruta o credential stuffing | 5 | 3 | **Alto** | Mitigar | Bloqueo 15 min, rate limit 30/min en autenticación, CAPTCHA, mensajes uniformes | Desarrollo | Mitigado |
| R-08 | Secuestro de sesión por XSS en el frontend | 3 | 4 | **Alto** | Mitigar | Angular escapa HTML, sin `innerHTML` con datos, CSP por `<meta>` en Pages y CSP de API, expiración; evaluar cookies HttpOnly | Desarrollo | Parcial |
| R-09 | Falta de consentimiento válido de menores | 3 | 5 | **Crítico** | Mitigar | Autorización del acudiente, evidencia firmada, versión de política, bloqueo hasta aceptar | DPO | Mitigado |
| R-10 | Retención excesiva de datos personales | 4 | 3 | **Alto** | Mitigar | Retención configurable, anonimización, supresión, rotación de logs | DPO / TI | En tratamiento |
| R-11 | Fuga de datos por logs con información sensible | 4 | 3 | **Alto** | Mitigar | Redacción de tokens/contraseñas, retención 90 días, acceso admin auditado | Desarrollo | Mitigado |
| R-12 | Borrado de evidencia de auditoría por un administrador | 2 | 4 | **Medio** | Mitigar | Purga deshabilitada en producción, solo logs operativos, registro del evento | Desarrollo | Mitigado |
| R-13 | Dependencia vulnerable o desactualizada | 4 | 3 | **Alto** | Mitigar | CI con auditoría y CodeQL, Dependabot. Verificado: NuGet sin vulnerabilidades; Angular 18 con 9 (4 moderadas, 5 altas, 0 críticas) que exigen actualización mayor | Desarrollo | En tratamiento |
| R-14 | Indisponibilidad del backend o de la base de datos | 3 | 3 | **Medio** | Mitigar | Reintentos, health checks, warmup, respaldos, plan de recuperación | TI | Parcial |
| R-15 | Pérdida de datos por error humano o ataque | 2 | 5 | **Alto** | Mitigar | Mínimo privilegio y procedimiento documentado; **no hay backups automáticos ni respaldo lógico programado** en Supabase Free. Implementar copia cifrada y prueba de restauración | TI | En tratamiento |
| R-16 | Suplantación mediante Google OAuth con token no validado | 2 | 5 | **Crítico** | Evitar | Validación criptográfica del ID token, audiencia, correo verificado, sin fallback inseguro | Desarrollo | Mitigado |
| R-17 | CAPTCHA deshabilitado o con claves de prueba en producción | 2 | 4 | **Medio** | Mitigar | Fail-closed, bypass solo en Development y con bandera, clave por entorno | Desarrollo | Mitigado |
| R-18 | CORS abierto con credenciales | 3 | 4 | **Alto** | Evitar | Lista blanca explícita, sin comodín, sin `AllowCredentials` | Desarrollo | Mitigado |
| R-19 | Restauración de respaldo con datos incompletos o corruptos | 3 | 4 | **Alto** | Mitigar | Verificación, entorno de pruebas, respaldo previo, procedimiento documentado | TI | Parcial |
| R-20 | Incumplimiento de accesibilidad (NTC 5854) | 4 | 2 | **Medio** | Mitigar | Etiquetas, foco, contraste, auditoría pendiente | Desarrollo | Parcial |
| R-21 | Falsificación de `X-Forwarded-For` para eludir límites por IP | 3 | 2 | **Medio** | Mitigar | Render sobrescribe el encabezado en su proxy; límites por IP complementados con bloqueo por cuenta | TI | Aceptado |
| R-22 | Política pública de privacidad desactualizada (dice «12 caracteres» y «Por definir») | 4 | 2 | **Medio** | Mitigar | Corregir `frontend/public/privacidad.html` a la política real 8–64 y completar los datos del responsable | DPO / TI | En tratamiento |
| R-23 | Pérdida de adjuntos por almacenamiento local efímero en Render | 4 | 3 | **Medio** | Mitigar | Bucket privado `lms-uploads` listo; falta configurar `Storage__Provider=Supabase` y la Service Role Key, y respaldar los objetos | TI | En tratamiento |

---

## Riesgos residuales aceptados

| Riesgo | Justificación | Mitigación compensatoria | Aprobación |
| :--- | :--- | :--- | :--- |
| Token en `localStorage` | GitHub Pages no permite cookies de otro dominio sin un proxy; el cambio requiere rediseño de autenticación. | Expiración corta, sesión única, sanitización de Angular y CSP de la API. | DPO / TI |
| Ausencia de cabeceras HTTP en GitHub Pages | Limitación del proveedor. | No se almacenan datos sensibles en el frontend; evaluar migración de hosting. | TI |
| Sin certificación ISO 27001 | Costo y alcance organizacional. | Controles alineados y documentados; auditoría interna anual. | Rectoría |

---

## Seguimiento

- Revisar esta matriz en cada comité de seguridad (mínimo semestral).
- Actualizar el nivel de riesgo después de implementar controles.
- Registrar incidentes y lecciones aprendidas en `docs/PLAN_RESPUESTA_INCIDENTES.md`.
