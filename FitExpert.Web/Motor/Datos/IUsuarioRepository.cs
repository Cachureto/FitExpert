using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Datos;

/// <summary>Cuenta con la derivacion de su contrasena y la sal usada.</summary>
public sealed record CredencialAlmacenada(Usuario Usuario, byte[] ClaveHash, byte[] Sal);

/// <summary>Acceso a la tabla Usuario.</summary>
public interface IUsuarioRepository
{
    CredencialAlmacenada? ObtenerCredencial(string nombreUsuario);
    bool Existe(string nombreUsuario);
    int Crear(Usuario usuario, byte[] claveHash, byte[] sal);
    IReadOnlyList<Usuario> Listar();
    void CambiarEstado(int idUsuario, bool activo);
    void RegistrarAcceso(int idUsuario);
}
