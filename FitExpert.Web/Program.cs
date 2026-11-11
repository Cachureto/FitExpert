using System.Security.Cryptography;
using System.Text.Json;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;
using FitExpert.Motor.Negocio;
using FitExpert.Web.IA;
using FitExpert.Web.Seguridad;
using Microsoft.Data.SqlClient;

// =====================================================================
// FitExpert como servicio web.
// La base de conocimiento vive en Amazon RDS for SQL Server; este
// servicio, publicado en AWS Lambda con una URL de funcion, expone el
// motor de inferencia por HTTP y sirve la interfaz de wwwroot/index.html.
// En el computador se ejecuta como una aplicacion web normal (F5).
//
// Rutas
//   Publicas        /api/salud · /api/auth/login · /api/auth/registro
//   Usuario final   /api/app/...   (token de cualquier rol)
//                   /api/app/coach usa Amazon Bedrock para explicar el plan
//   Administrador   /api/admin/... (token con rol ADMINISTRADOR)
// =====================================================================

var builder = WebApplication.CreateBuilder(args);

// Dentro de AWS Lambda, ASP.NET Core atiende las solicitudes de la URL de
// la funcion. Fuera de Lambda esta linea no hace nada.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

var cadena = builder.Configuration.GetConnectionString("FitExpertDB");
var baseConfigurada = !string.IsNullOrWhiteSpace(cadena);
var conexion = new ConexionBD(cadena ?? "");

// Clave para firmar los tokens. En Lambda debe configurarse Jwt__Clave para
// que todas las copias de la funcion acepten los mismos tokens.
var claveJwt = builder.Configuration["Jwt:Clave"];
var claveTemporal = string.IsNullOrWhiteSpace(claveJwt) || claveJwt.Length < 32;
if (claveTemporal)
    claveJwt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

var repositorioUsuarios = new UsuarioRepository(conexion);
var repositorioAdministracion = new AdministracionRepository(conexion);

builder.Services.AddSingleton(new ServicioTokens(claveJwt!));
builder.Services.AddSingleton(new ServicioRecomendacion(() => new ConocimientoRepository(conexion)));
builder.Services.AddSingleton(new ServicioAutenticacion(repositorioUsuarios));
builder.Services.AddSingleton(new ServicioUsuarios(repositorioUsuarios));
builder.Services.AddSingleton(new ServicioConocimiento(repositorioAdministracion));
builder.Services.AddSingleton(new EvaluacionRepository(conexion));

// Servicio de IA de la nube: Amazon Bedrock. El modelo se puede cambiar con
// la variable Bedrock__Modelo (por defecto, Amazon Nova Lite).
var asesor = new AsesorBedrock(builder.Configuration["Bedrock:Modelo"]);
builder.Services.AddSingleton(asesor);

