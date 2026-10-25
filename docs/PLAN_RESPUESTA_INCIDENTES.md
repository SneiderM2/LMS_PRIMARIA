# Plan de respuesta a incidentes de seguridad y privacidad

**Alcance:** accesos no autorizados, pérdida o alteración de datos, fuga de información de menores,
compromiso de credenciales, indisponibilidad prolongada y errores de configuración.

**Estado de herramientas (25/10/2026):** `audit_logs` activa en PostgreSQL 17.6, `/api/health` y
`/api/health/live` operativos, CI/CodeQL en verde. **No hay respaldos automáticos ni copia externa
verificada**, por lo que la fase de recuperación depende de un respaldo manual que aún no se ha
programado.

---

## 1. Roles

| Rol | Responsabilidad |
| :--- | :--- |
| **Coordinador de incidentes** | Dirige la respuesta, toma decisiones y comunica. |
| **DPO** | Evalúa impacto en derechos de los titulares y notifica. |
| **TI / Desarrollo** | Contiene, corrige, recupera y documenta. |
| **Rectoría / Jurídica** | Autoriza comunicaciones y reportes a autoridades. |
| **Comunicaciones** | Mensajes a estudiantes, acudientes y personal. |

Suplentes deben estar definidos por escrito. Los contactos se mantienen actualizados en un
directorio fuera de la plataforma.

---

## 2. Clasificación

| Severidad | Criterio | Ejemplos | Tiempo de respuesta |
| :--- | :--- | :--- | :--- |
| **S1 Crítico** | Acceso o exposición de datos de menores, compromiso de administrador, RCE, pérdida masiva | Credenciales de BD filtradas, restore SQL explotado | 1 hora |
| **S2 Alto** | Acceso no autorizado limitado, denegación prolongada, integridad de notas | IDOR explotado, borrado de logs | 4 horas |
| **S3 Medio** | Vulnerabilidad sin explotación confirmada, error de configuración | CORS abierto, cabecera faltante | 1 día hábil |
| **S4 Bajo** | Hallazgo informativo o de baja exposición | Mensaje de versión, dependencia menor | 5 días hábiles |

---

## 3. Fases

### 3.1 Detección

Fuentes: paneles y alertas disponibles en Render/Supabase, revisión de `error.log` y
`app-audit.log`, consulta de `/api/admin/audit-logs` (tabla `audit_logs`), reportes de usuarios,
análisis de dependencias, resultados de CI/CodeQL y monitoreo de `/api/health`.

### 3.2 Contención

- Revocar sesiones (`session_token = NULL`) y rotar la clave JWT si hay sospecha.
- Deshabilitar cuentas comprometidas.
- Bloquear IPs o activar modo mantenimiento si es necesario.
- Aislar el contenedor o restaurar una versión anterior.
- Preservar logs y evidencias **antes** de reiniciar o borrar.

### 3.3 Erradicación y recuperación

- Corregir la causa raíz y desplegar.
- Restaurar datos desde el respaldo verificado (`docs/OPERACION_BACKUP.md`). **Brecha actual:** no
  existe respaldo automático ni copia externa programada; generar uno antes de una intervención
  destructiva y documentar su ubicación.
- Verificar integridad y funcionamiento (incluido `audit_logs`).
- Monitorear durante 72 horas.

### 3.4 Notificación

| Destinatario | Cuándo | Contenido |
| :--- | :--- | :--- |
| DPO y Rectoría | Inmediato (≤ 1 h en S1/S2) | Descripción, alcance, datos afectados, acciones |
| Titulares / acudientes | Si hay riesgo alto para sus derechos | Qué pasó, qué datos, qué hacer, contacto |
| SIC | Cuando la normativa lo exija y tras evaluación jurídica | Reporte formal |
| ICBF / autoridades | Si hay afectación a menores | Según orientación jurídica |
| Proveedores | Si el incidente es de su competencia | Notificación contractual |

> La Ley 1581 de 2012 y los lineamientos de la SIC exigen informar incidentes que afecten
> derechos de los titulares. La evaluación jurídica define el canal y el plazo exactos.

### 3.5 Comunicación

- Mensajes claros, sin tecnicismos, en español.
- No atribuir culpas ni minimizar.
- Incluir canal de contacto y medidas de protección.
- Coordinar una sola voz con comunicaciones.

### 3.6 Post-incidente

- Reunión en 5 días hábiles.
- Análisis de causa raíz (5 porqués o Ishikawa).
- Acciones correctivas con responsable y fecha.
- Actualizar matriz de riesgos, controles y este plan.
- Conservar evidencias por el término legal.

---

## 4. Evidencias a conservar

- Fecha y hora de detección, fuentes y personas.
- Logs relevantes (con datos personales minimizados).
- Capturas, consultas y resultados.
- Decisiones y autorizaciones.
- Comunicaciones enviadas.
- Acciones de contención y recuperación.
- Informe final y lecciones aprendidas.

---

## 5. Plantilla de notificación al titular

```
Asunto: Información importante sobre la seguridad de su cuenta — LMS Primaria

Estimado(a) [nombre]:

El [fecha] identificamos un incidente de seguridad que pudo afectar
[descripción breve de los datos]. Hemos contenido el evento y adoptado
las siguientes medidas: [medidas].

Le recomendamos: [cambio de contraseña, revisión de actividad, etc.].

Para consultas: privacidad@lmsprimaria.edu.co / [teléfono].

Atentamente,
[Responsable del tratamiento]
```

---

## 6. Pruebas y mejora

- Simulación anual de incidente (tabletop).
- Verificación trimestral de contactos y accesos.
- Revisión de tiempos de detección y respuesta.
- Actualización de runbooks y del plan.
