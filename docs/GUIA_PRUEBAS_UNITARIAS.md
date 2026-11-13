# 🧪 Guía Completa de Pruebas Unitarias y de Seguridad — LMS Primaria

Esta guía detalla la arquitectura, el inventario completo y los procedimientos paso a paso para ejecutar las pruebas unitarias y de seguridad del sistema **LMS Primaria**.

---

## 📋 Resumen General de la Suite

El software cuenta con suites automatizadas independientes para el **Backend** y para el **Frontend**, diseñadas bajo el principio de **aislamiento total** (las pruebas no dependen de servidores externos ni de una base de datos PostgreSQL activa; usan mocks y bases de datos en memoria):

| Componente | Framework | Ubicación | Total de Pruebas | Entorno de Ejecución |
| :--- | :--- | :--- | :---: | :--- |
| **Frontend** | Angular 18, Karma, Jasmine | [`frontend/src`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/frontend/src) | **24 pruebas** | `ChromeHeadless` / Chrome |
| **Backend** | .NET 8, xUnit, Moq, EF Core InMemory | [`backend/LMS.API.Tests`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests) | **124+ casos** *(129 casos / 80 métodos)* | CLI `dotnet test` / Release |

---

## ⚙️ 1. Requisitos Previos y Diagnóstico Automático

Para ejecutar las pruebas localmente requieres:
1. **.NET 8 SDK** (para compilar y ejecutar el proyecto xUnit).
2. **Node.js 20 LTS** y **npm** (para Angular y Karma).
3. **Google Chrome** instalado (necesario para el launcher de Karma en modo headless o interactivo).

### Diagnóstico e Instalación Automática
El proyecto incluye un script en batch que detecta e instala automáticamente cualquier componente faltante (.NET SDK, Node.js, dependencias NuGet y paquetes npm):

```cmd
LMS\scripts\0_VERIFICAR_ENTORNO.bat
```

---

## 🎨 2. Pruebas del Frontend (Angular 18 + Karma + Jasmine)

Las **24 pruebas** del cliente web verifican la seguridad de la sesión, la mitigación de fuga de credenciales, el aislamiento multi-pestaña y la accesibilidad según **WCAG 2.1 AA** y la norma colombiana **NTC 5854**.

### Inventario de Archivos y Pruebas del Frontend

#### 1. [`role.guard.spec.ts`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/frontend/src/app/core/guards/role.guard.spec.ts) (3 pruebas)
- `permite el acceso cuando el usuario tiene el rol requerido`: valida que un usuario con el rol correspondiente pueda ingresar a sus rutas.
- `bloquea la escalación de privilegios cuando un Estudiante intenta acceder a una ruta Admin`: garantiza que un rol `Student` no pueda ingresar a `/admin/**`.
- `redirige a /login cuando no hay rol activo en sesión`: expulsa a la pantalla de login cuando se accede sin credenciales.

#### 2. [`jwt.interceptor.spec.ts`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/frontend/src/app/core/interceptors/jwt.interceptor.spec.ts) (4 pruebas)
- `valida estrictamente orígenes de confianza con isTrustedApiUrl (mitigación CWE-200)`: solo envía tokens a dominios oficiales autorizados.
- `adjunta el encabezado Authorization Bearer en peticiones a la API legítima`: comprueba el formato `Bearer <token>`.
- `NO filtra el token JWT hacia dominios externos de terceros`: evita exfiltración de credenciales a CDNs o APIs externas.
- `limpia la sesión y redirige a /login cuando un endpoint protegido responde 401`: desconecta al usuario si el token es revocado.

#### 3. [`auth.service.spec.ts`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/frontend/src/app/core/services/auth.service.spec.ts) (5 pruebas)
- `devuelve el token JWT cuando es vigente y no ha expirado`: maneja el ciclo de vida del token activo.
- `descarta automáticamente un token JWT expirado y limpia la sesión`: no reutiliza tokens caducados.
- `rota el token JWT en localStorage al cambiar la contraseña exitosamente`: actualiza el token tras cambio de clave.
- `clearSessionData elimina todos los artefactos de sesión y reinicia signals`: borra memoria RAM y almacenamiento local de forma limpia.
- `conserva el token aislado de la pestaña en sessionStorage cuando otra pestaña inicia sesión con un usuario distinto`: soporte multi-pestaña para salas de cómputo compartidas.

