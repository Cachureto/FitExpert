using System.Data;
using FitExpert.Motor.Modelo;
using Microsoft.Data.SqlClient;

namespace FitExpert.Motor.Datos;

/// <summary>Implementacion sobre SQL Server de la tabla Usuario.</summary>
public sealed class UsuarioRepository : IUsuarioRepository
{
    private const string RolAdministrador = "ADMINISTRADOR";
    private const string RolUsuarioFinal = "USUARIO";

    private const string Columnas =
        "IdUsuario, NombreUsuario, NombreCompleto, Rol, Activo, FechaCreacion, UltimoAcceso";

    private readonly ConexionBD _conexion;

    public UsuarioRepository(ConexionBD conexion) => _conexion = conexion;

    public CredencialAlmacenada? ObtenerCredencial(string nombreUsuario)
    {
        const string sql =
            "SELECT " + Columnas + ", ClaveHash, Sal FROM Usuario WHERE NombreUsuario = @nombre;";

        var filas = SqlAuxiliar.Leer(_conexion, sql,
            comando => comando.Parameters.Add("@nombre", SqlDbType.NVarChar, 30).Value = nombreUsuario,
            lector => new CredencialAlmacenada(
                Mapear(lector),
                (byte[])lector["ClaveHash"],
                (byte[])lector["Sal"]));

        return filas.FirstOrDefault();
    }

    public bool Existe(string nombreUsuario)
    {
        const string sql = "SELECT COUNT(1) FROM Usuario WHERE NombreUsuario = @nombre;";

        var total = SqlAuxiliar.Escalar(_conexion, sql,
            comando => comando.Parameters.Add("@nombre", SqlDbType.NVarChar, 30).Value = nombreUsuario);

        return Convert.ToInt32(total) > 0;
    }

    public int Crear(Usuario usuario, byte[] claveHash, byte[] sal)
    {
        const string sql = @"
            INSERT INTO Usuario (NombreUsuario, NombreCompleto, Rol, ClaveHash, Sal, Activo)
            OUTPUT INSERTED.IdUsuario
            VALUES (@nombre, @completo, @rol, @hash, @sal, @activo);";

        var id = SqlAuxiliar.Escalar(_conexion, sql, comando =>
        {
            comando.Parameters.Add("@nombre", SqlDbType.NVarChar, 30).Value = usuario.NombreUsuario;
            comando.Parameters.Add("@completo", SqlDbType.NVarChar, 80).Value = usuario.NombreCompleto;
            comando.Parameters.Add("@rol", SqlDbType.NVarChar, 15).Value = CodigoRol(usuario.Rol);
            comando.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = claveHash;
            comando.Parameters.Add("@sal", SqlDbType.VarBinary, 16).Value = sal;
            comando.Parameters.Add("@activo", SqlDbType.Bit).Value = usuario.Activo;
        });

        return Convert.ToInt32(id);
    }

    public IReadOnlyList<Usuario> Listar()
    {
        const string sql =
            "SELECT " + Columnas + " FROM Usuario ORDER BY Rol, NombreUsuario;";

        return SqlAuxiliar.Leer(_conexion, sql, null, Mapear);
    }

    public void CambiarEstado(int idUsuario, bool activo)
    {
        const string sql = "UPDATE Usuario SET Activo = @activo WHERE IdUsuario = @id;";

        SqlAuxiliar.Ejecutar(_conexion, sql, comando =>
        {
            comando.Parameters.Add("@activo", SqlDbType.Bit).Value = activo;
            comando.Parameters.Add("@id", SqlDbType.Int).Value = idUsuario;
        });
    }

    public void RegistrarAcceso(int idUsuario)
    {
        const string sql = "UPDATE Usuario SET UltimoAcceso = SYSUTCDATETIME() WHERE IdUsuario = @id;";

        SqlAuxiliar.Ejecutar(_conexion, sql,
            comando => comando.Parameters.Add("@id", SqlDbType.Int).Value = idUsuario);
    }

    private static Usuario Mapear(SqlDataReader lector) => new()
    {
        IdUsuario      = lector.GetInt32(0),
        NombreUsuario  = lector.GetString(1),
        NombreCompleto = lector.GetString(2),
        Rol            = lector.GetString(3) == RolAdministrador
                             ? RolUsuario.Administrador
                             : RolUsuario.UsuarioFinal,
        Activo         = lector.GetBoolean(4),
        FechaCreacion  = lector.GetDateTime(5),
        UltimoAcceso   = lector.IsDBNull(6) ? (DateTime?)null : lector.GetDateTime(6)
    };

    private static string CodigoRol(RolUsuario rol) =>
        rol == RolUsuario.Administrador ? RolAdministrador : RolUsuarioFinal;
}
