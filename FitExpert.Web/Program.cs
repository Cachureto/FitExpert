using System.Security.Cryptography;
using Amazon.Lambda.AspNetCoreServer.Hosting;
using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;
using FitExpert.Motor.Negocio;
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

builder.Services.AddCors(opciones => opciones.AddDefaultPolicy(politica =>
    politica.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (claveTemporal)
    app.Logger.LogWarning("Jwt:Clave no está configurada; se usa una clave temporal. Configúrela en producción.");

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/salud", () => Results.Ok(new
{
    servicio = "FitExpert · Motor de inferencia",
    estado = "en línea",
    baseDeConocimiento = baseConfigurada ? "configurada" : "sin cadena de conexión",
    firmaDeTokens = claveTemporal ? "temporal" : "configurada"
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

// Arbol de decision (tabla NodoDecision). La interfaz lo recorre nodo por nodo:
// cada respuesta lleva al nodo hijo hasta llegar a un nodo terminal Hn.
appUsuario.MapGet("/arbol", (ServicioRecomendacion servicio) => Ejecutar(() => ArbolDto.Desde(servicio.ObtenerArbol())));

// Inferencia sobre el recorrido. Las respuestas deben llegar en el orden del recorrido.
appUsuario.MapPost("/evaluar", (SolicitudEvaluacion solicitud, HttpContext http, ServicioRecomendacion servicio, EvaluacionRepository evaluaciones) => Ejecutar(() =>
{
    var sesion = http.SesionActual();
    var resultado = servicio.Evaluar(solicitud.Hechos ?? new List<int>());

    var detalles = resultado.Hipotesis.Select(h => new DetalleHipotesis(
        h.Posicion,
        h.Hipotesis.IdHipotesis,
        h.Regla.IdRegla,
        h.Hipotesis.Codigo,
        h.Hipotesis.Nombre,
        h.Hipotesis.Area,
        h.Hipotesis.Prioridad,
        Math.Round(h.Peso, 1),
        Math.Round(h.Cobertura * 100m, 1),
        Math.Round(h.SoporteNecesario * 100m, 1),
        h.ConclusionArbol,
        h.Hipotesis.Rutina,
        h.Hipotesis.Dieta,
        h.Regla.Codigo,
        h.ReglaTexto,
        h.Explicacion,
        h.Hipotesis.Aviso)).ToList();

    // Se guarda la evaluacion, el camino recorrido y el detalle de las 5 hipotesis.
    var id = evaluaciones.Guardar(
        sesion.IdUsuario,
        resultado.Recorrido.Terminal.IdNodo,
        resultado.HipotesisEvaluadas,
        resultado.ConsultasEjecutadas,
        resultado.Recorrido.Pasos.Select(p => new RespuestaAGuardar(p.Paso, p.Nodo.IdNodo, p.Respuesta.IdHecho)).ToList(),
        detalles);

    return evaluaciones.Obtener(id, sesion.IdUsuario)
        ?? throw new SolicitudInvalidaException("No fue posible leer la evaluación guardada.");
}));

// Historial del usuario.
appUsuario.MapGet("/evaluaciones", (HttpContext http, EvaluacionRepository evaluaciones) => Ejecutar(() =>
    evaluaciones.ListarPorUsuario(http.SesionActual().IdUsuario)));

appUsuario.MapGet("/evaluaciones/{id:int}", (int id, HttpContext http, EvaluacionRepository evaluaciones) => Ejecutar(() =>
    evaluaciones.Obtener(id, http.SesionActual().IdUsuario)
        ?? throw new SolicitudInvalidaException("La evaluación no existe.")));

// Detalle de una de las 5 hipotesis (tabla DetalleHipotesis): rutina, dieta, regla y explicacion.
appUsuario.MapGet("/evaluaciones/{id:int}/hipotesis/{posicion:int}", (int id, int posicion, HttpContext http, EvaluacionRepository evaluaciones) => Ejecutar(() =>
    evaluaciones.ObtenerDetalle(id, http.SesionActual().IdUsuario, posicion)
        ?? throw new SolicitudInvalidaException("La hipótesis solicitada no existe en esta evaluación.")));

// ------------------------------------------------------------ administrador

var admin = api.MapGroup("/admin").ExigirSesion(RolUsuario.Administrador);

admin.MapGet("/resumen", (EvaluacionRepository evaluaciones) => Ejecutar(() => evaluaciones.Resumen()));

// Reglas SI ... Y ... ENTONCES almacenadas en la base (vista vw_Reglas).
admin.MapGet("/reglas", (ServicioConocimiento conocimiento) => Ejecutar(() => conocimiento.ListarReglas()));

admin.MapGet("/hipotesis", (ServicioConocimiento conocimiento) => Ejecutar(() => conocimiento.ListarHipotesis()));

admin.MapGet("/arbol", (ServicioRecomendacion servicio) => Ejecutar(() => ArbolDto.Desde(servicio.ObtenerArbol())));

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
        Aviso       = actual.Aviso,
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
    catch (RecorridoInvalidoException error)
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
public sealed record UsuarioSesionDto(int IdUsuario, string Usuario, string NombreCompleto, string Rol);
public sealed record SesionDto(string Token, DateTimeOffset Expira, UsuarioSesionDto Usuario);
public sealed record UsuarioAdminDto(int IdUsuario, string Usuario, string NombreCompleto, string Rol, bool Activo, DateTime FechaCreacion, DateTime? UltimoAcceso);
public sealed record MensajeDto(string Mensaje);

public sealed record OpcionDto(int IdHecho, string Codigo, string Texto, int IdNodoDestino);
public sealed record PreguntaNodoDto(string Codigo, string Texto, string? Ayuda);
public sealed record HipotesisNodoDto(string Codigo, string Nombre, string Area, string? Aviso);
public sealed record NodoDto(int IdNodo, string Codigo, string Tipo, PreguntaNodoDto? Pregunta, List<OpcionDto> Opciones, HipotesisNodoDto? Hipotesis);

public sealed record ArbolDto(int Raiz, List<NodoDto> Nodos)
{
    public static ArbolDto Desde(ArbolDecision arbol) => new(
        arbol.Raiz.IdNodo,
        arbol.Nodos.Select(n => new NodoDto(
            n.IdNodo,
            n.Codigo,
            n.EsTerminal ? "TERMINAL" : "DECISION",
            n.Pregunta is null ? null : new PreguntaNodoDto(n.Pregunta.Codigo, n.Pregunta.Texto, n.Pregunta.Ayuda),
            n.Pregunta is null
                ? new List<OpcionDto>()
                : n.Pregunta.Opciones
                    .Select(o => (Opcion: o, Destino: arbol.Hijo(n, o.IdHecho)))
                    .Where(x => x.Destino is not null)
                    .Select(x => new OpcionDto(x.Opcion.IdHecho, x.Opcion.Codigo, x.Opcion.Texto, x.Destino!.IdNodo))
                    .ToList(),
            n.Hipotesis is null ? null : new HipotesisNodoDto(n.Hipotesis.Codigo, n.Hipotesis.Nombre, n.Hipotesis.Area, n.Hipotesis.Aviso)))
        .ToList());
}

public sealed class SolicitudInvalidaException(string mensaje) : Exception(mensaje);
public sealed class AccesoDenegadoException(string mensaje) : Exception(mensaje);
