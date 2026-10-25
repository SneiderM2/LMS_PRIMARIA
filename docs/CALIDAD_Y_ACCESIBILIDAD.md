# Calidad del producto y accesibilidad

**Marcos:** ISO/IEC 25010 (calidad del producto), NTC 5854:2012 (accesibilidad web, WCAG 2.0 AA),
NTC-ISO 9001:2015 (gestión de la calidad) y buenas prácticas de ingeniería de software.

**Última verificación:** 2026-10-25, sobre `main` = `fix/audit-2026-10` = `11938e0f`.
CI y CodeQL en `success`; frontend de Pages desplegado desde `d2dad0cf`.

---

## 1. ISO/IEC 25010 — evaluación inicial

| Característica | Subcaracterística | Situación actual | Meta |
| :--- | :--- | :--- | :--- |
| Adecuación funcional | Completitud, corrección | Matrícula, cursos, tareas, entregas, notas y reportes implementados | Cobertura funcional ≥ 95 % |
| Eficiencia de desempeño | Tiempo de respuesta, uso de recursos | Warmup, pool de conexiones, proyecciones EF; sin medición formal de p95 | p95 API < 800 ms |
| Compatibilidad | Interoperabilidad | API REST/JSON, PostgreSQL, PWA | Contratos versionados |
| Usabilidad | Aprendizaje, accesibilidad | Interfaz infantil, 5 idiomas, modo oscuro; accesibilidad parcial | NTC 5854 AA |
| Fiabilidad | Disponibilidad, tolerancia a fallos | Reintentos EF, health checks, manejo global de errores; hosting gratuito con *cold start* | Disponibilidad ≥ 99 % |
| Seguridad | Confidencialidad, integridad | JWT, RBAC, TLS, rate limiting, auditoría, archivos privados | Ver `SECURITY.md` |
| Mantenibilidad | Modularidad, analizabilidad | Arquitectura por capas, DTOs, servicios, 55 pruebas backend y 6 frontend; faltan integración/E2E y medición formal de cobertura | Cobertura ≥ 60 % en servicios críticos |
| Portabilidad | Adaptabilidad | Docker, configuración por entorno, Angular multiplataforma | Despliegue reproducible |

### Métricas de referencia

| Métrica | Valor actual (25/10/2026) | Cómo obtenerla |
| :--- | :--- | :--- |
| Compilación backend | Correcta, 0 errores / 0 advertencias | `dotnet build -c Release` |
| Compilación frontend | Correcta; build de producción con CSP | `npm run build` |
| Bundle inicial | **152,4 KB**: `main` 102,5 KB + `polyfills` 33,7 KB + `styles` 16,2 KB; `dist` total ~1,98 MB | `ng build --configuration production` |
| Pruebas automatizadas | **55/55** backend (xUnit) + **6/6** frontend (Karma/Jasmine, ChromeHeadless) | `dotnet test ...` y `npm test` |
| CI/CD | CI y CodeQL en verde; Deploy Pages en verde | GitHub Actions |
| Vulnerabilidades conocidas | Angular 18: 9 (4 moderadas, 5 altas, 0 críticas) en producción; NuGet sin paquetes vulnerables | `dotnet list package --vulnerable`, `npm audit --omit=dev --audit-level=critical` |
| Producción | `/api/ping`, `/api/health` y `/api/health/live` 200; Swagger 404; `database: Healthy`, `storage: Local` | Verificación HTTP del 25/10/2026 |
| Base de datos | PostgreSQL 17.6; `audit_logs` activa (125 registros, RLS, 3 índices) | Consulta de solo lectura a Supabase |
| Accesibilidad | Parcial | Auditoría NTC 5854 |

---

## 2. Prácticas de calidad implementadas

- Separación de responsabilidades: controladores, servicios, DTOs y contexto EF.
- Validación de entrada con DataAnnotations y mensajes en español.
- Manejo global de excepciones con `TraceId` y respuestas uniformes.
- Logs estructurados y auditoría de eventos críticos.
- Configuración por entorno y secretos fuera del código.
- Pipeline de CI con compilación y auditoría de dependencias.
- Contratos de API documentados con Swagger (solo desarrollo).
- Revisión de cambios mediante *pull request* y control de versiones.
- Documentación de operación, riesgos e incidentes.

