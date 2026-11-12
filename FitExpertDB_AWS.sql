/* =====================================================================
   FitExpert - Base de conocimiento para Amazon RDS for SQL Server
   Ejecutar desde SSMS conectado al endpoint de RDS con el usuario
   maestro (admin). Crea la base FitExpertDB si no existe y luego las
   tablas, los datos y las vistas. Si se ejecuta de nuevo, recrea todo
   (se pierde el historial de evaluaciones).

   BASE DE CONOCIMIENTO
       Pregunta          nodos de decision: lo que el sistema pregunta
       Hecho             respuestas posibles de cada pregunta (ramas)
       Hipotesis         conclusiones del sistema (H1 a H26)
       NodoDecision      arbol de decision (relacion recursiva al padre).
                         La interfaz recorre ESTE arbol: cada respuesta
                         lleva al nodo hijo, hasta un nodo terminal Hn.
       Regla             una regla por cada nodo terminal (R01 a R27)
       CondicionRegla    parte SI de cada regla: los hechos del camino
                         desde la raiz hasta el nodo terminal, con su peso

   TRANSACCIONAL
       Usuario           cuentas de acceso (usuario final y administrador)
       Evaluacion        cada ejecucion del motor de inferencia
       EvaluacionRespuesta  camino recorrido en el arbol (pregunta y respuesta)
       DetalleHipotesis  detalle de las 5 hipotesis con mayor puntuacion:
                         rutina, dieta, regla y explicacion de cada una
   ===================================================================== */

IF DB_ID(N'FitExpertDB') IS NULL
    CREATE DATABASE FitExpertDB;
GO
USE FitExpertDB;
GO

DROP VIEW  IF EXISTS vw_Reglas;
DROP VIEW  IF EXISTS vw_HechosPorHipotesis;
DROP VIEW  IF EXISTS vw_ArbolDecision;
DROP TABLE IF EXISTS DetalleHipotesis;
DROP TABLE IF EXISTS EvaluacionRespuesta;
DROP TABLE IF EXISTS Evaluacion;
DROP TABLE IF EXISTS Usuario;
DROP TABLE IF EXISTS CondicionRegla;
DROP TABLE IF EXISTS Regla;
DROP TABLE IF EXISTS NodoDecision;
DROP TABLE IF EXISTS HechoHipotesis;   -- tabla de la version anterior
DROP TABLE IF EXISTS Hecho;
DROP TABLE IF EXISTS Hipotesis;
DROP TABLE IF EXISTS Pregunta;
GO

/* =====================================================================
   BASE DE CONOCIMIENTO
   ===================================================================== */
CREATE TABLE Pregunta (
    IdPregunta  INT IDENTITY(1,1) CONSTRAINT PK_Pregunta PRIMARY KEY,
    Codigo      NVARCHAR(10)  NOT NULL CONSTRAINT UQ_Pregunta_Codigo UNIQUE,
    Texto       NVARCHAR(200) NOT NULL,
    Ayuda       NVARCHAR(200) NULL
);

CREATE TABLE Hecho (
    IdHecho     INT IDENTITY(1,1) CONSTRAINT PK_Hecho PRIMARY KEY,
    IdPregunta  INT           NOT NULL CONSTRAINT FK_Hecho_Pregunta REFERENCES Pregunta(IdPregunta),
    Codigo      NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Hecho_Codigo UNIQUE,
    Texto       NVARCHAR(120) NOT NULL,   -- se muestra como opcion al usuario
    Enunciado   NVARCHAR(120) NOT NULL,   -- se usa al redactar la regla SI ... ENTONCES
    Orden       INT           NOT NULL
);

CREATE TABLE Hipotesis (
    IdHipotesis   INT IDENTITY(1,1) CONSTRAINT PK_Hipotesis PRIMARY KEY,
    Codigo        NVARCHAR(5)   NOT NULL CONSTRAINT UQ_Hipotesis_Codigo UNIQUE,
    Nombre        NVARCHAR(80)  NOT NULL,
    Area          NVARCHAR(20)  NOT NULL CONSTRAINT CK_Hipotesis_Area
                  CHECK (Area IN (N'SEGURIDAD', N'DISPONIBILIDAD', N'RECUPERACION', N'RESTRICCION', N'ENTRENAMIENTO')),
    Prioridad     INT           NOT NULL CONSTRAINT CK_Hipotesis_Prioridad CHECK (Prioridad BETWEEN 1 AND 100),
    Rutina        NVARCHAR(400) NOT NULL,
    Dieta         NVARCHAR(400) NOT NULL,
    -- Aviso obligatorio. Si existe, el recorrido concluye en esta hipotesis:
    -- no se presentan alternativas y se remite al usuario a un profesional.
    Aviso         NVARCHAR(400) NULL
);

/* Arbol de decision. Un nodo de decision formula una Pregunta; un nodo
   terminal concluye una Hipotesis. IdHechoRama es la respuesta del nodo
   padre que conduce a este nodo (la etiqueta de la rama). */
