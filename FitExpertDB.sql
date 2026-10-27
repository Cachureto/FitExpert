/* =====================================================================
   FitExpert - Base de conocimiento (SQL Server)
   Modelo reducido a cuatro tablas:
       Pregunta        preguntas que el sistema formula al usuario
       Hecho           respuestas posibles de cada pregunta
       Hipotesis       conclusiones del sistema (H1 a H26)
       HechoHipotesis  relaciona un hecho con una o mas hipotesis
   Las reglas SI ... Y ... ENTONCES no se almacenan: se derivan de
   HechoHipotesis y se consultan en la vista vw_Reglas.
   ===================================================================== */

USE master;
GO
IF DB_ID(N'FitExpertDB') IS NOT NULL
BEGIN
    ALTER DATABASE FitExpertDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE FitExpertDB;
END
GO
CREATE DATABASE FitExpertDB;
GO
USE FitExpertDB;
GO

CREATE TABLE Pregunta (
    IdPregunta  INT IDENTITY(1,1) CONSTRAINT PK_Pregunta PRIMARY KEY,
    Codigo      NVARCHAR(10)  NOT NULL CONSTRAINT UQ_Pregunta_Codigo UNIQUE,
    Texto       NVARCHAR(200) NOT NULL,
    Orden       INT           NOT NULL,
    Obligatoria BIT           NOT NULL CONSTRAINT DF_Pregunta_Obligatoria DEFAULT (1)
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
    Dieta         NVARCHAR(400) NOT NULL
);

CREATE TABLE HechoHipotesis (
    IdHecho     INT NOT NULL CONSTRAINT FK_HH_Hecho     REFERENCES Hecho(IdHecho),
    IdHipotesis INT NOT NULL CONSTRAINT FK_HH_Hipotesis REFERENCES Hipotesis(IdHipotesis),
    Peso        DECIMAL(4,2) NOT NULL CONSTRAINT CK_HH_Peso CHECK (Peso > 0 AND Peso <= 1),
    Necesario   BIT NOT NULL CONSTRAINT DF_HH_Necesario DEFAULT (0),
    CONSTRAINT PK_HechoHipotesis PRIMARY KEY (IdHecho, IdHipotesis)
);
GO

INSERT INTO Pregunta (Codigo, Texto, Orden) VALUES
    (N'P1', N'¿Tiene alguna condición médica grave diagnosticada?', 1),
    (N'P2', N'¿En qué rango de edad se encuentra?', 2),
    (N'P3', N'¿Cuántos días por semana puede entrenar?', 3),
    (N'P4', N'¿Cuánto tiempo tiene disponible por sesión?', 4),
    (N'P5', N'¿Cómo califica su nivel de fatiga actual?', 5),
    (N'P6', N'¿Cuántas horas duerme en promedio por noche?', 6),
    (N'P7', N'¿Tiene alguna restricción física o lesión?', 7),
    (N'P8', N'¿Cuál es su objetivo principal?', 8),
    (N'P9', N'¿Cuál es su nivel de experiencia entrenando?', 9);

