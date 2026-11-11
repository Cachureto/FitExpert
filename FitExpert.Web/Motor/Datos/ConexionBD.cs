using Microsoft.Data.SqlClient;

namespace FitExpert.Motor.Datos;

/// <summary>
/// Fabrica de conexiones a FitExpertDB en Amazon RDS for SQL Server.
/// Si hay un corte de red momentaneo, la conexion se reintenta unas
/// veces antes de fallar.
/// </summary>
public sealed class ConexionBD
{
    private const int Intentos = 3;
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(3);

    // Errores transitorios: base no disponible o corte de red.
    private static readonly HashSet<int> ErroresTransitorios = new() { -2, 53, 4060, 40613, 40197, 40501 };

    private readonly string _cadena;

    public ConexionBD(string cadena) => _cadena = cadena;

    public SqlConnection Abrir()
    {
        for (var intento = 1; ; intento++)
        {
            var conexion = new SqlConnection(_cadena);
            try
            {
                conexion.Open();
                return conexion;
            }
            catch (SqlException error) when (intento < Intentos && ErroresTransitorios.Contains(error.Number))
            {
                conexion.Dispose();
                Thread.Sleep(Espera);
            }
            catch
            {
                conexion.Dispose();
                throw;
            }
        }
    }
}