CREATE TABLE NodoDecision (
    IdNodo       INT          NOT NULL CONSTRAINT PK_NodoDecision PRIMARY KEY,
    Codigo       NVARCHAR(5)  NOT NULL CONSTRAINT UQ_NodoDecision_Codigo UNIQUE,
    IdNodoPadre  INT          NULL CONSTRAINT FK_Nodo_Padre     REFERENCES NodoDecision(IdNodo),
    IdHechoRama  INT          NULL CONSTRAINT FK_Nodo_Hecho     REFERENCES Hecho(IdHecho),
    IdPregunta   INT          NULL CONSTRAINT FK_Nodo_Pregunta  REFERENCES Pregunta(IdPregunta),
    IdHipotesis  INT          NULL CONSTRAINT FK_Nodo_Hipotesis REFERENCES Hipotesis(IdHipotesis),
    CONSTRAINT CK_Nodo_Tipo CHECK ((IdPregunta IS NOT NULL AND IdHipotesis IS NULL)
                                OR (IdPregunta IS NULL AND IdHipotesis IS NOT NULL)),
    CONSTRAINT CK_Nodo_Rama CHECK ((IdNodoPadre IS NULL AND IdHechoRama IS NULL)
                                OR (IdNodoPadre IS NOT NULL AND IdHechoRama IS NOT NULL)),
    CONSTRAINT UQ_Nodo_Rama UNIQUE (IdNodoPadre, IdHechoRama)
);

/* Una regla por nodo terminal: el camino del arbol es la regla. */
CREATE TABLE Regla (
    IdRegla        INT IDENTITY(1,1) CONSTRAINT PK_Regla PRIMARY KEY,
    Codigo         NVARCHAR(5) NOT NULL CONSTRAINT UQ_Regla_Codigo UNIQUE,
    IdHipotesis    INT NOT NULL CONSTRAINT FK_Regla_Hipotesis REFERENCES Hipotesis(IdHipotesis),
    IdNodoTerminal INT NOT NULL CONSTRAINT FK_Regla_Nodo REFERENCES NodoDecision(IdNodo)
                   CONSTRAINT UQ_Regla_Nodo UNIQUE,
    Activa         BIT NOT NULL CONSTRAINT DF_Regla_Activa DEFAULT (1)
);

/* Parte SI de cada regla. Orden = profundidad en el arbol. El peso crece
   con la profundidad: la condicion mas especifica aporta mas evidencia. */
CREATE TABLE CondicionRegla (
    IdRegla  INT NOT NULL CONSTRAINT FK_CR_Regla REFERENCES Regla(IdRegla),
    IdHecho  INT NOT NULL CONSTRAINT FK_CR_Hecho REFERENCES Hecho(IdHecho),
    Orden    INT NOT NULL,
    Peso     DECIMAL(4,2) NOT NULL CONSTRAINT CK_CR_Peso CHECK (Peso > 0 AND Peso <= 1),
    CONSTRAINT PK_CondicionRegla PRIMARY KEY (IdRegla, IdHecho)
);
GO

/* ------------------------------------------------------------ preguntas */
INSERT INTO Pregunta (Codigo, Texto, Ayuda) VALUES
    (N'P1', N'¿Presenta alguna condición de riesgo para entrenar?', N'Se considera condición de riesgo: una condición médica grave diagnosticada, tener menos de 16 años o tener 60 años o más.'),
    (N'P2', N'¿Qué tipo de condición presenta?', NULL),
    (N'P3', N'¿Cuál es su disponibilidad para entrenar?', N'Se necesita al menos 1 día por semana y sesiones de 15 minutos o más para un plan estructurado.'),
    (N'P4', N'¿Cómo califica su nivel de fatiga actual?', NULL),
    (N'P5', N'¿Cuántas horas duerme en promedio por noche?', NULL),
    (N'P6', N'¿Tiene alguna restricción física o lesión?', NULL),
    (N'P7', N'¿En qué zona se presenta la restricción?', NULL),
    (N'P8', N'¿Cuál es su objetivo principal?', NULL),
    (N'P9', N'¿Cuál es su nivel de experiencia entrenando?', NULL);