#### 4. [`password-policy.service.spec.ts`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/frontend/src/app/core/services/password-policy.service.spec.ts) (6 pruebas)
- `acepta una contraseña de 8 caracteres con todos los requisitos`: valida el cumplimiento mínimo de complejidad.
- `acepta una contraseña de 64 caracteres`: valida el límite superior de longitud.
- `rechaza contraseñas de menos de 8 caracteres`: bloquea claves cortas.
- `rechaza contraseñas de más de 64 caracteres`: previene ataques de denegación de servicio (DoS) por hashing costoso.
- `rechaza contraseñas que contienen el nombre o el documento`: evita contraseñas predecibles con datos del titular.
- `genera una contraseña temporal de 12 caracteres que cumple la política`: verificación del generador aleatorio seguro.

#### 5. [`theme.service.spec.ts`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/frontend/src/app/core/services/theme.service.spec.ts) (2 pruebas)
- `sincroniza <meta name="color-scheme"> y atributos data-theme al cambiar a modo oscuro`: accesibilidad visual y contraste del navegador.
- `alterna correctamente entre modo claro y oscuro con toggleTheme()`: prueba de estado del tema.

#### 6. [`login.accessibility.spec.ts`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/frontend/src/app/features/auth/login/login.accessibility.spec.ts) (4 pruebas)
- `no contiene enlaces inseguros con pseudo-protocolo javascript:void(0) (cumplimiento CSP)`: previene ejecución inline no permitida por CSP.
- `utiliza botones nativos accesibles para alternar entre Iniciar Sesión y Registro (WCAG 4.1.2)`: controles interactivos estándar para lectores de pantalla.
- `expone estados ARIA válidos en las pestañas de autenticación (role="tablist" y aria-selected)`: accesibilidad semántica.
- `asocia etiquetas accesibles a todos los campos de entrada visibles`: cumplimiento de inputs etiquetados con labels asociados.

---

### Comandos de Ejecución — Frontend

Posiciónate en el directorio del frontend:
```bash
cd LMS/frontend
```

#### A. Ejecución de la suite completa en consola (Rápida / Modo Headless)
```bash
npm test
```
*Ejecuta todas las 24 pruebas en segundo plano utilizando `ChromeHeadlessNoSandbox` y finaliza automáticamente.*

#### B. Modo Interactivo con Interfaz Gráfica (Watch Mode)
```bash
npx ng test --watch=true --browsers=Chrome
```
*Abre una ventana de Google Chrome con la consola visual de Jasmine, permitiendo volver a ejecutar pruebas automáticamente al guardar cambios en el código.*

#### C. Ejecutar un solo archivo de prueba específico
```bash
# Ejemplo: Solo probar el servicio de autenticación
npx ng test --include="src/app/core/services/auth.service.spec.ts" --watch=false

# Ejemplo: Solo probar accesibilidad del Login
npx ng test --include="src/app/features/auth/login/login.accessibility.spec.ts" --watch=false
```

#### D. Generar Reporte de Cobertura de Código (HTML)
```bash
npx ng test --code-coverage --watch=false
```
*Genera un informe interactivo en `LMS/frontend/coverage/lms-primaria/index.html` que puedes abrir en el navegador para ver líneas y ramas cubiertas.*

---

## 🛡️ 3. Pruebas del Backend (.NET 8 + xUnit)

La suite de pruebas del backend garantiza la integridad de los controladores frente a ataques comunes (OWASP Top 10), mitigación contra **IDOR** (Insecure Direct Object References), mitigación de **BOLA** (Broken Object Level Authorization), cumplimiento de la **Ley 1581 de 2012** (Habeas Data) y validación de seguridad de archivos.

### Inventario de Archivos y Pruebas del Backend (13 Suites)