builder.Services.AddCors(opciones => opciones.AddDefaultPolicy(politica =>
    politica.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (claveTemporal)
    app.Logger.LogWarning("Jwt:Clave no está configurada; se usa una clave temporal. Configúrela en producción.");

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var api = app.MapGroup("/api");

api.MapGet("/salud", () => Results.Ok(new
{
    servicio = "FitExpert · Motor de inferencia",
    estado = "en línea",
    baseDeConocimiento = baseConfigurada ? "configurada" : "sin cadena de conexión",
    firmaDeTokens = claveTemporal ? "temporal" : "configurada",
    asesorIA = $"Amazon Bedrock · {asesor.Modelo}"
}));

// ------------------------------------------------------------ autenticacion

var auth = api.MapGroup("/auth");

auth.MapPost("/login", (SolicitudLogin solicitud, ServicioAutenticacion autenticacion, ServicioTokens tokens) => Ejecutar(() =>
{
    var perfil = string.Equals(solicitud.Perfil, "ADMINISTRADOR", StringComparison.OrdinalIgnoreCase)
        ? RolUsuario.Administrador
        : RolUsuario.UsuarioFinal;

    var resultado = autenticacion.IniciarSesion(solicitud.Usuario ?? "", solicitud.Clave ?? "", perfil);

    if (!resultado.Exito || resultado.Usuario is null)
        throw new AccesoDenegadoException(resultado.Mensaje);

    return CrearSesion(resultado.Usuario, tokens);
}));

auth.MapPost("/registro", (SolicitudRegistro solicitud, ServicioUsuarios usuarios, ServicioAutenticacion autenticacion, ServicioTokens tokens) => Ejecutar(() =>
{
    var registro = usuarios.Registrar(
        solicitud.NombreCompleto ?? "",
        solicitud.Usuario ?? "",
        solicitud.Clave ?? "",
        solicitud.Confirmacion ?? "",
        RolUsuario.UsuarioFinal);

    if (!registro.Exito)
        throw new SolicitudInvalidaException(registro.Mensaje);

    var ingreso = autenticacion.IniciarSesion(solicitud.Usuario ?? "", solicitud.Clave ?? "", RolUsuario.UsuarioFinal);
    if (!ingreso.Exito || ingreso.Usuario is null)
        throw new SolicitudInvalidaException(ingreso.Mensaje);

    return CrearSesion(ingreso.Usuario, tokens);
}));

auth.MapGet("/yo", (HttpContext http) =>
{
    var sesion = http.SesionActual();
    return Results.Ok(new UsuarioSesionDto(sesion.IdUsuario, sesion.NombreUsuario, sesion.NombreCompleto, CodigoRol(sesion.Rol)));
}).ExigirSesion();

// ------------------------------------------------------------ usuario final

var appUsuario = api.MapGroup("/app").ExigirSesion();

// Paso 1 del motor: preguntas y hechos posibles (tablas Pregunta y Hecho).
appUsuario.MapGet("/preguntas", (ServicioRecomendacion servicio) => Ejecutar(() =>
    servicio.ObtenerCuestionario()
        .Select(p => new PreguntaDto(
            p.IdPregunta,
            p.Codigo,
            p.Texto,
            p.Obligatoria,
            p.Opciones.Select(h => new OpcionDto(h.IdHecho, h.Codigo, h.Texto)).ToList()))
        .ToList()));

// Pasos 2 a 5 del motor: inferencia sobre los hechos declarados.
appUsuario.MapPost("/evaluar", (SolicitudEvaluacion solicitud, HttpContext http, ServicioRecomendacion servicio, EvaluacionRepository evaluaciones) => Ejecutar(() =>
{
    var sesion = http.SesionActual();
    var preguntas = servicio.ObtenerCuestionario();
    var hechosPorId = preguntas.SelectMany(p => p.Opciones).ToDictionary(h => h.IdHecho);
    var ids = (solicitud.Hechos ?? new List<int>()).Distinct().ToList();

    var desconocidos = ids.Where(id => !hechosPorId.ContainsKey(id)).ToList();
    if (desconocidos.Count > 0)
        throw new SolicitudInvalidaException($"Hechos no válidos: {string.Join(", ", desconocidos)}.");

    var hechos = ids.Select(id => hechosPorId[id]).ToList();

    if (hechos.GroupBy(h => h.IdPregunta).Any(g => g.Count() > 1))
        throw new SolicitudInvalidaException("Solo se admite una respuesta por pregunta.");

    var respondidas = hechos.Select(h => h.IdPregunta).ToHashSet();
    var faltantes = preguntas
        .Where(p => p.Obligatoria && !respondidas.Contains(p.IdPregunta))
        .Select(p => p.Codigo)
        .ToList();

    if (faltantes.Count > 0)
        throw new SolicitudInvalidaException($"Faltan respuestas obligatorias: {string.Join(", ", faltantes)}.");

    var resultado = servicio.Evaluar(hechos);

    var respuesta = new RespuestaEvaluacion(
        0,
        DateTime.UtcNow,
        resultado.Evaluadas.Count,
        resultado.ConsultasEjecutadas,
        hechos.Select(h => h.Enunciado).ToList(),
        resultado.Hipotesis.Select(h => new HipotesisDto(
            h.Posicion,
            h.Hipotesis.Codigo,
            h.Hipotesis.Nombre,
            h.Hipotesis.Area,
            h.Hipotesis.Prioridad,
            Math.Round(h.Peso, 1),
            Math.Round(h.Cobertura * 100m, 0),
            Math.Round(h.SoporteNecesario * 100m, 0),
            h.Hipotesis.Rutina,
            h.Hipotesis.Dieta,
            h.Regla,
            h.Explicacion)).ToList());

    // Se guarda en el historial del usuario con su recomendacion principal.
    var principal = respuesta.Hipotesis.FirstOrDefault();
    if (principal is not null)
    {
        var id = evaluaciones.Guardar(
            sesion.IdUsuario,
            principal.Codigo,
            principal.Nombre,
            principal.Peso,
            JsonSerializer.Serialize(respuesta, json));

        respuesta = respuesta with { IdEvaluacion = id };
    }

    return respuesta;
}));

// Historial del usuario.
appUsuario.MapGet("/evaluaciones", (HttpContext http, EvaluacionRepository evaluaciones) => Ejecutar(() =>
    evaluaciones.ListarPorUsuario(http.SesionActual().IdUsuario)));

appUsuario.MapGet("/evaluaciones/{id:int}", (int id, HttpContext http, EvaluacionRepository evaluaciones) => Ejecutar(() =>
{
    var guardado = evaluaciones.ObtenerResultado(id, http.SesionActual().IdUsuario)
        ?? throw new SolicitudInvalidaException("La evaluación no existe.");

    using var documento = JsonDocument.Parse(guardado);
    var raiz = documento.RootElement.Clone();
    return raiz;
}));

// Coach de IA: Amazon Bedrock explica y organiza el plan que ya calculo el motor.
appUsuario.MapPost("/coach", async (SolicitudCoach solicitud, HttpContext http, EvaluacionRepository evaluaciones, AsesorBedrock coach, CancellationToken cancelacion) =>
{
    if (!baseConfigurada)
        return Results.Problem(title: "La base de conocimiento no está configurada.", statusCode: StatusCodes.Status503ServiceUnavailable);

    var pregunta = solicitud.Pregunta?.Trim();
    if (pregunta is { Length: > 300 })
        return Results.BadRequest(new { error = "La pregunta admite hasta 300 caracteres." });

    string? guardado;
    try
    {
        guardado = evaluaciones.ObtenerResultado(solicitud.IdEvaluacion, http.SesionActual().IdUsuario);
    }
    catch (SqlException error)
    {
        return Results.Problem(title: "No fue posible consultar la base de conocimiento en Amazon RDS.", detail: error.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (guardado is null)
        return Results.BadRequest(new { error = "La evaluación no existe." });

    try
    {
        var respuesta = await coach.ConsultarAsync(AsesorBedrock.ResumirResultado(guardado), pregunta, cancelacion);
        return Results.Ok(new RespuestaCoach(respuesta, coach.Modelo));
    }
    catch (Exception error) when (error is not OperationCanceledException)
    {
        app.Logger.LogError(error, "Error al consultar Amazon Bedrock");
        return Results.Json(new { error = coach.DescribirError(error) }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// ------------------------------------------------------------ administrador

var admin = api.MapGroup("/admin").ExigirSesion(RolUsuario.Administrador);

admin.MapGet("/resumen", (EvaluacionRepository evaluaciones) => Ejecutar(() => evaluaciones.Resumen()));

// Reglas SI ... Y ... ENTONCES almacenadas en la base (vista vw_Reglas).
admin.MapGet("/reglas", (ServicioConocimiento conocimiento) => Ejecutar(() => conocimiento.ListarReglas()));

admin.MapGet("/hipotesis", (ServicioConocimiento conocimiento) => Ejecutar(() => conocimiento.ListarHipotesis()));

admin.MapPut("/hipotesis/{id:int}", (int id, SolicitudHipotesis solicitud, ServicioConocimiento conocimiento) => Ejecutar(() =>
{
    var actual = conocimiento.ListarHipotesis().FirstOrDefault(h => h.IdHipotesis == id)
        ?? throw new SolicitudInvalidaException("La hipótesis no existe.");

    var resultado = conocimiento.ActualizarHipotesis(new Hipotesis
    {
        IdHipotesis = actual.IdHipotesis,
        Codigo      = actual.Codigo,
        Area        = actual.Area,
        Nombre      = solicitud.Nombre ?? "",
        Prioridad   = solicitud.Prioridad,
        Rutina      = solicitud.Rutina ?? "",
        Dieta       = solicitud.Dieta ?? ""
    });

    return Confirmar(resultado);
}));

admin.MapGet("/usuarios", (ServicioUsuarios usuarios) => Ejecutar(() =>
    usuarios.Listar().Select(u => new UsuarioAdminDto(
        u.IdUsuario,
        u.NombreUsuario,
        u.NombreCompleto,
        CodigoRol(u.Rol),
        u.Activo,
        DateTime.SpecifyKind(u.FechaCreacion, DateTimeKind.Utc),
        u.UltimoAcceso is null ? null : DateTime.SpecifyKind(u.UltimoAcceso.Value, DateTimeKind.Utc))).ToList()));

admin.MapPost("/usuarios", (SolicitudNuevoUsuario solicitud, ServicioUsuarios usuarios) => Ejecutar(() =>
{
    var rol = string.Equals(solicitud.Rol, "ADMINISTRADOR", StringComparison.OrdinalIgnoreCase)
        ? RolUsuario.Administrador
        : RolUsuario.UsuarioFinal;

    return Confirmar(usuarios.Registrar(
        solicitud.NombreCompleto ?? "",
        solicitud.Usuario ?? "",
        solicitud.Clave ?? "",
        solicitud.Clave ?? "",
        rol));
}));

admin.MapPut("/usuarios/{id:int}/estado", (int id, SolicitudEstado solicitud, HttpContext http, ServicioUsuarios usuarios) => Ejecutar(() =>
{
    var sesion = http.SesionActual();
    var solicitante = new Usuario { IdUsuario = sesion.IdUsuario, NombreUsuario = sesion.NombreUsuario, Rol = sesion.Rol };
    return Confirmar(usuarios.CambiarEstado(id, solicitud.Activo, solicitante));
}));

app.Run();

// ---------------------------------------------------------------- auxiliares

SesionDto CrearSesion(Usuario usuario, ServicioTokens tokens)
{
    var (token, expira) = tokens.Emitir(usuario);
    return new SesionDto(token, expira,
        new UsuarioSesionDto(usuario.IdUsuario, usuario.NombreUsuario, usuario.NombreCompleto, CodigoRol(usuario.Rol)));
}

static string CodigoRol(RolUsuario rol) => rol == RolUsuario.Administrador ? "ADMINISTRADOR" : "USUARIO";

static MensajeDto Confirmar(ResultadoOperacion resultado) =>
    resultado.Exito ? new MensajeDto(resultado.Mensaje) : throw new SolicitudInvalidaException(resultado.Mensaje);

// Ejecuta una operacion y traduce los errores a respuestas HTTP claras.
IResult Ejecutar<T>(Func<T> accion)
{
    if (!baseConfigurada)
        return Results.Problem(
            title: "La base de conocimiento no está configurada.",
            detail: "Agregue la variable de entorno ConnectionStrings__FitExpertDB en la configuración de la función Lambda.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    try
    {
        return Results.Ok(accion());
    }
    catch (AccesoDenegadoException error)
    {
        return Results.Json(new { error = error.Message }, statusCode: StatusCodes.Status401Unauthorized);
    }
    catch (SolicitudInvalidaException error)
    {
        return Results.BadRequest(new { error = error.Message });
    }
    catch (SqlException error)
    {
        return Results.Problem(
            title: "No fue posible consultar la base de conocimiento en Amazon RDS.",
            detail: error.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

// ------------------------------------------------------------- contratos

public sealed record SolicitudLogin(string? Usuario, string? Clave, string? Perfil);
public sealed record SolicitudRegistro(string? NombreCompleto, string? Usuario, string? Clave, string? Confirmacion);
public sealed record SolicitudEvaluacion(List<int>? Hechos);
public sealed record SolicitudHipotesis(string? Nombre, int Prioridad, string? Rutina, string? Dieta);
public sealed record SolicitudNuevoUsuario(string? NombreCompleto, string? Usuario, string? Clave, string? Rol);
public sealed record SolicitudEstado(bool Activo);
public sealed record SolicitudCoach(int IdEvaluacion, string? Pregunta);
public sealed record RespuestaCoach(string Respuesta, string Modelo);

public sealed record UsuarioSesionDto(int IdUsuario, string Usuario, string NombreCompleto, string Rol);
public sealed record SesionDto(string Token, DateTimeOffset Expira, UsuarioSesionDto Usuario);
public sealed record UsuarioAdminDto(int IdUsuario, string Usuario, string NombreCompleto, string Rol, bool Activo, DateTime FechaCreacion, DateTime? UltimoAcceso);
public sealed record MensajeDto(string Mensaje);

public sealed record OpcionDto(int IdHecho, string Codigo, string Texto);
public sealed record PreguntaDto(int IdPregunta, string Codigo, string Texto, bool Obligatoria, List<OpcionDto> Opciones);

public sealed record HipotesisDto(
    int Posicion,
    string Codigo,
    string Nombre,
    string Area,
    int Prioridad,
    decimal Peso,
    decimal Cobertura,
    decimal HechosObligatorios,
    string Rutina,
    string Dieta,
    string Regla,
    string Explicacion);

public sealed record RespuestaEvaluacion(
    int IdEvaluacion,
    DateTime Fecha,
    int HipotesisEvaluadas,
    int ConsultasEjecutadas,
    List<string> HechosDeclarados,
    List<HipotesisDto> Hipotesis);

public sealed class SolicitudInvalidaException(string mensaje) : Exception(mensaje);
public sealed class AccesoDenegadoException(string mensaje) : Exception(mensaje);