/* ------------------------------------------------------------ hechos (ramas) */
INSERT INTO Hecho (IdPregunta, Codigo, Texto, Enunciado, Orden) VALUES
    (1, N'F_RIESGO_SI', N'Sí, presento una condición de riesgo', N'presenta una condición de riesgo', 1),
    (1, N'F_RIESGO_NO', N'No presento ninguna condición de riesgo', N'no presenta condiciones de riesgo', 2),
    (2, N'F_COND_MEDICA', N'Condición médica grave diagnosticada', N'tiene una condición médica grave diagnosticada', 1),
    (2, N'F_COND_MENOR16', N'Tengo menos de 16 años', N'es menor de 16 años', 2),
    (2, N'F_COND_60MAS', N'Tengo 60 años o más', N'tiene 60 años o más', 3),
    (3, N'F_DISP_MENOS1DIA', N'Menos de 1 día por semana', N'dispone de menos de 1 día por semana', 1),
    (3, N'F_DISP_MENOS15MIN', N'Sesiones de menos de 15 minutos', N'dispone de sesiones de menos de 15 minutos', 2),
    (3, N'F_DISP_SUFICIENTE', N'1 día o más por semana y sesiones de 15 minutos o más', N'tiene disponibilidad suficiente', 3),
    (4, N'F_FATIGA_ALTA', N'Alta, me siento agotado', N'tiene fatiga alta', 1),
    (4, N'F_FATIGA_MOD', N'Moderada', N'tiene fatiga moderada', 2),
    (4, N'F_FATIGA_BAJA', N'Baja, me siento con energía', N'tiene fatiga baja', 3),
    (5, N'F_SUENO_MENOS5', N'Menos de 5 horas', N'duerme menos de 5 horas', 1),
    (5, N'F_SUENO_5MAS', N'5 horas o más', N'duerme 5 horas o más', 2),
    (6, N'F_REST_SI', N'Sí, tengo una restricción o lesión', N'tiene una restricción física', 1),
    (6, N'F_REST_NO', N'No tengo restricciones', N'no tiene restricciones físicas', 2),
    (7, N'F_ZONA_RODILLA', N'Rodilla', N'tiene la restricción en la rodilla', 1),
    (7, N'F_ZONA_HOMBRO', N'Hombro', N'tiene la restricción en el hombro', 2),
    (7, N'F_ZONA_LUMBAR', N'Zona lumbar', N'tiene la restricción en la zona lumbar', 3),
    (7, N'F_ZONA_OTRA', N'Otra zona', N'tiene la restricción en otra zona', 4),
    (8, N'F_OBJ_MASA', N'Ganar masa muscular', N'su objetivo es ganar masa muscular', 1),
    (8, N'F_OBJ_FUERZA', N'Aumentar fuerza', N'su objetivo es aumentar fuerza', 2),
    (8, N'F_OBJ_RESIS', N'Mejorar resistencia', N'su objetivo es mejorar resistencia', 3),
    (8, N'F_OBJ_PESO', N'Perder peso', N'su objetivo es perder peso', 4),
    (8, N'F_OBJ_MANTENER', N'Mantener la condición física', N'su objetivo es mantener la condición física', 5),
    (9, N'F_NIVEL_PRIN', N'Principiante', N'su nivel es principiante', 1),
    (9, N'F_NIVEL_INTER', N'Intermedio', N'su nivel es intermedio', 2),
    (9, N'F_NIVEL_AVAN', N'Avanzado', N'su nivel es avanzado', 3);

