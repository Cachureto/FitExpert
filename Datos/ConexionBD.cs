using Microsoft.Data.SqlClient;

namespace FitExpert.Motor.Datos;

/// <summary>Fabrica de conexiones a FitExpertDB.</summary>
public sealed class ConexionBD
{
    private readonly string _cadena;

    public ConexionBD(string cadena) => _cadena = cadena;

    /// <summary>SQL Server Express local con autenticacion de Windows.</summary>
    public static ConexionBD PorDefecto() => new(
        "Server=AMC;Database=FitExpertDB;" +
        "Integrated Security=true;TrustServerCertificate=true;");

    public SqlConnection Abrir()
    {
        var conexion = new SqlConnection(_cadena);
        conexion.Open();
        return conexion;
    }
}