INSERT INTO Hecho (IdPregunta, Codigo, Texto, Enunciado, Orden) VALUES
    (1, N'F_MED_SI', N'Sí, tengo una condición médica diagnosticada', N'tiene una condición médica grave', 1),
    (1, N'F_MED_NO', N'No tengo ninguna condición médica', N'no tiene condición médica', 2),
    (2, N'F_EDAD_MENOR16', N'Menos de 16 años', N'es menor de 16 años', 1),
    (2, N'F_EDAD_ADULTO', N'Entre 16 y 59 años', N'tiene entre 16 y 59 años', 2),
    (2, N'F_EDAD_60MAS', N'60 años o más', N'tiene 60 años o más', 3),
    (3, N'F_DIAS_0', N'Menos de 1 día', N'dispone de menos de 1 día por semana', 1),
    (3, N'F_DIAS_1A2', N'1 o 2 días', N'dispone de 1 a 2 días por semana', 2),
    (3, N'F_DIAS_3A4', N'3 o 4 días', N'dispone de 3 a 4 días por semana', 3),
    (3, N'F_DIAS_5MAS', N'5 días o más', N'dispone de 5 días o más por semana', 4),
    (4, N'F_TIEMPO_MENOS15', N'Menos de 15 minutos', N'dispone de menos de 15 minutos por sesión', 1),
    (4, N'F_TIEMPO_15A29', N'Entre 15 y 29 minutos', N'dispone de 15 a 29 minutos por sesión', 2),
    (4, N'F_TIEMPO_30A60', N'Entre 30 y 60 minutos', N'dispone de 30 a 60 minutos por sesión', 3),
    (4, N'F_TIEMPO_MAS60', N'Más de 60 minutos', N'dispone de más de 60 minutos por sesión', 4),
    (5, N'F_FATIGA_BAJA', N'Baja, me siento con energía', N'tiene fatiga baja', 1),
    (5, N'F_FATIGA_MOD', N'Moderada', N'tiene fatiga moderada', 2),
    (5, N'F_FATIGA_ALTA', N'Alta, me siento agotado', N'tiene fatiga alta', 3),
    (6, N'F_SUENO_MENOS5', N'Menos de 5 horas', N'duerme menos de 5 horas', 1),
    (6, N'F_SUENO_5A6', N'Entre 5 y 6 horas', N'duerme entre 5 y 6 horas', 2),
    (6, N'F_SUENO_7MAS', N'7 horas o más', N'duerme 7 horas o más', 3),
    (7, N'F_REST_NO', N'No tengo restricciones', N'no tiene restricciones físicas', 1),
    (7, N'F_REST_RODILLA', N'Sí, en la rodilla', N'tiene una restricción en la rodilla', 2),
    (7, N'F_REST_HOMBRO', N'Sí, en el hombro', N'tiene una restricción en el hombro', 3),
    (7, N'F_REST_LUMBAR', N'Sí, en la zona lumbar', N'tiene una restricción lumbar', 4),
    (7, N'F_REST_OTRA', N'Sí, en otra zona', N'tiene una restricción en otra zona', 5),
    (8, N'F_OBJ_MASA', N'Ganar masa muscular', N'su objetivo es ganar masa muscular', 1),
    (8, N'F_OBJ_FUERZA', N'Aumentar fuerza', N'su objetivo es aumentar fuerza', 2),
    (8, N'F_OBJ_RESIS', N'Mejorar resistencia', N'su objetivo es mejorar resistencia', 3),
    (8, N'F_OBJ_PESO', N'Perder peso', N'su objetivo es perder peso', 4),
    (8, N'F_OBJ_MANTENER', N'Mantener la condición física', N'su objetivo es mantener la condición física', 5),
    (9, N'F_NIVEL_PRIN', N'Principiante', N'su nivel es principiante', 1),
    (9, N'F_NIVEL_INTER', N'Intermedio', N'su nivel es intermedio', 2),
    (9, N'F_NIVEL_AVAN', N'Avanzado', N'su nivel es avanzado', 3);

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

