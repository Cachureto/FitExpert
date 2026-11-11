using Microsoft.Data.SqlClient;

namespace FitExpert.Motor.Datos;

/// <summary>Operaciones comunes de lectura y escritura para los repositorios.</summary>
internal static class SqlAuxiliar
{
    public static List<T> Leer<T>(
        ConexionBD conexionBd,
        string sql,
        Action<SqlCommand>? parametros,
        Func<SqlDataReader, T> mapear)
    {
        var filas = new List<T>();

        using var conexion = conexionBd.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        parametros?.Invoke(comando);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            filas.Add(mapear(lector));

        return filas;
    }

    public static int Ejecutar(ConexionBD conexionBd, string sql, Action<SqlCommand> parametros)
    {
        using var conexion = conexionBd.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        parametros(comando);
        return comando.ExecuteNonQuery();
    }

    public static object? Escalar(ConexionBD conexionBd, string sql, Action<SqlCommand> parametros)
    {
        using var conexion = conexionBd.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        parametros(comando);
        return comando.ExecuteScalar();
    }
}
