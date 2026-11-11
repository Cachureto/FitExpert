using System.Text.RegularExpressions;
using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>Registro de cuentas y gestion de usuarios por parte del administrador.</summary>
public sealed class ServicioUsuarios
{
    private static readonly Regex FormatoUsuario = new("^[a-z0-9._]{4,30}$");

    private readonly IUsuarioRepository _usuarios;

    public ServicioUsuarios(IUsuarioRepository usuarios) => _usuarios = usuarios;

    public IReadOnlyList<Usuario> Listar() => _usuarios.Listar();

    public ResultadoOperacion Registrar(
        string nombreCompleto,
        string nombreUsuario,
        string clave,
        string confirmacion,
        RolUsuario rol)
    {
        nombreCompleto = (nombreCompleto ?? "").Trim();
        nombreUsuario = (nombreUsuario ?? "").Trim().ToLowerInvariant();

        if (nombreCompleto.Length < 3 || nombreCompleto.Length > 80)
            return ResultadoOperacion.Falla("Escriba el nombre completo (entre 3 y 80 caracteres).");

        if (!FormatoUsuario.IsMatch(nombreUsuario))
            return ResultadoOperacion.Falla(
                "El usuario debe tener entre 4 y 30 caracteres: letras, números, punto o guion bajo, sin espacios.");

        var errorClave = ValidarClave(clave);
        if (errorClave is not null)
            return ResultadoOperacion.Falla(errorClave);

        if (clave != confirmacion)
            return ResultadoOperacion.Falla("Las contraseñas no coinciden.");

        if (_usuarios.Existe(nombreUsuario))
            return ResultadoOperacion.Falla("El nombre de usuario ya está registrado.");

        var (hash, sal) = HasherClaves.Generar(clave);

        _usuarios.Crear(new Usuario
        {
            NombreUsuario  = nombreUsuario,
            NombreCompleto = nombreCompleto,
            Rol            = rol,
            Activo         = true
        }, hash, sal);

        return ResultadoOperacion.Ok($"La cuenta «{nombreUsuario}» se creó correctamente.");
    }

    public ResultadoOperacion CambiarEstado(int idUsuario, bool activo, Usuario solicitante)
    {
        if (!activo && idUsuario == solicitante.IdUsuario)
            return ResultadoOperacion.Falla("No puede desactivar su propia cuenta.");

        var usuarios = _usuarios.Listar();
        var objetivo = usuarios.FirstOrDefault(u => u.IdUsuario == idUsuario);

        if (objetivo is null)
            return ResultadoOperacion.Falla("La cuenta seleccionada ya no existe.");

        if (!activo && objetivo.Rol == RolUsuario.Administrador &&
            usuarios.Count(u => u.Rol == RolUsuario.Administrador && u.Activo) <= 1)
            return ResultadoOperacion.Falla("Debe quedar al menos un administrador activo.");

        _usuarios.CambiarEstado(idUsuario, activo);

        return ResultadoOperacion.Ok(activo
            ? $"La cuenta «{objetivo.NombreUsuario}» quedó activa."
            : $"La cuenta «{objetivo.NombreUsuario}» quedó desactivada.");
    }

    public static string? ValidarClave(string clave)
    {
        if (string.IsNullOrEmpty(clave) || clave.Length < 8)
            return "La contraseña debe tener al menos 8 caracteres.";

        if (!clave.Any(char.IsLetter) || !clave.Any(char.IsDigit))
            return "La contraseña debe combinar letras y números.";

        return null;
    }
}
