/* =====================================================================
   FitExpert - Migracion para Amazon RDS: inicio de sesion e historial
   Agrega las tablas Usuario y Evaluacion a la base FitExpertDB que ya
   existe, sin tocar la base de conocimiento. Si se ejecuta de nuevo,
   recrea ambas tablas (se pierde el historial).
   ===================================================================== */
USE FitExpertDB;
GO
DROP TABLE IF EXISTS Evaluacion;
DROP TABLE IF EXISTS Usuario;
GO

/* =====================================================================
   SEGURIDAD E HISTORIAL
   Usuario     cuentas de acceso. La contrasena no se guarda: se guarda su
               derivacion PBKDF2-SHA256 (100 000 iteraciones) y una sal.
   Evaluacion  historial de evaluaciones de cada usuario final.
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

CREATE TABLE Evaluacion (
    IdEvaluacion    INT IDENTITY(1,1) CONSTRAINT PK_Evaluacion PRIMARY KEY,
    IdUsuario       INT           NOT NULL CONSTRAINT FK_Evaluacion_Usuario
                    REFERENCES Usuario (IdUsuario),
    Fecha           DATETIME2(0)  NOT NULL CONSTRAINT DF_Evaluacion_Fecha DEFAULT (SYSUTCDATETIME()),
    CodigoHipotesis NVARCHAR(10)  NOT NULL,
    NombreHipotesis NVARCHAR(80)  NOT NULL,
    Peso            DECIMAL(5,1)  NOT NULL,
    Resultado       NVARCHAR(MAX) NOT NULL
);

CREATE INDEX IX_Evaluacion_Usuario ON Evaluacion (IdUsuario, Fecha DESC);

INSERT INTO Usuario (NombreUsuario, NombreCompleto, Rol, ClaveHash, Sal) VALUES
    (N'admin',   N'Ingeniero del conocimiento', N'ADMINISTRADOR',
     0xD59CCEBBE5F425E5F4E33D82C1BBCE554B9A0B8DE9B2D90681FA3A1C6A3076B7,
     0xA3F1C27E9B4D58016E2F7C3D9A0B4E51),
    (N'usuario', N'Usuario de prueba',          N'USUARIO',
     0x8F06F86679F54FC16F7B55C495BD97441B70480FB662C626456BCF9EB27827CC,
     0x5C9E01B7D24F3A68E1907BC35D2A4F86);
GO

SELECT IdUsuario, NombreUsuario, NombreCompleto, Rol, Activo FROM Usuario;
GO
