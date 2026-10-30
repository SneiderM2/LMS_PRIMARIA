# Matriz de cumplimiento normativo — Colombia

Plataforma: **LMS Primaria** (backend .NET 8, frontend Angular 18, PostgreSQL 17.6/Supabase).
Este documento traza cada obligación relevante a un control técnico u organizativo concreto.

**Última verificación:** 2026-10-30. CI y CodeQL en verde; backend
Release con 125/125 pruebas unitarias y de seguridad superadas (LMS.API.Tests); frontend con pruebas Karma aprobadas; API y auditoría verificadas en producción.

> **Nota:** esta matriz es una herramienta de trabajo, no un concepto jurídico. El responsable
> del tratamiento debe validarla con su asesor legal y su Oficial de Protección de Datos.

---

## 1. Protección de datos personales

| Norma | Obligación | Implementación en el proyecto | Evidencia / brecha |
| :--- | :--- | :--- | :--- |
| **Ley 1581 de 2012, art. 4** | Principios de finalidad, libertad, veracidad, transparencia, acceso y circulación restringida, seguridad y confidencialidad. | Política publicada en `frontend/public/privacidad.html`; finalidades declaradas en `/api/privacy/policy`; minimización en exportaciones y logs. | Documento + endpoint público. |
| **Ley 1581, art. 7** | Tratamiento de datos de menores con autorización del representante legal. | Registro y acceso de estudiantes exige autorización expresa del acudiente; bloqueo preventivo con `MinorConsentActionFilter` y `minorConsentGuard`; evidencia probatoria en `ConsentimientoHabeasData`. | `RegisterRequestDto`, `ConsentService`, `ConsentimientoHabeasData`, modal de política y guard Angular. |
| **Ley 1581, art. 8** | Derechos de conocer, actualizar, rectificar, suprimir, revocar y presentar quejas. | Gestión de solicitudes ARCO (`/api/privacy/arco`, `SolicitudArco`) con cálculo legal perentorio (10/15 días hábiles con Ley Emiliani); `/api/privacy/my-data` (acceso/portabilidad), `PUT /api/privacy/my-data` (rectificación), `/api/privacy/delete-account` (supresión/anonimización). | `PrivacyController`, `SolicitudArco`. |
| **Ley 1581, art. 9** | Autorización previa, expresa e informada. | Checkbox obligatorio con finalidades esenciales y opcionales separadas + versión de política + evidencia (fecha, IP, agente, HMAC, hash SHA-256 de política). | `ConsentimientoHabeasData`, `DataPolicyEvidence`. |
| **Ley 1581, art. 14** | Prueba de la autorización. | Evidencia probatoria almacenada en tabla `consentimientos_habeas_data` y firmada con HMAC; exportable al titular. | `ConsentService`, `ConsentimientoHabeasData`. |
| **Ley 1581, art. 17** | Deberes del responsable: seguridad, confidencialidad, reporte de incidentes. | TLS, JWT, RBAC, bloqueo, rate limiting, archivos privados, auditoría, plan de incidentes. | `SECURITY.md`, `docs/PLAN_RESPUESTA_INCIDENTES.md`. |
| **Decreto 1074 de 2015** (compilatorio del Decreto 1377 de 2013) | Política de tratamiento, aviso de privacidad, procedimiento de consultas y reclamos. | Política y aviso en `privacidad.html`; plazos de 10/15 días hábiles documentados; endpoints y correo. | Documento + `privacy.html`. |
| **Decreto 886 de 2014 / RNBD** | Registro Nacional de Bases de Datos ante la SIC cuando aplique. | Inventario de bases y datos en la política. | **Pendiente de evaluar** si la institución supera el umbral de UVT o es entidad pública. |
| **Ley 1266 de 2008** | Habeas data financiero. | No se tratan datos crediticios. | No aplica; verificar si se incorporan pagos. |
| **Ley 1098 de 2006** | Interés superior del menor y protección integral. | Autorización del acudiente, minimización, acceso restringido a archivos y calificaciones. | `ConsentService`, `FilesController`. |
| **Ley 1620 de 2013** | Convivencia escolar y protección de la intimidad. | No se publican datos de estudiantes; acceso por roles y pertenencia de curso. | Revisar protocolos institucionales. |
| **Ley 1712 de 2014** | Transparencia y acceso a la información pública (si aplica). | Publicación de política y datos del responsable. | Completar datos institucionales. |