/* ------------------------------------------------------------ hipotesis */
INSERT INTO Hipotesis (Codigo, Nombre, Area, Prioridad, Rutina, Dieta) VALUES
    (N'H1', N'Plan de bajo impacto adaptado', N'SEGURIDAD', 100, N'3 días por semana, 30 min, RPE 3-4. 20 min de caminata o bicicleta estática a ritmo conversacional; 2x10-12 de sentarse y levantarse de una silla, remo con banda y elevación de talones; 10 min de movilidad.', N'Calorías de mantenimiento, proteína 1,0-1,2 g/kg. Plato saludable: mitad verduras, un cuarto proteína magra y un cuarto cereal integral. Menos de 5 g de sal al día en 5 comidas pequeñas.'),
    (N'H2', N'Plan juvenil formativo', N'SEGURIDAD', 100, N'3 días por semana, 45-60 min, solo peso corporal. Juego o deporte más 2-3x10-15 de sentadilla, flexión inclinada y plancha de 20-30 s; énfasis en la técnica y sin cargas máximas. 60 min diarios de actividad.', N'Sin restricción calórica, proteína 1,2-1,5 g/kg. Tres comidas y dos meriendas, con 3 porciones de lácteos al día.'),
    (N'H3', N'Fuerza funcional y equilibrio', N'SEGURIDAD', 100, N'3 días no consecutivos, 40 min, RPE 5-6. 2-3x10-12 de sentadilla a silla, remo con banda, flexión en pared y subida a escalón bajo; 10 min de equilibrio (apoyo en un pie, caminar talón-punta).', N'Mantenimiento, proteína 1,0-1,2 g/kg con 25-30 g por comida. Calcio y vitamina D; agua a horas fijas.'),
    (N'H4', N'Plan de actividad diaria', N'DISPONIBILIDAD', 95, N'8.000 a 10.000 pasos diarios y 2 pausas activas de 5 min: 10 sentadillas, 10 flexiones en pared y 30 s de plancha. Usar escaleras y ponerse de pie 5 min cada hora.', N'Mantenimiento, proteína 1,2 g/kg. Plato saludable, sin bebidas azucaradas y con pocos ultraprocesados.'),
    (N'H5', N'Micro-rutina diaria de 10 minutos', N'DISPONIBILIDAD', 95, N'EMOM de 10 min todos los días: minuto 1, 12 sentadillas; minuto 2, 8-10 flexiones; minuto 3, 10 zancadas; minuto 4, 30 s de plancha; repetir. Alternar un día de fuerza y otro de cardio.', N'Mantenimiento, proteína 1,4-1,6 g/kg. Comidas preparadas en lote para ahorrar tiempo.'),
    (N'H6', N'Plan de recuperación de dos semanas', N'RECUPERACION', 90, N'3 días por semana a RPE 4 o menos, sin HIIT ni cargas pesadas: 30 min de caminata y 20 min de movilidad o yoga suave. Horario de sueño fijo, meta de 7-9 h y sin pantallas 1 h antes de dormir.', N'Mantenimiento sin déficit, proteína 1,6 g/kg. Carbohidratos complejos en la cena, alimentos con magnesio y sin cafeína después de las 2 p. m.'),
    (N'H7', N'Semana de descarga', N'RECUPERACION', 90, N'Una semana con la mitad de las series, RPE 5-6 y un día más de descanso, conservando los mismos ejercicios; luego se retoma el plan habitual.', N'Mantenimiento, proteína 1,6 g/kg. No recortar carbohidratos y consumir 5 porciones de frutas y verduras al día.'),
    (N'H8', N'Rutina sin impacto en rodilla', N'RESTRICCION', 85, N'3 días por semana sin saltos ni sentadilla profunda: 15 min de bicicleta, puente de glúteo 3x12, prensa en recorrido parcial 3x12 y sentadilla isométrica en pared 3x30 s; tren superior completo.', N'Mantenimiento o déficit leve si hay sobrepeso, proteína 1,6 g/kg. Pescado azul 2-3 veces por semana.'),
    (N'H9', N'Rutina protectora de hombro', N'RESTRICCION', 85, N'3 días por semana sin press por encima de la cabeza: remo con banda 3x12, rotación externa con banda 3x15 y press inclinado con mancuernas en rango controlado; tren inferior completo.', N'Mantenimiento, proteína 1,6 g/kg y omega-3.'),
    (N'H10', N'Rutina de core y descarga lumbar', N'RESTRICCION', 85, N'3 días por semana sin carga axial pesada: curl-up, plancha lateral y bird-dog 3x8-10 con 10 s de sostén; puente de glúteo, prensa y 20 min de caminata.', N'Mantenimiento o control de peso, proteína 1,6 g/kg.'),
    (N'H11', N'Plan general de baja intensidad', N'RESTRICCION', 85, N'3 días por semana en máquinas guiadas, 2x12 a RPE 5, evitando los ejercicios que generen dolor mayor a 3 sobre 10 en la zona afectada; 20 min de cardio de bajo impacto.', N'Mantenimiento, proteína 1,4-1,6 g/kg.'),
    (N'H12', N'Masa · Principiante', N'ENTRENAMIENTO', 50, N'Full body A/B 3 días, 3x8-12 a RPE 6-7: sentadilla, press de banca, remo, peso muerto rumano y press militar, con 90 s de descanso.', N'Superávit de 300-400 kcal, proteína 1,6-2,2 g/kg y carbohidratos 4-6 g/kg en 4-5 comidas.'),
    (N'H13', N'Masa · Intermedio', N'ENTRENAMIENTO', 50, N'Torso-pierna 4 días, 3-4x6-12 a RPE 7-8. Torso: banca, remo, press inclinado, jalón y brazos. Pierna: sentadilla, peso muerto rumano, prensa, curl femoral y gemelos.', N'Superávit de 250-350 kcal, proteína 1,8-2,2 g/kg y carbohidratos 4-6 g/kg.'),
    (N'H14', N'Masa · Avanzado', N'ENTRENAMIENTO', 50, N'Empuje-tirón-pierna 6 días, 4x6-12 a RPE 8-9, con 12 a 20 series semanales por grupo muscular y drop set en la última serie.', N'Superávit de 200-300 kcal para limitar la ganancia de grasa, proteína 2,0-2,2 g/kg.'),
    (N'H15', N'Fuerza · Principiante', N'ENTRENAMIENTO', 50, N'Básicos 3 días, 3x5-8 a RPE 6-7: sentadilla, banca, peso muerto, press militar y remo, con progresión lineal de 2,5 kg por sesión mientras la técnica se mantenga.', N'Mantenimiento, proteína 1,6-2,0 g/kg y carbohidratos 3-5 g/kg; comida con carbohidratos 2-3 h antes de entrenar.'),
    (N'H16', N'Fuerza · Intermedio', N'ENTRENAMIENTO', 50, N'Torso-pierna 4 días: básicos 4x3-6 al 75-85 % de 1RM y accesorios 3x8-10.', N'Mantenimiento o superávit leve de 250 kcal, proteína 1,6-2,0 g/kg.'),
    (N'H17', N'Fuerza · Avanzado', N'ENTRENAMIENTO', 50, N'Periodización por bloques 4-5 días: acumulación 4 semanas al 75-80 % de 1RM, intensificación 3 semanas al 85-90 % y realización 1-2 semanas al 90-95 % con 1-5 repeticiones.', N'Superávit de 250 kcal, proteína 1,8-2,0 g/kg y carbohidratos 4-5 g/kg.'),
    (N'H18', N'Resistencia · Principiante', N'ENTRENAMIENTO', 50, N'Cardio continuo en zona 2, 3 días de 20-30 min a ritmo conversacional, aumentando el tiempo un 10 % por semana.', N'Mantenimiento, carbohidratos 3-5 g/kg y proteína 1,2-1,4 g/kg.'),
    (N'H19', N'Resistencia · Intermedio', N'ENTRENAMIENTO', 50, N'4 días: dos sesiones continuas de 40-50 min, una de intervalos (6x3 min fuertes con 2 min suaves) y una larga de 60 min.', N'Mantenimiento, carbohidratos 5-7 g/kg y proteína 1,4 g/kg; electrolitos en sesiones de más de 60 min.'),
    (N'H20', N'Resistencia · Avanzado', N'ENTRENAMIENTO', 50, N'Modelo polarizado 80/20, 5-6 días: dos sesiones de calidad (intervalos 5x5 min o tempo de 20-30 min) y una larga de 90 min o más.', N'Carbohidratos 6-10 g/kg según el volumen, proteína 1,6 g/kg y reposición de electrolitos.'),
    (N'H21', N'Peso · Principiante', N'ENTRENAMIENTO', 50, N'Full body 3 días, 3x10-12 a RPE 6, más 8.000 a 10.000 pasos diarios.', N'Déficit de 300 kcal, proteína 1,8-2,2 g/kg, carbohidratos 2-3 g/kg y 25 g de fibra al día.'),
    (N'H22', N'Peso · Intermedio', N'ENTRENAMIENTO', 50, N'Torso-pierna 4 días, 3x8-12, más 1 o 2 sesiones de HIIT de 10 rondas de 30 s fuertes y 60 s suaves.', N'Déficit de 400 kcal, proteína 1,8-2,2 g/kg y carbohidratos alrededor del entrenamiento.'),
    (N'H23', N'Peso · Avanzado', N'ENTRENAMIENTO', 50, N'Fuerza 4-5 días, 4x6-12, más cardio 2-3 días: un HIIT y una o dos sesiones de 40 min en zona 2.', N'Déficit de 500 kcal con un día semanal en mantenimiento, proteína 2,0-2,2 g/kg.'),
    (N'H24', N'Mantener · Principiante', N'ENTRENAMIENTO', 50, N'Full body 2-3 días, 2-3x10-12 a RPE 6, más 150 min semanales de cardio moderado.', N'Calorías de mantenimiento, proteína 1,2-1,6 g/kg y plato saludable.'),
    (N'H25', N'Mantener · Intermedio', N'ENTRENAMIENTO', 50, N'Mixto 3 días: dos de fuerza full body 3x8-12 y uno de cardio de 30-40 min.', N'Calorías de mantenimiento, proteína 1,4-1,6 g/kg.'),
    (N'H26', N'Mantener · Avanzado', N'ENTRENAMIENTO', 50, N'Mixto 4 días: dos de fuerza torso-pierna 3-4x6-10 a RPE 7-8, uno de intervalos y uno de zona 2 de 45 min.', N'Calorías de mantenimiento, proteína 1,6 g/kg y carbohidratos según la carga.');

