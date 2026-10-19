# Calidad del producto y accesibilidad

**Marcos:** ISO/IEC 25010 (calidad del producto), NTC 5854:2012 (accesibilidad web, WCAG 2.0 AA),
NTC-ISO 9001:2015 (gestión de la calidad) y buenas prácticas de ingeniería de software.

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
| Mantenibilidad | Modularidad, analizabilidad | Arquitectura por capas, DTOs, servicios; falta suite de pruebas automatizadas | Cobertura ≥ 60 % en servicios críticos |
| Portabilidad | Adaptabilidad | Docker, configuración por entorno, Angular multiplataforma | Despliegue reproducible |

### Métricas de referencia

| Métrica | Valor actual | Cómo obtenerla |
| :--- | :--- | :--- |
| Compilación backend | Correcta, 0 errores / 0 advertencias | `dotnet build -c Release` |
| Compilación frontend | Correcta | `npm run build` |
| Tamaño del bundle | Ver presupuesto en `angular.json` (2 MB advertencia / 4 MB error) | `ng build --stats-json` |
| Pruebas automatizadas | 26 pruebas unitarias (xUnit) en `backend/LMS.API.Tests` | `dotnet test backend\LMS.API.Tests\LMS.API.Tests.csproj -c Release` |
| Vulnerabilidades conocidas | Revisar | `dotnet list package --vulnerable`, `npm audit` |
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
| Navegación por teclado | ⚠️ | Controles nativos; falta probar todos los modales y menús. |
| Foco visible | ✅ | Estilos de foco en botones e inputs; revisar enlaces. |
| Trampa de foco en modales | ❌ | Implementar `focus trap` y retorno de foco al cerrar. |
| Cerrar modal con Escape | ❌ | Añadir manejador `keydown.escape`. |
| Texto alternativo en imágenes | ⚠️ | Avatares decorativos deben llevar `alt=""`; iconos emoji `aria-hidden="true"`. |
| Mensajes dinámicos anunciados | ❌ | Añadir `aria-live="polite"` a toasts y alertas. |
| Enlace para saltar al contenido | ❌ | Añadir «Saltar al contenido principal». |
| Redimensionamiento y zoom | ✅ | Diseño responsive; probar al 200 %. |
| Movimiento y animaciones | ⚠️ | Respetar `prefers-reduced-motion`. |
| Tablas con encabezados | ⚠️ | Usar `scope="col"` y `<caption>` en tablas de reportes. |
| Documentos PDF accesibles | ❌ | Los reportes generados con jsPDF no son etiquetados; ofrecer alternativa HTML/CSV. |

Leyenda: ✅ cumple · ⚠️ parcial · ❌ pendiente.

---

## 4. Plan de remediación de accesibilidad

| # | Acción | Prioridad | Esfuerzo |
| :---: | :--- | :---: | :---: |
| 1 | Añadir enlace «Saltar al contenido» y `main` con `id`. | Alta | Bajo |
| 2 | Implementar foco atrapado y cierre con Escape en modales. | Alta | Medio |
| 3 | Añadir `aria-live` a toasts y mensajes de error. | Alta | Bajo |
| 4 | Revisar contraste en modo claro y oscuro. | Alta | Medio |
| 5 | Añadir `aria-hidden` a emojis decorativos y `alt` a avatares. | Media | Bajo |
| 6 | Asociar errores a campos con `aria-describedby`. | Media | Medio |
| 7 | Respetar `prefers-reduced-motion`. | Media | Bajo |
| 8 | Etiquetar tablas y ofrecer reportes en HTML/CSV. | Media | Medio |
| 9 | Auditoría con lector de pantalla (NVDA) y teclado. | Alta | Medio |
| 10 | Pruebas automatizadas con axe-core. | Media | Medio |

---

## 5. Estrategia de pruebas

### Actual

- 26 pruebas unitarias de `PasswordPolicy`, `FileSignatureValidator`, `ConsentService` y `TokenService`.
- Compilación de backend y frontend en CI.
- Prueba de humo de seguridad (`scripts/security_smoke_test.ps1`).
- Pruebas manuales de los flujos de login, matrícula, tarea, calificación y descarga.

### Recomendada

| Nivel | Herramienta | Alcance |
| :--- | :--- | :--- |
| Unitarias | xUnit + FluentAssertions | `PasswordPolicy`, `ConsentService`, `FileSignatureValidator`, `SemaforoService` (implementadas las tres primeras) |
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
dotnet list backend\LMS.API\LMS.API.csproj package --vulnerable --include-transitive

# Frontend
cd frontend
npm ci
npm run build
npm audit --omit=dev

# Prueba de humo de seguridad (backend en ejecución)
powershell -ExecutionPolicy Bypass -File scripts\security_smoke_test.ps1
```
