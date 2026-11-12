# FitExpert en AWS — Guía paso a paso

Arquitectura de la solución:

```
Navegador (interfaz web)  ──HTTPS──►  AWS Lambda + URL de función  ──SQL──►  Amazon RDS for SQL Server (Express)
   wwwroot/index.html                   FitExpert.Web: API REST y motor          Pregunta · Hecho · Hipotesis
                                        de inferencia en C# (.NET 10)            NodoDecision · Regla · CondicionRegla
```

Costos: Lambda está dentro de su capa siempre gratuita. RDS se paga con los créditos de bienvenida
del plan gratuito; en el plan **Free** la tarjeta no se cobra. Usa siempre la **misma región**
(por ejemplo *US East (N. Virginia) us-east-1*) para todo.

---

## Paso 1. Crear la cuenta

1. Entrar a https://aws.amazon.com/free → **Crear cuenta gratuita**.
2. Elegir el plan **Free** (no el Paid).
3. AWS pide correo, teléfono y normalmente una tarjeta para verificar identidad. En el plan Free
   no se cobra: los consumos salen de los créditos.
4. Ya dentro de la consola, buscar **Billing and Cost Management** → **Free Tier** y verificar los créditos.

> Si no tienes tarjeta: pregúntale al docente por **AWS Academy Learner Lab**, que la universidad puede
> habilitar sin tarjeta para el curso.

## Paso 2. Explorar los servicios de IA (para el video)

En la consola, abrir el menú **Services → Machine Learning / Artificial Intelligence**:

| Servicio | Para qué sirve |
|---|---|
| Amazon Bedrock | Modelos fundacionales, agentes y bases de conocimiento |
| Bedrock Guardrails · Automated Reasoning checks | Verifica respuestas con reglas lógicas formales (lo más cercano a un sistema experto) |
| Amazon SageMaker AI | Entrenar y publicar modelos propios |
| Amazon Lex | Chatbots con intenciones y reglas de diálogo |
| Amazon Comprehend, Rekognition, Textract, Polly, Transcribe | Texto, imágenes, documentos y voz |

**Justificación de la elección (decirla en el video):** los servicios de IA de AWS trabajan con
modelos probabilísticos o tienen costo por uso, y ninguno ejecuta directamente una base de reglas
SI–ENTONCES como la del proyecto. Por eso el sistema experto se publica como **servicio propio**:
la base de conocimiento con sus reglas queda en **Amazon RDS** y el motor de inferencia se expone
como API REST en **AWS Lambda**. Es el modelo de *sistema experto como servicio*.

---

## Paso 3A. Base de conocimiento en Amazon RDS for SQL Server

1. Consola → **RDS** → **Create database**.
2. Configuración:
   - Creation method: **Standard create**
   - Engine: **Microsoft SQL Server** → Edition: **SQL Server Express Edition**
   - Templates: **Free tier** (si aparece)
   - DB instance identifier: `fitexpert-db`
   - Master username: `admin` · Credentials management: **Self managed** · escribir y anotar la contraseña
   - Instance class: **db.t3.micro**
   - Storage: **20 GiB**, desmarcar *Enable storage autoscaling*
   - Connectivity → Public access: **Yes**
   - VPC security group: **Create new** → nombre `fitexpert-sg`
   - Additional configuration: desmarcar *Performance Insights* y *Enhanced monitoring*
3. **Create database**. Tarda entre 10 y 20 minutos hasta quedar *Available*.
4. Abrir los permisos de red: en la base → **Connectivity & security** → clic en el grupo `fitexpert-sg`
   → **Inbound rules** → **Edit inbound rules**:
   - Regla 1: Type **MSSQL**, Source **My IP** (para conectarte desde SSMS)
   - Regla 2: Type **MSSQL**, Source **Anywhere-IPv4 (0.0.0.0/0)** (para que Lambda llegue a la base)
   - **Save rules**

   > La regla 2 deja el puerto abierto a internet. Es aceptable para un proyecto académico con una
   > contraseña fuerte; elimina la base cuando termines la actividad.
5. Copiar el **Endpoint** que aparece en *Connectivity & security* (termina en `.rds.amazonaws.com`).
6. Abrir **SSMS** → Connect:
   - Server name: `ENDPOINT,1433`
   - Authentication: **SQL Server Authentication** · Login `admin` y tu contraseña
   - Encryption: **Mandatory** y marcar **Trust server certificate**
7. Abrir `FitExpertDB_AWS.sql` y ejecutar con **F5**. El script crea la base `FitExpertDB`.
8. Verificar: 9 preguntas, 27 hechos, 26 hipótesis, 40 nodos del árbol (13 de decisión y 27 terminales),
   27 reglas y la consulta de coherencia del árbol **sin filas** (si devuelve filas, el árbol tiene una rama mal cargada).

Cadena de conexión para los pasos siguientes (reemplaza ENDPOINT y la contraseña):

```
Server=ENDPOINT,1433;Database=FitExpertDB;User ID=admin;Password=CONTRASEÑA;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;
```

## Paso 3B. Probar el servicio en el computador

