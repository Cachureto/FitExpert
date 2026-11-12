using System.Data;
using Microsoft.Data.SqlClient;

namespace FitExpert.Motor.Datos;

/// <summary>Detalle de una de las 5 hipotesis con mayor puntuacion. Tabla DetalleHipotesis.</summary>
public sealed record DetalleHipotesis(
    int Posicion,
    int IdHipotesis,
    int IdRegla,
    string Codigo,
    string Nombre,
    string Area,
    int Prioridad,
    decimal Peso,
    decimal Cobertura,
    decimal CondicionesCumplidas,
    bool ConclusionArbol,
    string Rutina,
    string Dieta,
    string CodigoRegla,
    string Regla,
    string Explicacion,
    string? Aviso);

/// <summary>Paso del recorrido que se guarda. Tabla EvaluacionRespuesta.</summary>
public sealed record RespuestaAGuardar(int Paso, int IdNodo, int IdHecho);

/// <summary>Paso del recorrido tal como se muestra al usuario.</summary>
public sealed record PasoGuardado(int Paso, string Nodo, string Pregunta, string Respuesta, string Enunciado);

/// <summary>Evaluacion completa reconstruida desde las tablas.</summary>
public sealed record EvaluacionGuardada(
    int IdEvaluacion,
    DateTime Fecha,
    string NodoTerminal,
    int HipotesisEvaluadas,
    int ConsultasEjecutadas,
    IReadOnlyList<PasoGuardado> Recorrido,
    IReadOnlyList<DetalleHipotesis> Hipotesis);

/// <summary>Evaluacion en la lista del historial (con su hipotesis principal).</summary>
public sealed record EvaluacionResumen(int IdEvaluacion, DateTime Fecha, string CodigoHipotesis, string NombreHipotesis, decimal Peso);

/// <summary>Cifras del panel del administrador.</summary>
public sealed record ResumenSistema(
    int UsuariosFinales,
    int UsuariosActivos,
    int Evaluaciones,
    int EvaluacionesSemana,
    IReadOnlyList<HipotesisFrecuente> MasFrecuentes);

public sealed record HipotesisFrecuente(string Codigo, string Nombre, int Veces);

/// <summary>Historial de evaluaciones: Evaluacion, EvaluacionRespuesta y DetalleHipotesis.</summary>
public sealed class EvaluacionRepository
{
    private readonly ConexionBD _conexion;

    public EvaluacionRepository(ConexionBD conexion) => _conexion = conexion;

