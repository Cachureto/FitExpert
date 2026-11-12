/* =====================================================================
   FitExpert - Recrear solo la parte transaccional (Amazon RDS)
   Recrea Usuario, Evaluacion, EvaluacionRespuesta y DetalleHipotesis sin
   tocar la base de conocimiento. Requiere haber ejecutado antes
   FitExpertDB_AWS.sql (usa NodoDecision, Hecho, Hipotesis y Regla).
   Se pierde el historial de evaluaciones.
   ===================================================================== */
USE FitExpertDB;
GO
DROP TABLE IF EXISTS DetalleHipotesis;
DROP TABLE IF EXISTS EvaluacionRespuesta;
DROP TABLE IF EXISTS Evaluacion;
DROP TABLE IF EXISTS Usuario;
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

SELECT IdUsuario, NombreUsuario, NombreCompleto, Rol, Activo FROM Usuario;
GO