1. Copiar la carpeta `FitExpert.Web` **al lado** de `FitExpert.Motor` (no adentro), por ejemplo
   `C:\Users\USUARIO\FitExpert.Web`.
2. Visual Studio 2026: clic derecho sobre la solución → **Agregar** → **Proyecto existente** → `FitExpert.Web.csproj`.
3. Pegar la cadena de conexión en `appsettings.Development.json`.
4. Clic derecho sobre `FitExpert.Web` → **Establecer como proyecto de inicio** → **F5**.
5. Se abre el navegador con la interfaz: recorrer el árbol de decisión pregunta por pregunta y generar la recomendación.

> No subas `appsettings.Development.json` a GitHub: contiene la contraseña.

## Paso 3C. Publicar en AWS Lambda

**Empaquetar** (terminal de PowerShell dentro de la carpeta `FitExpert.Web`):

```
dotnet publish -c Release -r linux-x64 --self-contained false -o publish
Compress-Archive -Path publish\* -DestinationPath fitexpert.zip -Force
```

**Crear la función:**

1. Consola → **Lambda** → **Create function** → **Author from scratch**:
   - Function name: `fitexpert`
   - Runtime: **.NET 10 (C#/F#/PowerShell)**
   - Architecture: **x86_64**
   - **Create function**
2. Pestaña **Code** → **Upload from** → **.zip file** → elegir `fitexpert.zip` → **Save**.
3. En **Runtime settings** → **Edit** → Handler: `FitExpert.Web` → **Save**.
4. **Configuration** → **General configuration** → **Edit**: Memory **512 MB**, Timeout **30 sec** → **Save**.
5. **Configuration** → **Environment variables** → **Edit** → **Add environment variable**:
   - Key: `ConnectionStrings__FitExpertDB` (con dos guiones bajos)
   - Value: la cadena de conexión del paso 3A
   - **Save**
6. **Configuration** → **Function URL** → **Create function URL** → Auth type **NONE** → **Save**.
7. Abrir la **Function URL** en el navegador. La primera carga tarda unos segundos (arranque en frío).

## Paso 3D. Inicio de sesión, historial y nueva interfaz (versión 2)

La interfaz ahora es una app de entrenamiento con dos modalidades de ingreso:

| Perfil | Cuenta inicial | Contraseña | Qué ve |
|---|---|---|---|
| Usuario final | `usuario` | `Usuario2026*` | Inicio con su plan actual y hábitos del día, evaluación pregunta por pregunta, su plan con rutina, dieta y explicación, e historial |
| Administrador | `admin` | `Admin2026*` | Panel con cifras de uso, reglas de la base de conocimiento, edición de hipótesis y gestión de cuentas |

Los usuarios finales pueden crear su propia cuenta desde la pantalla de ingreso.

**1. Agregar las tablas nuevas a RDS.** En SSMS, conectado a RDS, abrir `FitExpert_Login_AWS.sql` y ejecutar
con **F5**. Recrea las tablas `Usuario`, `Evaluacion`, `EvaluacionRespuesta` y `DetalleHipotesis` sin tocar la
base de conocimiento. (Si ya ejecutaste `FitExpertDB_AWS.sql` completo, este paso no hace falta.)

**2. Crear la clave de los tokens.** El inicio de sesión usa tokens JWT firmados. Inventa una clave larga
(mínimo 32 caracteres), por ejemplo `FitExpert-UMB-2026-clave-secreta-para-tokens`.

- En tu computador (opcional): agrégala a `appsettings.Development.json`:

  ```json
  {
    "ConnectionStrings": { "FitExpertDB": "...tu cadena..." },
    "Jwt": { "Clave": "FitExpert-UMB-2026-clave-secreta-para-tokens" }
  }
  ```

- En Lambda (obligatorio): **Configuración** → **Variables de entorno** → **Editar** → **Agregar variable de entorno**:
  - Clave: `Jwt__Clave` (con dos guiones bajos)
  - Valor: tu clave
  - **Guardar**

  Sin esta variable, cada copia de la función inventa su propia clave y las sesiones se cierran solas.

**3. Volver a publicar.** Repetir el empaquetado del paso 3C (`dotnet publish` y `Compress-Archive`) y en
Lambda: **Código** → **Cargar desde** → **Archivo .zip** → `fitexpert.zip` → **Guardar**.

**4. Probar.** Abrir la URL de la función, ingresar como usuario, hacer una evaluación y revisar el
historial. Luego cerrar sesión e ingresar como administrador.

Rutas del servicio en esta versión:

| Ruta | Acceso |
|---|---|
| `POST /api/auth/login` · `POST /api/auth/registro` · `GET /api/salud` | Público |
| `GET /api/app/arbol` · `POST /api/app/evaluar` · `GET /api/app/evaluaciones` · `GET /api/app/evaluaciones/{id}/hipotesis/{posicion}` | Cualquier sesión |
| `GET /api/admin/resumen` · `/arbol` · `/reglas` · `/hipotesis` · `/usuarios` | Solo administrador |

## Paso 3E. Árbol de decisión y detalle de hipótesis (versión 3)

FitExpert es un **sistema experto basado en reglas**: todas sus salidas salen de la base de conocimiento.
No usa modelos de lenguaje ni servicios de IA generativa (se retiró el coach con Amazon Bedrock).

- **La evaluación recorre el árbol de decisión.** La interfaz lee la tabla `NodoDecision` y muestra solo la
  pregunta del nodo actual; cada respuesta lleva al nodo hijo hasta un nodo terminal (H1 a H26). Por ejemplo,
  si en *¿Presenta alguna condición de riesgo?* se responde **Sí**, la siguiente pregunta es *¿Qué tipo de
  condición presenta?*, y el recorrido termina en H1, H2 o H3. El servicio rechaza cualquier recorrido que no
  siga una rama del árbol.
- **Las reglas salen del árbol.** Cada nodo terminal tiene su regla en `Regla` (R01 a R27) y su parte SI en
  `CondicionRegla`, que el script calcula a partir del camino del árbol. Así el árbol, las reglas y la
  navegación no pueden diferir.
- **Detalle de las 5 hipótesis.** Al evaluar, el motor ordena las hipótesis por su puntuación (granularidad):
  la primera es la conclusión del árbol y las otras cuatro son las alternativas más compatibles. El detalle de
  cada una (rutina, dieta, regla y explicación) se guarda en la tabla `DetalleHipotesis`, y es lo que se
  consulta al dar clic en una hipótesis en **Mi plan**.

Si ya tenías la función publicada con Bedrock, puedes eliminar la política insertada `FitExpertBedrock` del rol
de la función Lambda y la variable `Bedrock__Modelo`; ya no se usan.

## Solución de problemas

| Síntoma | Causa y solución |
|---|---|
| La URL responde `Forbidden` | Falta el permiso público: en **Configuration → Permissions → Resource-based policy** debe existir la política de la URL de función. Borra la URL y créala de nuevo con Auth type NONE. |
| `Task timed out` o error de conexión a la base | Falta la regla 0.0.0.0/0 en `fitexpert-sg` o *Public access* quedó en *No*. |
| Error de *handler* o *assembly* | El Handler debe ser exactamente `FitExpert.Web`. |
| Error sobre ICU o globalización | Agregar la variable de entorno `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` con valor `1`. |
| `Login failed` | Usuario o contraseña de la cadena de conexión incorrectos. |
| La sesión se cierra sola al poco tiempo | Falta la variable `Jwt__Clave` en Lambda (paso 3D.2). |
| `Invalid object name 'NodoDecision'` o `'DetalleHipotesis'` | Falta ejecutar la versión nueva de `FitExpertDB_AWS.sql` (paso 3A.7). |
| «La base de conocimiento no es coherente» al evaluar | Se desactivó o editó a mano una regla de un nodo terminal; vuelve a ejecutar `FitExpertDB_AWS.sql`. |
| `Invalid object name 'Usuario'` | Falta ejecutar `FitExpert_Login_AWS.sql` (paso 3D.1). |

## Cuidar los créditos

- RDS es lo único que consume créditos. Cuando no lo uses: base → **Actions** → **Stop temporarily**
  (se detiene hasta 7 días). Antes de grabar el video, iníciala de nuevo con **Actions → Start**.
- Al terminar la actividad: eliminar la base (**Actions → Delete**, sin snapshot final) y la función Lambda.

---

## Paso 4. Guion del video (8 a 10 minutos)

| Tiempo | Qué mostrar | Qué decir |
|---|---|---|
| 0:00 – 0:45 | Portada del proyecto | Presentación, integrantes y objetivo de la actividad |
| 0:45 – 1:45 | Consola de AWS, plan Free y créditos | Cómo se creó la cuenta y por qué el plan Free no genera cobros |
| 1:45 – 3:15 | Catálogo de IA: Bedrock, SageMaker, Lex, Comprehend | Recorrido por los servicios; ninguno ejecuta una base de reglas como la del proyecto, por eso el SE se publica como servicio propio |
| 3:15 – 4:45 | RDS creada y SSMS ejecutando el script | La base de conocimiento: árbol de decisión (`NodoDecision`), hipótesis, reglas con sus pesos y la vista `vw_Reglas` con las 27 reglas |
| 4:45 – 6:00 | Visual Studio: `FitExpert.Web` y `Program.cs` | El motor de inferencia es el mismo de clase; se expone con `/api/app/arbol`, `/api/app/evaluar` y `/api/admin/reglas` |
| 6:00 – 7:00 | Lambda: subir el .zip, variable de entorno, Function URL | Cómo se publica el servicio sin servidores y cómo se conecta con RDS |
| 7:00 – 9:00 | Interfaz web en la URL de Lambda | Recorrer el árbol, generar la recomendación, abrir el detalle de las hipótesis, mostrar la pestaña de reglas y `/api/salud` |
| 9:00 – 9:30 | Cierre | Separación entre base de conocimiento, motor e interfaz; el SE se consume como servicio desde cualquier cliente |