/* Condiciones de riesgo (H1 a H3): el usuario debe consultar directamente
   con un profesional de la salud y el recorrido termina en esta hipotesis. */
UPDATE Hipotesis SET Aviso = N'Por tener una condición médica grave diagnosticada, debe consultar directamente con un profesional de la salud antes de realizar cualquier actividad física. La orientación de este resultado solo debe seguirse con su autorización.' WHERE Codigo = N'H1';
UPDATE Hipotesis SET Aviso = N'Por ser menor de 16 años, debe consultar directamente con un profesional de la salud antes de entrenar. Cualquier actividad debe realizarse solo con su autorización y bajo la supervisión de un adulto responsable o un entrenador.' WHERE Codigo = N'H2';
UPDATE Hipotesis SET Aviso = N'Por tener 60 años o más, debe consultar directamente con un profesional de la salud para que valore su condición antes de iniciar el plan. La orientación de este resultado solo debe seguirse con su autorización.' WHERE Codigo = N'H3';
GO

/* ------------------------------------------------------------ arbol de decision
   13 nodos de decision, 27 nodos terminales y 39 ramas. Se carga con
   codigos y se traduce a identificadores para que sea facil de leer. */
DECLARE @arbol TABLE (IdNodo INT, Codigo NVARCHAR(5), IdNodoPadre INT,
                      Hecho NVARCHAR(30), Pregunta NVARCHAR(10), Hipotesis NVARCHAR(5));

INSERT INTO @arbol (IdNodo, Codigo, IdNodoPadre, Hecho, Pregunta, Hipotesis) VALUES
    (1, N'N01', NULL, NULL, N'P1', NULL),
    (2, N'N02', 1, N'F_RIESGO_SI', N'P2', NULL),
    (3, N'N03', 2, N'F_COND_MEDICA', NULL, N'H1'),
    (4, N'N04', 2, N'F_COND_MENOR16', NULL, N'H2'),
    (5, N'N05', 2, N'F_COND_60MAS', NULL, N'H3'),
    (6, N'N06', 1, N'F_RIESGO_NO', N'P3', NULL),
    (7, N'N07', 6, N'F_DISP_MENOS1DIA', NULL, N'H4'),
    (8, N'N08', 6, N'F_DISP_MENOS15MIN', NULL, N'H5'),
    (9, N'N09', 6, N'F_DISP_SUFICIENTE', N'P4', NULL),
    (10, N'N10', 9, N'F_FATIGA_ALTA', N'P5', NULL),
    (11, N'N11', 10, N'F_SUENO_MENOS5', NULL, N'H6'),
    (12, N'N12', 10, N'F_SUENO_5MAS', NULL, N'H7'),
    (13, N'N13', 9, N'F_FATIGA_MOD', NULL, N'H7'),
    (14, N'N14', 9, N'F_FATIGA_BAJA', N'P6', NULL),
    (15, N'N15', 14, N'F_REST_SI', N'P7', NULL),
    (16, N'N16', 15, N'F_ZONA_RODILLA', NULL, N'H8'),
    (17, N'N17', 15, N'F_ZONA_HOMBRO', NULL, N'H9'),
    (18, N'N18', 15, N'F_ZONA_LUMBAR', NULL, N'H10'),
    (19, N'N19', 15, N'F_ZONA_OTRA', NULL, N'H11'),
    (20, N'N20', 14, N'F_REST_NO', N'P8', NULL),
    (21, N'N21', 20, N'F_OBJ_MASA', N'P9', NULL),
    (22, N'N22', 21, N'F_NIVEL_PRIN', NULL, N'H12'),
    (23, N'N23', 21, N'F_NIVEL_INTER', NULL, N'H13'),
    (24, N'N24', 21, N'F_NIVEL_AVAN', NULL, N'H14'),
    (25, N'N25', 20, N'F_OBJ_FUERZA', N'P9', NULL),
    (26, N'N26', 25, N'F_NIVEL_PRIN', NULL, N'H15'),
    (27, N'N27', 25, N'F_NIVEL_INTER', NULL, N'H16'),
    (28, N'N28', 25, N'F_NIVEL_AVAN', NULL, N'H17'),
    (29, N'N29', 20, N'F_OBJ_RESIS', N'P9', NULL),
    (30, N'N30', 29, N'F_NIVEL_PRIN', NULL, N'H18'),
    (31, N'N31', 29, N'F_NIVEL_INTER', NULL, N'H19'),
    (32, N'N32', 29, N'F_NIVEL_AVAN', NULL, N'H20'),
    (33, N'N33', 20, N'F_OBJ_PESO', N'P9', NULL),
    (34, N'N34', 33, N'F_NIVEL_PRIN', NULL, N'H21'),
    (35, N'N35', 33, N'F_NIVEL_INTER', NULL, N'H22'),
    (36, N'N36', 33, N'F_NIVEL_AVAN', NULL, N'H23'),
    (37, N'N37', 20, N'F_OBJ_MANTENER', N'P9', NULL),
    (38, N'N38', 37, N'F_NIVEL_PRIN', NULL, N'H24'),
    (39, N'N39', 37, N'F_NIVEL_INTER', NULL, N'H25'),
    (40, N'N40', 37, N'F_NIVEL_AVAN', NULL, N'H26');

