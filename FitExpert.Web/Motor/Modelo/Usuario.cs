namespace FitExpert.Motor.Modelo;

/// <summary>Perfiles que pueden iniciar sesion en FitExpert.</summary>
public enum RolUsuario
{
    /// <summary>Responde el cuestionario y consulta sus recomendaciones.</summary>
    UsuarioFinal = 1,

    /// <summary>Ingeniero del conocimiento: gestiona la base de conocimiento y las cuentas.</summary>
    Administrador = 2
}

/// <summary>Cuenta de acceso. Tabla Usuario.</summary>
public sealed class Usuario
{
    public int IdUsuario { get; init; }
    public string NombreUsuario { get; init; } = "";
    public string NombreCompleto { get; init; } = "";
    public RolUsuario Rol { get; init; }
    public bool Activo { get; init; } = true;
    public DateTime FechaCreacion { get; init; }
    public DateTime? UltimoAcceso { get; init; }

    public string RolTexto => Rol == RolUsuario.Administrador ? "Administrador" : "Usuario final";
}