INSERT INTO HechoHipotesis (IdHecho, IdHipotesis, Peso, Necesario) VALUES
    (1, 1, 1.00, 1),
    (3, 2, 1.00, 1),
    (5, 3, 0.80, 1),
    (2, 3, 0.20, 0),
    (6, 4, 1.00, 1),
    (10, 5, 0.80, 1),
    (7, 5, 0.20, 0),
    (16, 6, 0.60, 1),
    (17, 6, 0.40, 1),
    (15, 7, 0.70, 1),
    (18, 7, 0.30, 0),
    (21, 8, 1.00, 1),
    (22, 9, 1.00, 1),
    (23, 10, 1.00, 1),
    (24, 11, 1.00, 1),
    (25, 12, 0.45, 1),
    (30, 12, 0.35, 1),
    (8, 12, 0.12, 0),
    (12, 12, 0.08, 0),
    (20, 12, 0.10, 0),
    (25, 13, 0.45, 1),
    (31, 13, 0.35, 1),
    (8, 13, 0.12, 0),
    (12, 13, 0.08, 0),
    (20, 13, 0.10, 0),
    (25, 14, 0.45, 1),
    (32, 14, 0.35, 1),
    (9, 14, 0.12, 0),
    (13, 14, 0.08, 0),
    (20, 14, 0.10, 0),
    (26, 15, 0.45, 1),
    (30, 15, 0.35, 1),
    (8, 15, 0.12, 0),
    (12, 15, 0.08, 0),
    (20, 15, 0.10, 0),
    (26, 16, 0.45, 1),
    (31, 16, 0.35, 1),
    (8, 16, 0.12, 0),
    (12, 16, 0.08, 0),
    (20, 16, 0.10, 0),
    (26, 17, 0.45, 1),
    (32, 17, 0.35, 1),
    (9, 17, 0.12, 0),
    (13, 17, 0.08, 0),
    (20, 17, 0.10, 0),
    (27, 18, 0.45, 1),
    (30, 18, 0.35, 1),
    (8, 18, 0.12, 0),
    (12, 18, 0.08, 0),
    (20, 18, 0.10, 0),
    (27, 19, 0.45, 1),
    (31, 19, 0.35, 1),
    (8, 19, 0.12, 0),
    (12, 19, 0.08, 0),
    (20, 19, 0.10, 0),
    (27, 20, 0.45, 1),
    (32, 20, 0.35, 1),
    (9, 20, 0.12, 0),
    (13, 20, 0.08, 0),
    (20, 20, 0.10, 0),
    (28, 21, 0.45, 1),
    (30, 21, 0.35, 1),
    (8, 21, 0.12, 0),
    (12, 21, 0.08, 0),
    (20, 21, 0.10, 0),
    (28, 22, 0.45, 1),
    (31, 22, 0.35, 1),
    (8, 22, 0.12, 0),
    (12, 22, 0.08, 0),
    (20, 22, 0.10, 0),
    (28, 23, 0.45, 1),
    (32, 23, 0.35, 1),
    (9, 23, 0.12, 0),
    (13, 23, 0.08, 0),
    (20, 23, 0.10, 0),
    (29, 24, 0.45, 1),
    (30, 24, 0.35, 1),
    (7, 24, 0.12, 0),
    (12, 24, 0.08, 0),
    (20, 24, 0.10, 0),
    (29, 25, 0.45, 1),
    (31, 25, 0.35, 1),
    (8, 25, 0.12, 0),
    (12, 25, 0.08, 0),
    (20, 25, 0.10, 0),
    (29, 26, 0.45, 1),
    (32, 26, 0.35, 1),
    (9, 26, 0.12, 0),
    (13, 26, 0.08, 0),
    (20, 26, 0.10, 0);
GO


/* =====================================================================
   VISTA. Reglas en formato SI ... Y ... ENTONCES
   Se construyen con los hechos necesarios de cada hipotesis.
   ===================================================================== */
CREATE OR ALTER VIEW vw_Reglas AS
SELECT  h.Codigo AS Hipotesis,
        N'SI el usuario ' +
        STRING_AGG(f.Enunciado, N' Y ') WITHIN GROUP (ORDER BY f.IdHecho) +
        N' ENTONCES ' + h.Codigo + N' (' + h.Nombre + N')' AS Regla,
        h.Area,
        h.Prioridad,
        COUNT(*) AS Condiciones
FROM Hipotesis h
JOIN HechoHipotesis hh ON hh.IdHipotesis = h.IdHipotesis AND hh.Necesario = 1
JOIN Hecho f           ON f.IdHecho = hh.IdHecho
GROUP BY h.IdHipotesis, h.Codigo, h.Nombre, h.Area, h.Prioridad;
GO

/* Vista de apoyo: todos los hechos que respaldan cada hipotesis. */
CREATE OR ALTER VIEW vw_HechosPorHipotesis AS
SELECT  h.Codigo AS Hipotesis, h.Nombre, h.Area,
        p.Codigo AS Pregunta, f.Codigo AS Hecho, f.Enunciado,
        hh.Peso, hh.Necesario
FROM Hipotesis h
JOIN HechoHipotesis hh ON hh.IdHipotesis = h.IdHipotesis
JOIN Hecho f           ON f.IdHecho = hh.IdHecho
JOIN Pregunta p        ON p.IdPregunta = f.IdPregunta;
GO

/* =====================================================================
   VERIFICACION
   ===================================================================== */
SELECT N'Preguntas' AS Tabla, COUNT(*) AS Registros FROM Pregunta
UNION ALL SELECT N'Hechos', COUNT(*) FROM Hecho
UNION ALL SELECT N'Hipotesis', COUNT(*) FROM Hipotesis
UNION ALL SELECT N'Asociaciones hecho-hipotesis', COUNT(*) FROM HechoHipotesis;

SELECT Hipotesis, Regla, Prioridad FROM vw_Reglas ORDER BY Prioridad DESC, Hipotesis;
GO
