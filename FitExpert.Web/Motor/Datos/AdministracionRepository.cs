using System.Data;
using FitExpert.Motor.Modelo;
using Microsoft.Data.SqlClient;

namespace FitExpert.Motor.Datos;

/// <summary>Implementacion sobre SQL Server de la gestion de la base de conocimiento.</summary>
public sealed class AdministracionRepository : IAdministracionRepository
{
    private readonly ConexionBD _conexion;

    public AdministracionRepository(ConexionBD conexion) => _conexion = conexion;

    public IReadOnlyList<ReglaDerivada> ListarReglas()
    {
        const string sql = @"
            SELECT Hipotesis, Regla, Area, Prioridad, Condiciones
            FROM vw_Reglas
            ORDER BY Prioridad DESC, LEN(Hipotesis), Hipotesis;";

        return SqlAuxiliar.Leer(_conexion, sql, null, lector => new ReglaDerivada(
            lector.GetString(0),
            lector.GetString(1),
            lector.GetString(2),
            lector.GetInt32(3),
            lector.GetInt32(4)));
    }

    public IReadOnlyList<Hipotesis> ListarHipotesis()
    {
        const string sql = @"
            SELECT IdHipotesis, Codigo, Nombre, Area, Prioridad, Rutina, Dieta
            FROM Hipotesis
            ORDER BY IdHipotesis;";

        return SqlAuxiliar.Leer(_conexion, sql, null, lector => new Hipotesis
        {
            IdHipotesis = lector.GetInt32(0),
            Codigo      = lector.GetString(1),
            Nombre      = lector.GetString(2),
            Area        = lector.GetString(3),
            Prioridad   = lector.GetInt32(4),
            Rutina      = lector.GetString(5),
            Dieta       = lector.GetString(6)
        });
    }

    public void ActualizarHipotesis(Hipotesis hipotesis)
    {
        const string sql = @"
            UPDATE Hipotesis
            SET Nombre = @nombre, Prioridad = @prioridad, Rutina = @rutina, Dieta = @dieta
            WHERE IdHipotesis = @id;";

        SqlAuxiliar.Ejecutar(_conexion, sql, comando =>
        {
            comando.Parameters.Add("@nombre", SqlDbType.NVarChar, 80).Value = hipotesis.Nombre;
            comando.Parameters.Add("@prioridad", SqlDbType.Int).Value = hipotesis.Prioridad;
            comando.Parameters.Add("@rutina", SqlDbType.NVarChar, 400).Value = hipotesis.Rutina;
            comando.Parameters.Add("@dieta", SqlDbType.NVarChar, 400).Value = hipotesis.Dieta;
            comando.Parameters.Add("@id", SqlDbType.Int).Value = hipotesis.IdHipotesis;
        });
    }

    public IReadOnlyList<HechoCatalogo> ListarHechos()
    {
        const string sql = @"
            SELECT f.IdHecho, p.Codigo, f.Codigo, f.Texto, f.Enunciado
            FROM Hecho f
            JOIN Pregunta p ON p.IdPregunta = f.IdPregunta
            ORDER BY p.Orden, f.Orden;";

        return SqlAuxiliar.Leer(_conexion, sql, null, lector => new HechoCatalogo(
            lector.GetInt32(0),
            lector.GetString(1),
            lector.GetString(2),
            lector.GetString(3),
            lector.GetString(4)));
    }

    public IReadOnlyList<VinculoHechoHipotesis> ListarVinculos(int idHipotesis)
    {
        const string sql = @"
            SELECT f.IdHecho, f.IdPregunta, f.Codigo, f.Texto, f.Enunciado, f.Orden,
                   hh.IdHipotesis, hh.Peso, hh.Necesario
            FROM HechoHipotesis hh
            JOIN Hecho f ON f.IdHecho = hh.IdHecho
            WHERE hh.IdHipotesis = @idHipotesis
            ORDER BY hh.Necesario DESC, hh.Peso DESC;";

        return SqlAuxiliar.Leer(_conexion, sql,
            comando => comando.Parameters.Add("@idHipotesis", SqlDbType.Int).Value = idHipotesis,
            lector => new VinculoHechoHipotesis
            {
                Hecho = new Hecho
                {
                    IdHecho    = lector.GetInt32(0),
                    IdPregunta = lector.GetInt32(1),
                    Codigo     = lector.GetString(2),
                    Texto      = lector.GetString(3),
                    Enunciado  = lector.GetString(4),
                    Orden      = lector.GetInt32(5)
                },
                IdHipotesis = lector.GetInt32(6),
                Peso        = lector.GetDecimal(7),
                Necesario   = lector.GetBoolean(8)
            });
    }

    public void GuardarVinculo(int idHecho, int idHipotesis, decimal peso, bool necesario)
    {
        const string sql = @"
            UPDATE HechoHipotesis
            SET Peso = @peso, Necesario = @necesario
            WHERE IdHecho = @hecho AND IdHipotesis = @hipotesis;

            IF @@ROWCOUNT = 0
                INSERT INTO HechoHipotesis (IdHecho, IdHipotesis, Peso, Necesario)
                VALUES (@hecho, @hipotesis, @peso, @necesario);";

        SqlAuxiliar.Ejecutar(_conexion, sql, comando =>
        {
            var parametroPeso = comando.Parameters.Add("@peso", SqlDbType.Decimal);
            parametroPeso.Precision = 4;
            parametroPeso.Scale = 2;
            parametroPeso.Value = peso;

            comando.Parameters.Add("@necesario", SqlDbType.Bit).Value = necesario;
            comando.Parameters.Add("@hecho", SqlDbType.Int).Value = idHecho;
            comando.Parameters.Add("@hipotesis", SqlDbType.Int).Value = idHipotesis;
        });
    }

    public void EliminarVinculo(int idHecho, int idHipotesis)
    {
        const string sql =
            "DELETE FROM HechoHipotesis WHERE IdHecho = @hecho AND IdHipotesis = @hipotesis;";

        SqlAuxiliar.Ejecutar(_conexion, sql, comando =>
        {
            comando.Parameters.Add("@hecho", SqlDbType.Int).Value = idHecho;
            comando.Parameters.Add("@hipotesis", SqlDbType.Int).Value = idHipotesis;
        });
    }
}
