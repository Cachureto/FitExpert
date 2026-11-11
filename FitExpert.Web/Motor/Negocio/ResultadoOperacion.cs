using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>Resultado de una operacion de negocio con el mensaje para el usuario.</summary>
public sealed record ResultadoOperacion(bool Exito, string Mensaje)
{
    public static ResultadoOperacion Ok(string mensaje) => new(true, mensaje);
    public static ResultadoOperacion Falla(string mensaje) => new(false, mensaje);
}

/// <summary>Resultado del inicio de sesion.</summary>
public sealed record ResultadoAutenticacion(bool Exito, string Mensaje, Usuario? Usuario)
{
    public static ResultadoAutenticacion Ok(Usuario usuario) => new(true, "", usuario);
    public static ResultadoAutenticacion Falla(string mensaje) => new(false, mensaje, null);
}