---

## 2. Seguridad digital y delitos informáticos

| Norma / estándar | Obligación o control | Implementación | Estado |
| :--- | :--- | :--- | :--- |
| **Ley 1273 de 2009** | Tipificación de delitos informáticos; deber de protección. | Controles de acceso, auditoría, integridad de archivos (SHA-256), bloqueo por fuerza bruta. | Implementado. |
| **Ley 527 de 1999** | Validez de mensajes de datos y firmas electrónicas. | Evidencia de consentimiento firmada con HMAC; trazabilidad de auditoría. | Implementado (firma técnica, no certificada). |
| **Decreto 1078 de 2015 / MSPI (MinTIC)** | Modelo de Seguridad y Privacidad de la Información: diagnóstico, plan, implementación, monitoreo. | `docs/MODELO_SEGURIDAD.md`, matriz de riesgos, plan de incidentes, revisión por roles. | Documentado; faltan revisiones periódicas formales. |
| **CONPES 3854 de 2016** | Política Nacional de Seguridad Digital. | Gestión de riesgos, controles de acceso, respuesta a incidentes. | Alineado. |
| **CONPES 3995 de 2020** | Confianza y seguridad digital. | TLS, HSTS, validación de archivos, autenticación fuerte. | Alineado. |
| **Resolución MinTIC 1519 de 2020** | Estándares de publicación web y accesibilidad. | `lang="es"`, navegación por teclado, etiquetas, contraste, modal accesible. | Parcial; ver `docs/CALIDAD_Y_ACCESIBILIDAD.md`. |
| **ISO/IEC 27001:2022** | Sistema de gestión de seguridad de la información. | Controles técnicos y organizativos mapeados en `docs/MODELO_SEGURIDAD.md`. | Implementación parcial, sin certificación. |
| **ISO/IEC 27002:2022** | Buenas prácticas de controles. | A.5 políticas, A.8 activos, A.9 acceso, A.12 operaciones, A.13 comunicaciones. | Parcial. |
| **NIST SP 800-63B** | Autenticación y gestión de credenciales. | Longitud de **8 a 64** caracteres, bloqueo temporal, lista de contraseñas comunes y BCrypt (factor 12). La composición obligatoria es una decisión institucional adicional. | Implementado. |
| **OWASP ASVS 4.0.3** | Verificación de seguridad de aplicaciones. | Ver mapeo en `docs/MODELO_SEGURIDAD.md`. | Nivel 1 mayormente cubierto. |
| **OWASP Top 10 2021** | Riesgos web prevalentes. | A01 acceso, A02 criptografía, A03 inyección, A04 diseño, A05 configuración, A07 identificación, A09 registro. | Mitigado; ver riesgos residuales. |

---

## 3. Calidad y accesibilidad

| Norma | Requisito | Implementación | Estado |
| :--- | :--- | :--- | :--- |
| **ISO/IEC 25010** | Calidad del producto: adecuación funcional, eficiencia, compatibilidad, usabilidad, fiabilidad, seguridad, mantenibilidad, portabilidad. | Arquitectura por capas, DTOs, validación, manejo global de errores, CI, documentación. | Medición inicial en `docs/CALIDAD_Y_ACCESIBILIDAD.md`. |
| **NTC 5854:2012** | Accesibilidad web (WCAG 2.0 nivel AA). | Etiquetas de formulario, `aria-*` en modales, foco visible, contraste, textos alternativos. | Parcial; plan de remediación. |
| **NTC-ISO 9001:2015** | Gestión de la calidad y mejora continua. | Procedimientos documentados, control de cambios, revisión por indicadores. | Recomendado para la institución. |
| **Ley 594 de 2000** | Gestión de archivos y retención documental. | Retención académica configurable; anonimización con conservación de historia. | Implementado a nivel de aplicación; falta tabla de retención institucional. |