---

## 3. NTC 5854 / WCAG 2.0 AA — lista de verificación

| Criterio | Estado | Observación / acción |
| :--- | :---: | :--- |
| Idioma de la página (`lang`) | ✅ | `index.html` usa `lang="es"`. |
| Título de página descriptivo | ✅ | Título institucional. |
| Encabezados jerárquicos | ⚠️ | Revisar `h1` único por vista y orden de `h2/h3`. |
| Etiquetas de formulario | ✅ | `label for` en login, registro y formularios administrativos. |
| Identificación de errores | ⚠️ | Mensajes visibles; falta asociación `aria-describedby` en varios campos. |
| Contraste de color | ⚠️ | Verificar textos secundarios y modo oscuro con herramienta de contraste. |
| Navegación por teclado | ✅ | Controles nativos; el enlace de salto lleva el foco a `main` con `tabindex="-1"`. |
| Foco visible | ✅ | Estilos `:focus-visible` en botones, enlaces, inputs y elementos con `tabindex`. |
| Trampa de foco en modales | ⚠️ | El diálogo de confirmación atrapa el foco y cierra con Escape; revisar los demás modales. |
| Cerrar modal con Escape | ⚠️ | Implementado en confirmación, selector de idioma y diálogos; el modal de política es obligatorio. |
| Texto alternativo en imágenes | ⚠️ | Avatares decorativos deben llevar `alt=""`; iconos emoji `aria-hidden="true"`. |
| Mensajes dinámicos anunciados | ✅ | Toasts y medidor de contraseña usan `aria-live`; revisar errores de formulario. |
| Enlace para saltar al contenido | ✅ | «Saltar al contenido principal» con destino `#main-content`. |
| Redimensionamiento y zoom | ✅ | Diseño responsive; probar al 200 %. |
| Movimiento y animaciones | ✅ | Regla global `prefers-reduced-motion: reduce` en `styles.css`. |
| Tablas con encabezados | ⚠️ | Usar `scope="col"` y `<caption>` en tablas de reportes. |
| Documentos PDF accesibles | ❌ | Los reportes generados con jsPDF no son etiquetados; ofrecer alternativa HTML/CSV. |

Leyenda: ✅ cumple · ⚠️ parcial · ❌ pendiente.

---

## 4. Plan de remediación de accesibilidad

| # | Acción | Prioridad | Esfuerzo | Estado |
| :---: | :--- | :---: | :---: | :--- |
| 1 | Enlace «Saltar al contenido» y `main` con `id`. | Alta | Bajo | Implementado |
| 2 | Foco atrapado y cierre con Escape en los modales restantes. | Alta | Medio | Parcial: confirmación e idioma; revisar el modal de política |
| 3 | Asociar errores de formulario con `aria-describedby`. | Alta | Bajo | Pendiente |
| 4 | Revisar contraste en modo claro y oscuro. | Alta | Medio | Pendiente |
| 5 | Añadir `aria-hidden` a emojis decorativos y `alt` a avatares. | Media | Bajo | Parcial |
| 6 | Etiquetar tablas (`scope`, `<caption>`) y ofrecer reportes HTML/CSV. | Media | Medio | Pendiente |
| 7 | Respetar `prefers-reduced-motion`. | Media | Bajo | Implementado |
| 8 | Auditoría con lector de pantalla (NVDA) y teclado. | Alta | Medio | Pendiente |
| 9 | Pruebas automatizadas con axe-core. | Media | Medio | Pendiente |
| 10 | Reportes PDF etiquetados o alternativa accesible. | Media | Medio | Pendiente |

---

## 5. Estrategia de pruebas

### Actual

