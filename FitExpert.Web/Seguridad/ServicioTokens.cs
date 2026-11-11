using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FitExpert.Motor.Modelo;

namespace FitExpert.Web.Seguridad;

/// <summary>Datos de la persona que inicio sesion, leidos del token.</summary>
public sealed record Sesion(int IdUsuario, string NombreUsuario, string NombreCompleto, RolUsuario Rol, DateTimeOffset Expira);

/// <summary>
/// Emite y valida tokens JWT firmados con HMAC-SHA256 (HS256).
/// El servicio no guarda sesiones: cada solicitud trae su token en el
/// encabezado Authorization, lo que permite ejecutar varias copias de la
/// funcion Lambda a la vez. La clave de firma se lee de Jwt__Clave.
/// </summary>
public sealed class ServicioTokens
{
    public static readonly TimeSpan Duracion = TimeSpan.FromHours(8);

    private const string RolAdministrador = "ADMINISTRADOR";
    private const string RolUsuarioFinal = "USUARIO";

    private readonly byte[] _clave;

    public ServicioTokens(string clave) => _clave = Encoding.UTF8.GetBytes(clave);

    public (string Token, DateTimeOffset Expira) Emitir(Usuario usuario)
    {
        var expira = DateTimeOffset.UtcNow.Add(Duracion);

        var encabezado = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var carga = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["sub"]  = usuario.IdUsuario,
            ["usr"]  = usuario.NombreUsuario,
            ["name"] = usuario.NombreCompleto,
            ["role"] = usuario.Rol == RolUsuario.Administrador ? RolAdministrador : RolUsuarioFinal,
            ["exp"]  = expira.ToUnixTimeSeconds()
        }));

        var firma = Base64Url(Firmar($"{encabezado}.{carga}"));
        return ($"{encabezado}.{carga}.{firma}", expira);
    }

    public Sesion? Validar(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var partes = token.Split('.');
        if (partes.Length != 3)
            return null;

        try
        {
            var esperada = Firmar($"{partes[0]}.{partes[1]}");
            if (!CryptographicOperations.FixedTimeEquals(esperada, DesdeBase64Url(partes[2])))
                return null;

            using var documento = JsonDocument.Parse(DesdeBase64Url(partes[1]));
            var datos = documento.RootElement;

            var expira = DateTimeOffset.FromUnixTimeSeconds(datos.GetProperty("exp").GetInt64());
            if (expira <= DateTimeOffset.UtcNow)
                return null;

            return new Sesion(
                datos.GetProperty("sub").GetInt32(),
                datos.GetProperty("usr").GetString() ?? "",
                datos.GetProperty("name").GetString() ?? "",
                datos.GetProperty("role").GetString() == RolAdministrador
                    ? RolUsuario.Administrador
                    : RolUsuario.UsuarioFinal,
                expira);
        }
        catch (Exception error) when (error is FormatException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private byte[] Firmar(string contenido) =>
        HMACSHA256.HashData(_clave, Encoding.ASCII.GetBytes(contenido));

    private static string Base64Url(byte[] datos) =>
        Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DesdeBase64Url(string texto)
    {
        texto = texto.Replace('-', '+').Replace('_', '/');
        texto += (texto.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(texto);
    }
}

/// <summary>Filtro que exige un token valido y, si se indica, un rol.</summary>
public static class FiltrosSesion
{
    private const string Clave = "fitexpert.sesion";

    public static TBuilder ExigirSesion<TBuilder>(this TBuilder constructor, RolUsuario? rol = null)
        where TBuilder : IEndpointConventionBuilder =>
        constructor.AddEndpointFilter(async (contexto, siguiente) =>
        {
            var http = contexto.HttpContext;
            var tokens = http.RequestServices.GetRequiredService<ServicioTokens>();

            var encabezado = http.Request.Headers.Authorization.ToString();
            var token = encabezado.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? encabezado[7..].Trim()
                : null;

            var sesion = tokens.Validar(token);

            if (sesion is null)
                return Results.Json(
                    new { error = token is null ? "Inicie sesión para continuar." : "Su sesión expiró. Inicie sesión de nuevo." },
                    statusCode: StatusCodes.Status401Unauthorized);

            if (rol is not null && sesion.Rol != rol)
                return Results.Json(new { error = "No tiene permisos para esta sección." }, statusCode: StatusCodes.Status403Forbidden);

            http.Items[Clave] = sesion;
            return await siguiente(contexto);
        });

    public static Sesion SesionActual(this HttpContext http) =>
        http.Items[Clave] as Sesion ?? throw new InvalidOperationException("La solicitud no tiene sesión.");
}
