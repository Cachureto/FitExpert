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
            SELECT Regla, Hipotesis, Texto, Area, Prioridad, Condiciones
            FROM vw_Reglas
            ORDER BY Regla;";

        return SqlAuxiliar.Leer(_conexion, sql, null, lector => new ReglaDerivada(
            lector.GetString(0),
            lector.GetString(1),
            lector.GetString(2),
            lector.GetString(3),
            lector.GetInt32(4),
            lector.GetInt32(5)));
    }

    public IReadOnlyList<Hipotesis> ListarHipotesis()
    {
        const string sql = @"
            SELECT IdHipotesis, Codigo, Nombre, Area, Prioridad, Rutina, Dieta, Aviso
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
            Dieta       = lector.GetString(6),
            Aviso       = lector.IsDBNull(7) ? null : lector.GetString(7)
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
}
