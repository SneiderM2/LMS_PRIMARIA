# Política y manual operativo de tratamiento de datos personales

**Versión:** 2026-10-19
**Marco:** Ley 1581 de 2012, Decreto 1074 de 2015, Ley 1098 de 2006, lineamientos de la SIC y MSPI (MinTIC).
**Publicación:** `frontend/public/privacidad.html` (versión ciudadana y aviso de privacidad).

> Este manual es la versión de gobierno interno. Debe ser aprobado por el representante legal de
> la institución y conservarse como evidencia ante la SIC, el ICBF o una auditoría.

---

## 1. Definiciones y roles

| Rol | Responsabilidad | Cargo sugerido |
| :--- | :--- | :--- |
| **Responsable del tratamiento** | Decide sobre la finalidad y los medios; atiende derechos y reclamos. | Rector / Representante legal |
| **Oficial de Protección de Datos (DPO)** | Vigila el cumplimiento, recibe solicitudes, reporta incidentes. | Secretaría / Calidad |
| **Administrador del sistema** | Gestiona cuentas, roles, respaldos y auditoría. | Coordinación de TI |
| **Docente** | Trata datos solo para fines académicos de sus cursos. | Docente titular |
| **Encargados** | Proveedores que tratan datos por cuenta de la institución. | Supabase, Render, GitHub, Google |
| **Titular / acudiente** | Autoriza y ejerce derechos. | Estudiante / padre o madre |

---

## 2. Inventario de actividades de tratamiento (RAT)

| # | Actividad | Categorías de datos | Titulares | Finalidad | Base legal | Destinatarios | Retención |
| :---: | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | Registro y autenticación | Documento/carnet, nombres, contraseña (hash), rol, correo (opcional) | Estudiantes, docentes, administrativos | Acceso seguro | Autorización / interés legítimo | Supabase, Render | Mientras exista la cuenta |
| 2 | Gestión académica | Grado, matrícula, cursos, tareas, entregas, calificaciones | Estudiantes | Prestación del servicio educativo | Autorización / obligación legal | Docentes, acudientes | 5 años tras el egreso |
| 3 | Archivos de tareas | Nombre, tipo, tamaño, contenido | Estudiantes | Evaluación | Autorización | Docentes del curso | Hasta supresión o 5 años |
| 4 | Autorización de menores | Nombre del acudiente, documento enmascarado, constancia | Acudientes | Cumplir art. 7 Ley 1581 | Obligación legal | Autoridades | Vigencia + 5 años |
| 5 | Seguridad y auditoría | IP, agente, fecha/hora, usuario, acción | Usuarios y visitantes | Seguridad y trazabilidad | Interés legítimo | Administradores | 90 días |
| 6 | Comunicaciones | Correo, mensajes institucionales | Usuarios y acudientes | Información académica | Autorización | Proveedor de correo | 1 año |
| 7 | Soporte y PQRS | Datos de contacto, descripción del caso | Usuarios | Atención de solicitudes | Obligación legal | DPO | 5 años |

**Datos sensibles:** no se tratan de forma intencional. Si se requiere información de salud o
discapacidad para un ajuste razonable, se solicitará autorización explícita y se limitará el acceso.

---

## 3. Principios aplicados

1. **Finalidad:** cada dato tiene una finalidad registrada en el RAT.
2. **Libertad:** el consentimiento es previo, expreso e informado; se puede revocar para finalidades no esenciales.
3. **Veracidad:** el titular puede rectificar sus datos desde `/api/privacy/my-data`.
4. **Transparencia:** política pública y aviso de privacidad accesibles.
5. **Acceso y circulación restringida:** RBAC, pertenencia de curso y descargas autenticadas.
6. **Seguridad:** controles técnicos descritos en `docs/MODELO_SEGURIDAD.md`.
7. **Confidencialidad:** deber de secreto del personal y de los encargados.

---

## 4. Consentimiento y evidencia

### 4.1 Registro

- El formulario exige aceptar la política y, para menores, la autorización del acudiente.
- El backend registra en `usuarios.data_policy_evidence` un JSON con:
  - versión de la política, fecha y hora UTC, método (`registration` o `login-modal`);
  - hash del usuario, IP y agente de usuario;
  - nombre del acudiente y documento enmascarado (si aplica);
  - firma `HMAC-SHA256` del conjunto.
- La versión vigente se controla con `Privacy:PolicyVersion`. Un cambio de versión obliga a
  aceptar de nuevo antes de operar.

### 4.2 Verificación

- El endpoint `/api/auth/me` informa `requiresPolicyAcceptance`.
- El modal de la aplicación bloquea la operación hasta registrar el consentimiento.
- Un administrador puede consultar la cobertura en `/api/admin/compliance`.

### 4.3 Conservación de la evidencia