INSERT INTO NodoDecision (IdNodo, Codigo, IdNodoPadre, IdHechoRama, IdPregunta, IdHipotesis)
SELECT a.IdNodo, a.Codigo, a.IdNodoPadre, f.IdHecho, p.IdPregunta, h.IdHipotesis
FROM @arbol a
LEFT JOIN Hecho f     ON f.Codigo = a.Hecho
LEFT JOIN Pregunta p  ON p.Codigo = a.Pregunta
LEFT JOIN Hipotesis h ON h.Codigo = a.Hipotesis
ORDER BY a.IdNodo;
GO

/* ------------------------------------------------------------ reglas
   R01 a R26 corresponden a H1 a H26. R27 cubre la fatiga alta con 5 o
   mas horas de sueno, que tambien concluye H7 (semana de descarga). */
INSERT INTO Regla (Codigo, IdHipotesis, IdNodoTerminal) VALUES
    (N'R01', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H1'), 3),
    (N'R02', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H2'), 4),
    (N'R03', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H3'), 5),
    (N'R04', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H4'), 7),
    (N'R05', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H5'), 8),
    (N'R06', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H6'), 11),
    (N'R07', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H7'), 13),
    (N'R08', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H8'), 16),
    (N'R09', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H9'), 17),
    (N'R10', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H10'), 18),
    (N'R11', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H11'), 19),
    (N'R12', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H12'), 22),
    (N'R13', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H13'), 23),
    (N'R14', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H14'), 24),
    (N'R15', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H15'), 26),
    (N'R16', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H16'), 27),
    (N'R17', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H17'), 28),
    (N'R18', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H18'), 30),
    (N'R19', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H19'), 31),
    (N'R20', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H20'), 32),
    (N'R21', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H21'), 34),
    (N'R22', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H22'), 35),
    (N'R23', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H23'), 36),
    (N'R24', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H24'), 38),
    (N'R25', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H25'), 39),
    (N'R26', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H26'), 40),
    (N'R27', (SELECT IdHipotesis FROM Hipotesis WHERE Codigo = N'H7'), 12);
GO

/* Condiciones de cada regla: se derivan del camino del arbol, de modo que
   la regla y el recorrido de la interfaz no pueden diferir. */
WITH Camino AS (
    SELECT r.IdRegla, n.IdNodoPadre, n.IdHechoRama, 0 AS Altura
    FROM Regla r
    JOIN NodoDecision n ON n.IdNodo = r.IdNodoTerminal
    UNION ALL
    SELECT c.IdRegla, p.IdNodoPadre, p.IdHechoRama, c.Altura + 1
    FROM Camino c
    JOIN NodoDecision p ON p.IdNodo = c.IdNodoPadre
    WHERE p.IdHechoRama IS NOT NULL
),
Ordenado AS (
    SELECT IdRegla, IdHechoRama AS IdHecho,
           COUNT(*) OVER (PARTITION BY IdRegla) - Altura AS Orden,
           COUNT(*) OVER (PARTITION BY IdRegla)          AS Total
    FROM Camino
)
INSERT INTO CondicionRegla (IdRegla, IdHecho, Orden, Peso)
SELECT IdRegla, IdHecho, Orden,
       CAST(ROUND(CAST(Orden AS DECIMAL(9,4)) / (Total * (Total + 1) / 2.0), 2) AS DECIMAL(4,2))
FROM Ordenado;
GO


/* =====================================================================
   SEGURIDAD E HISTORIAL
   Usuario     cuentas de acceso. La contrasena no se guarda: se guarda su
               derivacion PBKDF2-SHA256 (100 000 iteraciones) y una sal.
   Cuentas iniciales:
       admin    / Admin2026*     -> ADMINISTRADOR (ingeniero del conocimiento)
       usuario  / Usuario2026*   -> USUARIO (usuario final de prueba)
   ===================================================================== */
CREATE TABLE Usuario (
    IdUsuario      INT IDENTITY(1,1) CONSTRAINT PK_Usuario PRIMARY KEY,
    NombreUsuario  NVARCHAR(30)  NOT NULL CONSTRAINT UQ_Usuario_Nombre UNIQUE,
    NombreCompleto NVARCHAR(80)  NOT NULL,
    Rol            NVARCHAR(15)  NOT NULL CONSTRAINT CK_Usuario_Rol
                   CHECK (Rol IN (N'USUARIO', N'ADMINISTRADOR')),
    ClaveHash      VARBINARY(32) NOT NULL,
    Sal            VARBINARY(16) NOT NULL,
    Activo         BIT           NOT NULL CONSTRAINT DF_Usuario_Activo DEFAULT (1),
    FechaCreacion  DATETIME2(0)  NOT NULL CONSTRAINT DF_Usuario_Fecha DEFAULT (SYSUTCDATETIME()),
    UltimoAcceso   DATETIME2(0)  NULL
);

INSERT INTO Usuario (NombreUsuario, NombreCompleto, Rol, ClaveHash, Sal) VALUES
    (N'admin',   N'Ingeniero del conocimiento', N'ADMINISTRADOR',
     0xD59CCEBBE5F425E5F4E33D82C1BBCE554B9A0B8DE9B2D90681FA3A1C6A3076B7,
     0xA3F1C27E9B4D58016E2F7C3D9A0B4E51),
    (N'usuario', N'Usuario de prueba',          N'USUARIO',
     0x8F06F86679F54FC16F7B55C495BD97441B70480FB662C626456BCF9EB27827CC,
     0x5C9E01B7D24F3A68E1907BC35D2A4F86);
GO

/* Evaluacion: cada ejecucion del motor de inferencia de un usuario. */
CREATE TABLE Evaluacion (
    IdEvaluacion        INT IDENTITY(1,1) CONSTRAINT PK_Evaluacion PRIMARY KEY,
    IdUsuario           INT           NOT NULL CONSTRAINT FK_Evaluacion_Usuario REFERENCES Usuario (IdUsuario),
    Fecha               DATETIME2(0)  NOT NULL CONSTRAINT DF_Evaluacion_Fecha DEFAULT (SYSUTCDATETIME()),
    IdNodoTerminal      INT           NOT NULL CONSTRAINT FK_Evaluacion_Nodo REFERENCES NodoDecision (IdNodo),
    HipotesisEvaluadas  INT           NOT NULL,
    ConsultasEjecutadas INT           NOT NULL
);

CREATE INDEX IX_Evaluacion_Usuario ON Evaluacion (IdUsuario, Fecha DESC);

/* Camino recorrido en el arbol: en cada paso, el nodo de decision que se
   mostro y la respuesta (hecho) que eligio el usuario. */
CREATE TABLE EvaluacionRespuesta (
    IdEvaluacion INT NOT NULL CONSTRAINT FK_ER_Evaluacion REFERENCES Evaluacion (IdEvaluacion) ON DELETE CASCADE,
    Paso         INT NOT NULL,
    IdNodo       INT NOT NULL CONSTRAINT FK_ER_Nodo  REFERENCES NodoDecision (IdNodo),
    IdHecho      INT NOT NULL CONSTRAINT FK_ER_Hecho REFERENCES Hecho (IdHecho),
    CONSTRAINT PK_EvaluacionRespuesta PRIMARY KEY (IdEvaluacion, Paso)
);

/* Detalle de las 5 hipotesis con mayor puntuacion de cada evaluacion.
   Es lo que se muestra al dar clic en una hipotesis: la rutina, la dieta
   y la regla con sus descripciones. Se guarda una copia del texto para
   que el historial muestre lo que el usuario vio, aunque despues se
   edite la base de conocimiento. */
CREATE TABLE DetalleHipotesis (
    IdEvaluacion          INT            NOT NULL CONSTRAINT FK_DH_Evaluacion REFERENCES Evaluacion (IdEvaluacion) ON DELETE CASCADE,
    Posicion              TINYINT        NOT NULL CONSTRAINT CK_DH_Posicion CHECK (Posicion BETWEEN 1 AND 5),
    IdHipotesis           INT            NOT NULL CONSTRAINT FK_DH_Hipotesis REFERENCES Hipotesis (IdHipotesis),
    IdRegla               INT            NOT NULL CONSTRAINT FK_DH_Regla REFERENCES Regla (IdRegla),
    CodigoHipotesis       NVARCHAR(5)    NOT NULL,
    NombreHipotesis       NVARCHAR(80)   NOT NULL,
    Area                  NVARCHAR(20)   NOT NULL,
    Prioridad             INT            NOT NULL,
    Peso                  DECIMAL(5,1)   NOT NULL,   -- puntuacion normalizada (%) entre las 5
    Cobertura             DECIMAL(5,1)   NOT NULL,   -- % del peso de la regla cubierto por los hechos
    CondicionesCumplidas  DECIMAL(5,1)   NOT NULL,   -- % de condiciones de la regla que se cumplen
    ConclusionArbol       BIT            NOT NULL,   -- 1 = hipotesis a la que llego el recorrido
    Rutina                NVARCHAR(400)  NOT NULL,   -- descripcion de la rutina
    Dieta                 NVARCHAR(400)  NOT NULL,   -- descripcion de la dieta
    CodigoRegla           NVARCHAR(5)    NOT NULL,
    Regla                 NVARCHAR(800)  NOT NULL,   -- SI ... Y ... ENTONCES ...
    Explicacion           NVARCHAR(1500) NOT NULL,   -- descripcion de por que se obtuvo la regla
    Aviso                 NVARCHAR(400)  NULL,       -- aviso de condicion de riesgo (consultar a un profesional)
    CONSTRAINT PK_DetalleHipotesis PRIMARY KEY (IdEvaluacion, Posicion)
);
GO


/* =====================================================================
   VISTAS
   ===================================================================== */

/* Reglas en formato SI ... Y ... ENTONCES, construidas con CondicionRegla. */
CREATE OR ALTER VIEW vw_Reglas AS
SELECT  r.Codigo AS Regla,
        h.Codigo AS Hipotesis,
        N'SI el usuario ' +
        STRING_AGG(f.Enunciado, N' Y ') WITHIN GROUP (ORDER BY cr.Orden) +
        N' ENTONCES ' + h.Codigo + N' (' + h.Nombre + N')' AS Texto,
        h.Area,
        h.Prioridad,
        COUNT(*) AS Condiciones
FROM Regla r
JOIN Hipotesis h       ON h.IdHipotesis = r.IdHipotesis
JOIN CondicionRegla cr ON cr.IdRegla = r.IdRegla
JOIN Hecho f           ON f.IdHecho = cr.IdHecho
WHERE r.Activa = 1
GROUP BY r.IdRegla, r.Codigo, h.Codigo, h.Nombre, h.Area, h.Prioridad;
GO

/* Arbol de decision legible: cada nodo con la rama que lo origina. */
CREATE OR ALTER VIEW vw_ArbolDecision AS
SELECT  n.Codigo AS Nodo,
        padre.Codigo AS NodoPadre,
        f.Texto AS Rama,
        CASE WHEN n.IdPregunta IS NOT NULL THEN N'DECISION' ELSE N'TERMINAL' END AS Tipo,
        COALESCE(p.Codigo + N' ' + p.Texto, h.Codigo + N' ' + h.Nombre) AS Contenido
FROM NodoDecision n
LEFT JOIN NodoDecision padre ON padre.IdNodo = n.IdNodoPadre
LEFT JOIN Hecho f            ON f.IdHecho = n.IdHechoRama
LEFT JOIN Pregunta p         ON p.IdPregunta = n.IdPregunta
LEFT JOIN Hipotesis h        ON h.IdHipotesis = n.IdHipotesis;
GO

/* =====================================================================
   VERIFICACION
   ===================================================================== */
SELECT N'Preguntas' AS Tabla, COUNT(*) AS Registros FROM Pregunta
UNION ALL SELECT N'Hechos', COUNT(*) FROM Hecho
UNION ALL SELECT N'Hipotesis', COUNT(*) FROM Hipotesis
UNION ALL SELECT N'Nodos del arbol', COUNT(*) FROM NodoDecision
UNION ALL SELECT N'Nodos de decision', COUNT(*) FROM NodoDecision WHERE IdPregunta IS NOT NULL
UNION ALL SELECT N'Nodos terminales', COUNT(*) FROM NodoDecision WHERE IdHipotesis IS NOT NULL
UNION ALL SELECT N'Reglas', COUNT(*) FROM Regla
UNION ALL SELECT N'Condiciones', COUNT(*) FROM CondicionRegla
UNION ALL SELECT N'Usuarios', COUNT(*) FROM Usuario;

/* Coherencia del arbol: debe devolver 0 filas. Cada rama debe ser una
   respuesta de la pregunta del nodo padre, y cada pregunta de un nodo
   debe tener una rama hija por cada una de sus respuestas. */
SELECT N'Rama que no pertenece a la pregunta del padre' AS Problema, n.Codigo
FROM NodoDecision n
JOIN NodoDecision padre ON padre.IdNodo = n.IdNodoPadre
JOIN Hecho f ON f.IdHecho = n.IdHechoRama
WHERE f.IdPregunta <> padre.IdPregunta
UNION ALL
SELECT N'Respuesta sin rama en el arbol', d.Codigo + N' / ' + f.Codigo
FROM NodoDecision d
JOIN Hecho f ON f.IdPregunta = d.IdPregunta
WHERE NOT EXISTS (SELECT 1 FROM NodoDecision c WHERE c.IdNodoPadre = d.IdNodo AND c.IdHechoRama = f.IdHecho)
UNION ALL
SELECT N'Hipotesis sin nodo terminal', h.Codigo
FROM Hipotesis h
WHERE NOT EXISTS (SELECT 1 FROM NodoDecision n WHERE n.IdHipotesis = h.IdHipotesis);

SELECT Nodo, NodoPadre, Rama, Tipo, Contenido FROM vw_ArbolDecision ORDER BY Nodo;
SELECT Regla, Hipotesis, Texto, Prioridad FROM vw_Reglas ORDER BY Regla;
GO
