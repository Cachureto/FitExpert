using System.Data;
using Microsoft.Data.SqlClient;

namespace FitExpert.Motor.Datos;

/// <summary>Evaluacion guardada en el historial de un usuario.</summary>
public sealed record EvaluacionResumen(int IdEvaluacion, DateTime Fecha, string CodigoHipotesis, string NombreHipotesis, decimal Peso);

/// <summary>Cifras del panel del administrador.</summary>
public sealed record ResumenSistema(
    int UsuariosFinales,
    int UsuariosActivos,
    int Evaluaciones,
    int EvaluacionesSemana,
    IReadOnlyList<HipotesisFrecuente> MasFrecuentes);

public sealed record HipotesisFrecuente(string Codigo, string Nombre, int Veces);

/// <summary>Historial de evaluaciones (tabla Evaluacion).</summary>
public sealed class EvaluacionRepository
{
    private readonly ConexionBD _conexion;

    public EvaluacionRepository(ConexionBD conexion) => _conexion = conexion;

    public int Guardar(int idUsuario, string codigo, string nombre, decimal peso, string resultadoJson)
    {
        const string sql = @"
            INSERT INTO Evaluacion (IdUsuario, CodigoHipotesis, NombreHipotesis, Peso, Resultado)
            OUTPUT INSERTED.IdEvaluacion
            VALUES (@usuario, @codigo, @nombre, @peso, @resultado);";

        var id = SqlAuxiliar.Escalar(_conexion, sql, comando =>
        {
            comando.Parameters.Add("@usuario", SqlDbType.Int).Value = idUsuario;
            comando.Parameters.Add("@codigo", SqlDbType.NVarChar, 10).Value = codigo;
            comando.Parameters.Add("@nombre", SqlDbType.NVarChar, 80).Value = nombre;
            var parametroPeso = comando.Parameters.Add("@peso", SqlDbType.Decimal);
            parametroPeso.Precision = 5;
            parametroPeso.Scale = 1;
            parametroPeso.Value = peso;
            comando.Parameters.Add("@resultado", SqlDbType.NVarChar, -1).Value = resultadoJson;
        });

        return Convert.ToInt32(id);
    }

    public IReadOnlyList<EvaluacionResumen> ListarPorUsuario(int idUsuario, int maximo = 30)
    {
        const string sql = @"
            SELECT TOP (@maximo) IdEvaluacion, Fecha, CodigoHipotesis, NombreHipotesis, Peso
            FROM Evaluacion
            WHERE IdUsuario = @usuario
            ORDER BY Fecha DESC, IdEvaluacion DESC;";

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

    /// <summary>Resultado completo en JSON, solo si la evaluacion pertenece al usuario.</summary>
    public string? ObtenerResultado(int idEvaluacion, int idUsuario)
    {
        const string sql =
            "SELECT Resultado FROM Evaluacion WHERE IdEvaluacion = @id AND IdUsuario = @usuario;";

        var filas = SqlAuxiliar.Leer(_conexion, sql,
            comando =>
            {
                comando.Parameters.Add("@id", SqlDbType.Int).Value = idEvaluacion;
                comando.Parameters.Add("@usuario", SqlDbType.Int).Value = idUsuario;
            },
            lector => lector.GetString(0));

        return filas.FirstOrDefault();
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
            SELECT TOP 5 CodigoHipotesis, NombreHipotesis, COUNT(*) AS Veces
            FROM Evaluacion
            GROUP BY CodigoHipotesis, NombreHipotesis
            ORDER BY Veces DESC, CodigoHipotesis;";

        var totales = SqlAuxiliar.Leer(_conexion, sqlTotales, null,
            lector => (lector.GetInt32(0), lector.GetInt32(1), lector.GetInt32(2), lector.GetInt32(3))).First();

        var frecuentes = SqlAuxiliar.Leer(_conexion, sqlFrecuentes, null,
            lector => new HipotesisFrecuente(lector.GetString(0), lector.GetString(1), lector.GetInt32(2)));

        return new ResumenSistema(totales.Item1, totales.Item2, totales.Item3, totales.Item4, frecuentes);
    }
}
