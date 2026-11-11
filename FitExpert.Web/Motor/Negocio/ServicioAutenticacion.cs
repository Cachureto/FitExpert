using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Inicio de sesion. Cada perfil entra por su propia opcion: una cuenta de
/// usuario final no puede ingresar como administrador ni al contrario.
/// </summary>
public sealed class ServicioAutenticacion
{
    private const string CredencialesInvalidas = "Usuario o contraseña incorrectos.";

    private readonly IUsuarioRepository _usuarios;

    public ServicioAutenticacion(IUsuarioRepository usuarios) => _usuarios = usuarios;

    public ResultadoAutenticacion IniciarSesion(string nombreUsuario, string clave, RolUsuario perfil)
    {
        nombreUsuario = (nombreUsuario ?? "").Trim();

        if (nombreUsuario.Length == 0 || string.IsNullOrEmpty(clave))
            return ResultadoAutenticacion.Falla("Ingrese el usuario y la contraseña.");

        var credencial = _usuarios.ObtenerCredencial(nombreUsuario);

        // La contrasena se verifica antes de revelar cualquier dato de la cuenta.
        if (credencial is null || !HasherClaves.Verificar(clave, credencial.ClaveHash, credencial.Sal))
            return ResultadoAutenticacion.Falla(CredencialesInvalidas);

        var usuario = credencial.Usuario;

        if (!usuario.Activo)
            return ResultadoAutenticacion.Falla("La cuenta está desactivada. Comuníquese con el administrador.");

        if (usuario.Rol != perfil)
            return ResultadoAutenticacion.Falla(perfil == RolUsuario.Administrador
                ? "Esta cuenta no tiene permisos de administrador. Ingrese por la opción Usuario final."
                : "Las cuentas de administrador deben ingresar por la opción Administrador.");

        _usuarios.RegistrarAcceso(usuario.IdUsuario);
        return ResultadoAutenticacion.Ok(usuario);
    }
}