La evidencia se conserva mientras exista la relación académica y durante el término de
prescripción de las obligaciones (mínimo 5 años). El acceso está restringido al DPO y al
administrador del sistema.

---

## 5. Procedimiento de consultas, reclamos y derechos (ARCO)

| Etapa | Responsable | Plazo | Registro |
| :--- | :--- | :--- | :--- |
| Recepción | DPO / secretaría | Día 0 | Correo o formulario con radicado |
| Verificación de identidad | DPO | 1 día hábil | Documento del titular o poder del acudiente |
| Consulta (acceso) | DPO + TI | 10 días hábiles | Respuesta escrita con datos y finalidades |
| Reclamo (rectificación, supresión, revocatoria) | DPO + TI | 15 días hábiles | Decisión motivada y constancia de ejecución |
| Escalamiento | Titular | — | Queja ante la SIC |

Canales:

- **En la aplicación:** «Descargar mis datos» y «Eliminar mi cuenta».
- **Correo:** `privacidad@lmsprimaria.edu.co`.
- **Presencial:** ventanilla única de la institución.
- **Autoridad:** Superintendencia de Industria y Comercio (SIC).

### 5.1 Supresión y anonimización

Al ejercer la supresión:

1. Se verifica la contraseña y la frase de confirmación.
2. Se eliminan los archivos personales y se borran sus metadatos.
3. Se anonimiza la cuenta: usuario, nombres, avatar y credenciales.
4. Se conserva la historia académica disociada (calificaciones y matrículas) por deber legal.
5. Se registra la operación en auditoría.

No procede la supresión cuando exista un deber legal o contractual de conservar el dato
(por ejemplo, archivos académicos exigidos por la Ley 594 de 2000 y normas educativas).

---

## 6. Retención documental

| Tipo de dato | Plazo | Acción al vencer |
| :--- | :--- | :--- |
| Historia académica | 5 años tras el egreso (o el plazo institucional mayor) | Conservar disociada / archivo histórico |
| Archivos de tareas | Hasta la supresión o 5 años | Eliminar archivo y metadatos |
| Evidencia de consentimiento | Vigencia + 5 años | Conservar evidencia mínima |
| Logs de auditoría | 90 días | Rotación y eliminación segura |
| Cuentas inactivas | 36 meses | Anonimizar previa verificación |
| Respaldos | 30 días (o según política) | Destrucción segura y cifrada |

La institución debe aprobar esta tabla y alinearla con su tabla de retención documental.

---

## 7. Encargados y transferencias internacionales

| Proveedor | Servicio | País | Datos | Salvaguarda |
| :--- | :--- | :--- | :--- | :--- |
| Supabase | PostgreSQL administrado | EE. UU. | Todos los datos de la aplicación | Contrato, TLS, acceso restringido, copias cifradas |
| Render | Hosting de API | EE. UU. | Tráfico y logs | Contrato, TLS, variables de entorno |
| GitHub Pages | Frontend estático | EE. UU. | Sin datos de aplicación | No almacena datos personales |
| Google | OAuth y reCAPTCHA | EE. UU. | Correo, token, IP | Términos de Google, uso opcional |

Obligaciones: mantener copia de contratos y cláusulas, verificar medidas de seguridad, y
comunicar al titular la existencia de transferencias en la política.

---

## 8. Seguridad de la información

- Control de acceso por roles y mínimo privilegio.
- Autenticación con JWT, expiración y revocación de sesión.
- Contraseñas con BCrypt y política de 12 caracteres.
- Cifrado en tránsito y verificación de certificados.
- Registro de auditoría con redacción de datos sensibles.
- Gestión de incidentes según `docs/PLAN_RESPUESTA_INCIDENTES.md`.
- Respaldos cifrados y restauración controlada (`docs/OPERACION_BACKUP.md`).
- Revisión de dependencias y análisis estático en CI.

---

## 9. Incidentes y notificación

1. Detectar y contener.
2. Clasificar el incidente (ver plan).
3. Notificar al DPO y al responsable en un máximo de 24 horas.
4. Evaluar el riesgo para los titulares; si es alto, informarles y reportar a la SIC cuando corresponda.
5. Documentar causa raíz, acciones y evidencias.
6. Actualizar la matriz de riesgos y los controles.

---

## 10. Revisión y mejora

- Revisión anual de la política y del RAT.
- Revisión tras cada incidente relevante o cambio normativo.
- Indicadores: cobertura de consentimiento, incidentes, solicitudes ARCO atendidas en plazo,
  vulnerabilidades abiertas, disponibilidad y tiempos de respuesta.
- Registro de cambios con versión, fecha, autor y aprobación.

---

## 11. Aprobación

| Cargo | Nombre | Firma | Fecha |
| :--- | :--- | :--- | :--- |
| Representante legal | | | |
| Oficial de Protección de Datos | | | |
| Coordinación de TI | | | |