- **55/55** pruebas unitarias backend (`PasswordPolicy`, `FileSignatureValidator`, `ConsentService`, `TokenService`, CAPTCHA, cola de auditoría).
- **6/6** pruebas unitarias frontend de la política de contraseñas (Karma/Jasmine, ChromeHeadless).
- Revisión visual manual del login y registro en escritorio (1280 px) y móvil (390 px), tema claro y oscuro: banner de cold start sin solapamiento, banderas SVG locales, estado de warmup con reintentos y formulario de registro apilado en pantallas pequeñas.
- CI y CodeQL en verde para `11938e0f`; Deploy Angular to GitHub Pages en verde para `d2dad0cf`.
- Prueba de humo de seguridad (`scripts/security_smoke_test.ps1`) y verificación HTTP de producción: salud, Swagger, CORS y cabeceras.
- Pruebas manuales de los flujos de login, matrícula, tarea, calificación y descarga.
- `git diff --check` limpio y JSON del proyecto válidos.

### Recomendada

| Nivel | Herramienta | Alcance |
| :--- | :--- | :--- |
| Unitarias | xUnit + FluentAssertions | `PasswordPolicy`, `ConsentService`, `FileSignatureValidator`, `TokenService`, CAPTCHA y cola de auditoría (implementadas); falta `SemaforoService` |
| Integración | `WebApplicationFactory` + Testcontainers/PostgreSQL | Auth, privacidad, archivos, autorización |
| E2E | Playwright | Login, registro con acudiente, entrega, calificación, descarga |
| Seguridad | OWASP ZAP baseline | Cabeceras, CORS, autenticación, inyección |
| Accesibilidad | axe-core / Lighthouse | WCAG 2.0 AA |
| Carga | k6 | Login y dashboard con concurrencia esperada |

### Criterios de aceptación

- 0 errores de compilación y 0 advertencias nuevas.
- 0 vulnerabilidades críticas o altas sin mitigación documentada.
- Cobertura ≥ 60 % en servicios críticos.
- Flujos críticos E2E en verde.
- Sin hallazgos críticos de accesibilidad.

---

## 6. Gestión de la calidad (ISO 9001)

- **Planificar:** requisitos, criterios de aceptación, riesgos y cronograma.
- **Hacer:** desarrollo con revisión de pares y control de versiones.
- **Verificar:** CI, pruebas, auditoría de accesibilidad y revisión de seguridad.
- **Actuar:** acciones correctivas, actualización de documentación y lecciones aprendidas.

Indicadores sugeridos: defectos por release, tiempo medio de corrección, cobertura de pruebas,
incidentes de seguridad, satisfacción de usuarios y disponibilidad.

---

## 7. Comandos útiles

```powershell
# Backend
dotnet build backend\LMS.API\LMS.API.csproj -c Release
dotnet test backend\LMS.API.Tests\LMS.API.Tests.csproj -c Release
dotnet list backend\LMS.API\LMS.API.csproj package --vulnerable --include-transitive

# Frontend
cd frontend
npm ci
npm run build
npm test
npm audit --omit=dev

# Prueba de humo de seguridad (backend en ejecución)
powershell -ExecutionPolicy Bypass -File scripts\security_smoke_test.ps1
```

---

## 8. Evidencias de la verificación (25/10/2026)

| Evidencia | Resultado |
| :--- | :--- |
| Commit de referencia | `11938e0f` en `main` y `fix/audit-2026-10` |
| CI / CodeQL | `success` para `11938e0f` |
| Deploy Angular to GitHub Pages | `success` para `d2dad0cf` |
| Backend Release | 0 errores, 0 advertencias, 55/55 pruebas |
| Frontend | `npm ci`, build de producción y 6/6 pruebas Karma |
| Bundle inicial | 152,4 KB (`main` 102,5 KB, `polyfills` 33,7 KB, `styles` 16,2 KB) |
| API de producción | `/api/ping` 200, `/api/health` 200, `/api/health/live` 200, Swagger 404 |
| Supabase | PostgreSQL 17.6; 18 usuarios; 10 con consentimiento; `audit_logs` con 125 registros |
| Dependencias | NuGet sin vulnerabilidades; `npm audit` crítico con código 0 |