    /// <summary>Guarda la evaluacion, su recorrido y el detalle de sus hipotesis en una sola transaccion.</summary>
    public int Guardar(
        int idUsuario,
        int idNodoTerminal,
        int hipotesisEvaluadas,
        int consultasEjecutadas,
        IReadOnlyList<RespuestaAGuardar> recorrido,
        IReadOnlyList<DetalleHipotesis> detalles)
    {
        using var conexion = _conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        int idEvaluacion;
        using (var comando = new SqlCommand(@"
            INSERT INTO Evaluacion (IdUsuario, IdNodoTerminal, HipotesisEvaluadas, ConsultasEjecutadas)
            OUTPUT INSERTED.IdEvaluacion
            VALUES (@usuario, @nodo, @evaluadas, @consultas);", conexion, transaccion))
        {
            comando.Parameters.Add("@usuario", SqlDbType.Int).Value = idUsuario;
            comando.Parameters.Add("@nodo", SqlDbType.Int).Value = idNodoTerminal;
            comando.Parameters.Add("@evaluadas", SqlDbType.Int).Value = hipotesisEvaluadas;
            comando.Parameters.Add("@consultas", SqlDbType.Int).Value = consultasEjecutadas;
            idEvaluacion = Convert.ToInt32(comando.ExecuteScalar());
        }

        foreach (var paso in recorrido)
        {
            using var comando = new SqlCommand(@"
                INSERT INTO EvaluacionRespuesta (IdEvaluacion, Paso, IdNodo, IdHecho)
                VALUES (@evaluacion, @paso, @nodo, @hecho);", conexion, transaccion);
            comando.Parameters.Add("@evaluacion", SqlDbType.Int).Value = idEvaluacion;
            comando.Parameters.Add("@paso", SqlDbType.Int).Value = paso.Paso;
            comando.Parameters.Add("@nodo", SqlDbType.Int).Value = paso.IdNodo;
            comando.Parameters.Add("@hecho", SqlDbType.Int).Value = paso.IdHecho;
            comando.ExecuteNonQuery();
        }

        foreach (var d in detalles)
        {
            using var comando = new SqlCommand(@"
                INSERT INTO DetalleHipotesis
                    (IdEvaluacion, Posicion, IdHipotesis, IdRegla, CodigoHipotesis, NombreHipotesis, Area, Prioridad,
                     Peso, Cobertura, CondicionesCumplidas, ConclusionArbol, Rutina, Dieta, CodigoRegla, Regla, Explicacion, Aviso)
                VALUES
                    (@evaluacion, @posicion, @hipotesis, @regla, @codigo, @nombre, @area, @prioridad,
                     @peso, @cobertura, @cumplidas, @conclusion, @rutina, @dieta, @codigoRegla, @textoRegla, @explicacion, @aviso);",
                conexion, transaccion);

            comando.Parameters.Add("@evaluacion", SqlDbType.Int).Value = idEvaluacion;
            comando.Parameters.Add("@posicion", SqlDbType.TinyInt).Value = (byte)d.Posicion;
            comando.Parameters.Add("@hipotesis", SqlDbType.Int).Value = d.IdHipotesis;
            comando.Parameters.Add("@regla", SqlDbType.Int).Value = d.IdRegla;
            comando.Parameters.Add("@codigo", SqlDbType.NVarChar, 5).Value = d.Codigo;
            comando.Parameters.Add("@nombre", SqlDbType.NVarChar, 80).Value = d.Nombre;
            comando.Parameters.Add("@area", SqlDbType.NVarChar, 20).Value = d.Area;
            comando.Parameters.Add("@prioridad", SqlDbType.Int).Value = d.Prioridad;
            Decimal(comando, "@peso", d.Peso);
            Decimal(comando, "@cobertura", d.Cobertura);
            Decimal(comando, "@cumplidas", d.CondicionesCumplidas);
            comando.Parameters.Add("@conclusion", SqlDbType.Bit).Value = d.ConclusionArbol;
            comando.Parameters.Add("@rutina", SqlDbType.NVarChar, 400).Value = d.Rutina;
            comando.Parameters.Add("@dieta", SqlDbType.NVarChar, 400).Value = d.Dieta;
            comando.Parameters.Add("@codigoRegla", SqlDbType.NVarChar, 5).Value = d.CodigoRegla;
            comando.Parameters.Add("@textoRegla", SqlDbType.NVarChar, 800).Value = Recortar(d.Regla, 800);
            comando.Parameters.Add("@explicacion", SqlDbType.NVarChar, 1500).Value = Recortar(d.Explicacion, 1500);
            comando.Parameters.Add("@aviso", SqlDbType.NVarChar, 400).Value = (object?)d.Aviso ?? DBNull.Value;
            comando.ExecuteNonQuery();
        }

        transaccion.Commit();
        return idEvaluacion;
    }

    public IReadOnlyList<EvaluacionResumen> ListarPorUsuario(int idUsuario, int maximo = 30)
    {
        const string sql = @"
            SELECT TOP (@maximo) e.IdEvaluacion, e.Fecha, d.CodigoHipotesis, d.NombreHipotesis, d.Peso
            FROM Evaluacion e
            JOIN DetalleHipotesis d ON d.IdEvaluacion = e.IdEvaluacion AND d.Posicion = 1
            WHERE e.IdUsuario = @usuario
            ORDER BY e.Fecha DESC, e.IdEvaluacion DESC;";

        return SqlAuxiliar.Leer(_conexion, sql,
            comando =>
            {
                comando.Parameters.Add("@maximo", SqlDbType.Int).Value = maximo;
                comando.Parameters.Add("@usuario", SqlDbType.Int).Value = idUsuario;
            },
            lector => new EvaluacionResumen(
                lector.GetInt32(0),
                DateTime.SpecifyKind(lector.GetDateTime(1), DateTimeKind.Utc),
                lector.GetString(2),
                lector.GetString(3),
                lector.GetDecimal(4)));
    }

    /// <summary>Evaluacion completa, solo si pertenece al usuario.</summary>
    public EvaluacionGuardada? Obtener(int idEvaluacion, int idUsuario)
    {
        const string sqlCabecera = @"
            SELECT e.IdEvaluacion, e.Fecha, n.Codigo, e.HipotesisEvaluadas, e.ConsultasEjecutadas
            FROM Evaluacion e
            JOIN NodoDecision n ON n.IdNodo = e.IdNodoTerminal
            WHERE e.IdEvaluacion = @id AND e.IdUsuario = @usuario;";

        var cabecera = SqlAuxiliar.Leer(_conexion, sqlCabecera, Parametros(idEvaluacion, idUsuario),
            lector => (Id: lector.GetInt32(0),
                       Fecha: DateTime.SpecifyKind(lector.GetDateTime(1), DateTimeKind.Utc),
                       Nodo: lector.GetString(2),
                       Evaluadas: lector.GetInt32(3),
                       Consultas: lector.GetInt32(4))).FirstOrDefault();

        if (cabecera.Id == 0) return null;

        const string sqlRecorrido = @"
            SELECT r.Paso, n.Codigo, p.Texto, f.Texto, f.Enunciado
            FROM EvaluacionRespuesta r
            JOIN NodoDecision n ON n.IdNodo = r.IdNodo
            JOIN Pregunta p     ON p.IdPregunta = n.IdPregunta
            JOIN Hecho f        ON f.IdHecho = r.IdHecho
            WHERE r.IdEvaluacion = @id
            ORDER BY r.Paso;";

        var recorrido = SqlAuxiliar.Leer(_conexion, sqlRecorrido,
            comando => comando.Parameters.Add("@id", SqlDbType.Int).Value = idEvaluacion,
            lector => new PasoGuardado(lector.GetInt32(0), lector.GetString(1), lector.GetString(2), lector.GetString(3), lector.GetString(4)));

        return new EvaluacionGuardada(cabecera.Id, cabecera.Fecha, cabecera.Nodo, cabecera.Evaluadas, cabecera.Consultas,
            recorrido, ListarDetalle(idEvaluacion, idUsuario, null));
    }

    /// <summary>Detalle de una hipotesis (rutina, dieta, regla y explicacion), solo si la evaluacion es del usuario.</summary>
    public DetalleHipotesis? ObtenerDetalle(int idEvaluacion, int idUsuario, int posicion) =>
        ListarDetalle(idEvaluacion, idUsuario, posicion).FirstOrDefault();

    private IReadOnlyList<DetalleHipotesis> ListarDetalle(int idEvaluacion, int idUsuario, int? posicion)
    {
        const string sql = @"
            SELECT d.Posicion, d.IdHipotesis, d.IdRegla, d.CodigoHipotesis, d.NombreHipotesis, d.Area, d.Prioridad,
                   d.Peso, d.Cobertura, d.CondicionesCumplidas, d.ConclusionArbol, d.Rutina, d.Dieta,
                   d.CodigoRegla, d.Regla, d.Explicacion, d.Aviso
            FROM DetalleHipotesis d
            JOIN Evaluacion e ON e.IdEvaluacion = d.IdEvaluacion
            WHERE d.IdEvaluacion = @id AND e.IdUsuario = @usuario
              AND (@posicion IS NULL OR d.Posicion = @posicion)
            ORDER BY d.Posicion;";

        return SqlAuxiliar.Leer(_conexion, sql,
            comando =>
            {
                Parametros(idEvaluacion, idUsuario)(comando);
                comando.Parameters.Add("@posicion", SqlDbType.Int).Value = (object?)posicion ?? DBNull.Value;
            },
            lector => new DetalleHipotesis(
                lector.GetByte(0),
                lector.GetInt32(1),
                lector.GetInt32(2),
                lector.GetString(3),
                lector.GetString(4),
                lector.GetString(5),
                lector.GetInt32(6),
                lector.GetDecimal(7),
                lector.GetDecimal(8),
                lector.GetDecimal(9),
                lector.GetBoolean(10),
                lector.GetString(11),
                lector.GetString(12),
                lector.GetString(13),
                lector.GetString(14),
                lector.GetString(15),
                lector.IsDBNull(16) ? null : lector.GetString(16)));
    }

    public ResumenSistema Resumen()
    {
        const string sqlTotales = @"
            SELECT
                (SELECT COUNT(*) FROM Usuario WHERE Rol = N'USUARIO'),
                (SELECT COUNT(*) FROM Usuario WHERE Rol = N'USUARIO' AND Activo = 1),
                (SELECT COUNT(*) FROM Evaluacion),
                (SELECT COUNT(*) FROM Evaluacion WHERE Fecha >= DATEADD(DAY, -7, SYSUTCDATETIME()));";

        const string sqlFrecuentes = @"
            SELECT TOP 5 h.Codigo, h.Nombre, COUNT(*) AS Veces
            FROM DetalleHipotesis d
            JOIN Hipotesis h ON h.IdHipotesis = d.IdHipotesis
            WHERE d.Posicion = 1
            GROUP BY h.IdHipotesis, h.Codigo, h.Nombre
            ORDER BY Veces DESC, h.IdHipotesis;";

        var totales = SqlAuxiliar.Leer(_conexion, sqlTotales, null,
            lector => (lector.GetInt32(0), lector.GetInt32(1), lector.GetInt32(2), lector.GetInt32(3))).First();

        var frecuentes = SqlAuxiliar.Leer(_conexion, sqlFrecuentes, null,
            lector => new HipotesisFrecuente(lector.GetString(0), lector.GetString(1), lector.GetInt32(2)));

        return new ResumenSistema(totales.Item1, totales.Item2, totales.Item3, totales.Item4, frecuentes);
    }

    private static Action<SqlCommand> Parametros(int idEvaluacion, int idUsuario) => comando =>
    {
        comando.Parameters.Add("@id", SqlDbType.Int).Value = idEvaluacion;
        comando.Parameters.Add("@usuario", SqlDbType.Int).Value = idUsuario;
    };

    private static void Decimal(SqlCommand comando, string nombre, decimal valor)
    {
        var parametro = comando.Parameters.Add(nombre, SqlDbType.Decimal);
        parametro.Precision = 5;
        parametro.Scale = 1;
        parametro.Value = Math.Round(valor, 1);
    }

    private static string Recortar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..(maximo - 1)] + "…";
}