| Archivo de Prueba | Casos | Aspectos Clave Evaluados |
| :--- | :---: | :--- |
| [`AdminControllerSecurityTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/AdminControllerSecurityTests.cs) | 11 | Prevención de auto-bloqueo del último admin activo, invalidación inmediata de tokens al cambiar roles/claves, protección contra RCE/SQL Injection en restauración de backups y validación estricta del rol docente al asignar cursos. |
| [`AuditQueueAndHealthTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/AuditQueueAndHealthTests.cs) | 3 | Operación FIFO de la cola asíncrona de auditoría forense (`AuditLogQueue`), inicialización segura de la entidad `AuditLog` y cálculo de promedios en la libreta de calificaciones. |
| [`AuthControllerSecurityTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/AuthControllerSecurityTests.cs) | 10 | Bloqueo por fuerza bruta tras 5 intentos fallidos (HTTP 423 Locked por 15 minutos), honeypots, validación fail-closed de reCAPTCHA, prohibición de autoregistro de Administradores y rotación de tokens. |
| [`ConsentServiceTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/ConsentServiceTests.cs) | 6 | Firma HMAC de consentimientos de menores, enmascaramiento de documentos de acudientes y cálculo de 10 y 15 días hábiles colombianos con Ley Emiliani (Ley 51 de 1983) y algoritmo astronómico de Pascua. |
| [`FilesControllerSecurityTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/FilesControllerSecurityTests.cs) | 8 | Prevención de vulnerabilidades IDOR en descargas de tareas entre estudiantes distintos o docentes no autorizados, y bloqueo de ataques de Path Traversal y bytes nulos. |
| [`FileSignatureValidatorTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/FileSignatureValidatorTests.cs) | 19 | Validación rigurosa de cabeceras binarias (*magic bytes*) en PDF y PNG para evitar que scripts ejecutables (.exe, .sh, .php) sean subidos cambiando solo la extensión. |
| [`GoogleRecaptchaServiceTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/GoogleRecaptchaServiceTests.cs) | 8 | Política fail-closed en producción (si no hay clave configurada en el servidor, rechaza por defecto) y limitación estricta de tokens de prueba exclusivamente al entorno `Development`. |
| [`MiddlewareSecurityTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/MiddlewareSecurityTests.cs) | 6 | Inyección de cabeceras de seguridad CSP, HSTS y `X-Frame-Options`, detección de concurrencia y sesión duplicada (HTTP 401), obligatoriedad de política de privacidad (HTTP 403), cero fugas de `StackTrace` en respuestas HTTP y sanitización de credenciales en logs. |
| [`PasswordPolicyTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/PasswordPolicyTests.cs) | 15 | Reglas de longitud entre 8 y 64 caracteres, rechazo de patrones débiles o comunes (`Admin123!`), rechazo de nombres del usuario dentro de la clave y generación segura de credenciales temporales. |
| [`StudentControllerSecurityTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/StudentControllerSecurityTests.cs) | 6 | Mitigación de BOLA: impide que alumnos se matriculen o entreguen tareas en grados que no les corresponden, y bloqueo del panel académico si el acudiente revoca el consentimiento legal. |
| [`SystemFilesSecurityTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/SystemFilesSecurityTests.cs) | 19 | Protección de endpoints de gestión de archivos del sistema: acceso exclusivo a Administradores, bloqueo estricto de Path Traversal y preservación de registros forenses de auditoría. |
| [`TeacherControllerSecurityTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/TeacherControllerSecurityTests.cs) | 12 | Mitigación de IDOR docente: un profesor no puede alterar cursos, tareas, entregas, notas ni consultar el semáforo escolar de estudiantes pertenecientes a otro docente; aislamiento de métricas y desempaquetado de metadatos de adjuntos. |
| [`TokenServiceTests.cs`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/backend/LMS.API.Tests/TokenServiceTests.cs) | 6 | Emisión de tokens JWT con roles canónicos estandarizados y claim `session_token`, rechazo de claves de firma cortas o inseguras y normalización de roles heredados. |

---