---

## 4. Brechas y plan de acción

**Cerradas o verificadas el 30/10/2026:** despliegue del backend endurecido y del frontend con CSP;
`audit_logs` activa en Supabase (RLS y 3 índices); 13 tablas relacionales normalizadas; CI/CodeQL en verde; 125/125 pruebas
backend superadas y pruebas frontend aprobadas. Las brechas que siguen abiertas se listan a continuación.

| # | Brecha | Impacto | Acción | Responsable | Plazo |
| :---: | :--- | :--- | :--- | :--- | :--- |
| 1 | Secretos antiguos en historial de Git | Crítico | Rotar contraseña de BD, JWT y reCAPTCHA; purgar historial si se autoriza. | Líder técnico | Inmediato |
| 2 | Cuentas demo con contraseñas iniciales | Alto | Cambiar o desactivar `admin`, `docente`, `estudiante`. | Administrador | Inmediato |
| 3 | Datos del responsable «Por definir» | Alto | Completar NIT, razón social, dirección, teléfono, correo, DPO y encargados en `appsettings` y `privacidad.html`. La página pública aún los muestra como «Por definir». | Rectoría / Jurídica | 5 días |
| 4 | Evaluación de RNBD | Medio | Determinar si aplica y registrar la base ante la SIC. | Jurídica | 30 días |
| 5 | Respaldos y cifrado | Alto | No hay backups automáticos en Supabase Free ni respaldo lógico programado. Implementar `pg_dump` cifrado, retención y prueba de restauración. | TI | 15 días |
| 6 | Cabeceras del frontend (CSP/HSTS) | Medio | CSP por `<meta>` ya publicada en el build de producción. `frame-ancestors` y HSTS requieren un hosting con cabeceras HTTP; evaluar proxy o migración. | TI | 30 días |
| 7 | Pruebas de seguridad automatizadas | Medio | CI y CodeQL en verde; 55/55 pruebas backend y 6/6 frontend. Faltan integración/E2E y OWASP ZAP. | Desarrollo | 15 días |
| 8 | Migraciones EF Core | Bajo | Reemplazar `DbInitializer` por migraciones versionadas. | Desarrollo | 60 días |
| 9 | Accesibilidad AA completa | Medio | Auditoría NTC 5854 y corrección de hallazgos. | Desarrollo | 45 días |
| 10 | Política de retención aprobada | Medio | Aprobar tabla de retención y aplicación automatizada. | Jurídica / TI | 30 días |

---

## 5. Evidencias para auditoría

- Código fuente con controles: `backend/LMS.API/Controllers`, `Middleware`, `Services`.
- Política y aviso: `frontend/public/privacidad.html`.
- Configuración: `backend/LMS.API/appsettings.json`, `SETUP_SECRETS.md`.
- Seguridad: `SECURITY.md`, `docs/MODELO_SEGURIDAD.md`.
- Riesgos: `docs/MATRIZ_RIESGOS.md`.
- Incidentes: `docs/PLAN_RESPUESTA_INCIDENTES.md`.
- Respaldos: `docs/OPERACION_BACKUP.md`.
- Calidad: `docs/CALIDAD_Y_ACCESIBILIDAD.md`.
- Prueba de humo: `scripts/security_smoke_test.ps1`.
- CI y CodeQL en verde para `11938e0f`; Deploy Pages en verde para `d2dad0cf`.
- Producción: `/api/ping`, `/api/health` y `/api/health/live` 200; Swagger 404; CORS y cabeceras
  verificados el 25/10/2026.
- Auditoría: `audit_logs` en Supabase con 125 registros, RLS habilitado, 0 políticas y 3 índices.
- Base de datos: PostgreSQL 17.6; 18 usuarios y 10 con consentimiento vigente (55,6 %).
- Dependencias: NuGet sin vulnerabilidades; `npm audit --omit=dev --audit-level=critical` con
  código 0 y 9 vulnerabilidades de Angular 18 sin críticas.