### Comandos de Ejecución — Backend

Posiciónate en el directorio del backend o ejecuta desde la raíz:
```bash
cd LMS/backend
```

#### A. Ejecución de la suite completa (Release)
```bash
dotnet test LMS.API.Tests/LMS.API.Tests.csproj -c Release
```

#### B. Ver el listado detallado de cada prueba aprobada
```bash
dotnet test LMS.API.Tests/LMS.API.Tests.csproj --logger "console;verbosity=detailed"
```

#### C. Ejecutar únicamente una suite o archivo específico
```bash
# Probar únicamente autenticación y fuerza bruta
dotnet test LMS.API.Tests/LMS.API.Tests.csproj --filter "FullyQualifiedName~AuthControllerSecurityTests"

# Probar únicamente protección anti-IDOR en profesores
dotnet test LMS.API.Tests/LMS.API.Tests.csproj --filter "FullyQualifiedName~TeacherControllerSecurityTests"

# Probar únicamente consentimiento Habeas Data y festivos colombianos
dotnet test LMS.API.Tests/LMS.API.Tests.csproj --filter "FullyQualifiedName~ConsentServiceTests"

# Probar únicamente validación de firmas de archivos
dotnet test LMS.API.Tests/LMS.API.Tests.csproj --filter "FullyQualifiedName~FileSignatureValidatorTests"
```

#### D. Medir Cobertura de Código del Backend
```bash
dotnet test LMS.API.Tests/LMS.API.Tests.csproj --collect:"XPlat Code Coverage"
```

---

## 🚀 4. Pruebas de Integración y Humo (Scripts PowerShell)

Para verificar el comportamiento de la API en vivo una vez iniciada localmente (usando `LMS\scripts\1_INICIAR_BACKEND.bat`):

1. **Prueba de Humo de Seguridad (`security_smoke_test.ps1`):**
   Valida cabeceras HTTP en vivo, CORS restringido, endpoint `/api/ping` y Swagger en modo desarrollo:
   ```powershell
   powershell -ExecutionPolicy Bypass -File "LMS\scripts\security_smoke_test.ps1"
   ```

2. **Prueba Integral de Endpoints (`test_all_endpoints.ps1`):**
   Realiza una ronda de peticiones automatizadas a las rutas públicas y protegidas:
   ```powershell
   powershell -ExecutionPolicy Bypass -File "LMS\scripts\test_all_endpoints.ps1"
   ```

---

## 🔄 5. Integración Continua (CI/CD en GitHub Actions)

Tanto las 24 pruebas del frontend como las pruebas del backend se ejecutan automáticamente en cada `push` o `pull_request` a la rama `main` a través del workflow [`.github/workflows/ci.yml`](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/.github/workflows/ci.yml):

```yaml
# Fragmento del CI Backend
- name: Pruebas unitarias
  run: dotnet test backend/LMS.API.Tests/LMS.API.Tests.csproj -c Release --nologo

# Fragmento del CI Frontend
- name: Pruebas unitarias
  run: npm test
```

---

## 🛠️ 6. Solución de Problemas Frecuentes

* **Error: `No .NET SDKs were found`**:
  * *Causa:* La variable de entorno de .NET no está apuntando al SDK o este no está instalado.
  * *Solución:* Ejecuta `LMS\scripts\0_VERIFICAR_ENTORNO.bat` para instalar automáticamente el SDK oficial de .NET 8.
* **Error de Karma: `Cannot start ChromeHeadless`**:
  * *Causa:* Google Chrome no está instalado o faltan permisos en el sandbox.
  * *Solución:* Asegúrate de tener instalado Google Chrome. El proyecto ya incluye las flags `--no-sandbox` y `--disable-gpu` en [karma.conf.js](file:///c:/Users/SNEIDER/Documents/Nueva%20carpeta/LMS%20(2)/LMS/frontend/karma.conf.js) para compatibilidad total en Windows y entornos de CI.
* **Error en Frontend: `node_modules` desactualizados**:
  * *Solución:* Entra a `LMS/frontend` y corre `npm ci`.
