using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Gestion de la base de conocimiento (CU-04). Valida los cambios antes de guardarlos.
/// Las reglas se derivan del arbol de decision, por eso aqui solo se consultan.
/// </summary>
public sealed class ServicioConocimiento
{
    private readonly IAdministracionRepository _repositorio;

    public ServicioConocimiento(IAdministracionRepository repositorio) => _repositorio = repositorio;

    public IReadOnlyList<ReglaDerivada> ListarReglas() => _repositorio.ListarReglas();
    public IReadOnlyList<Hipotesis> ListarHipotesis() => _repositorio.ListarHipotesis();

    public ResultadoOperacion ActualizarHipotesis(Hipotesis hipotesis)
    {
        var nombre = (hipotesis.Nombre ?? "").Trim();
        var rutina = (hipotesis.Rutina ?? "").Trim();
        var dieta = (hipotesis.Dieta ?? "").Trim();

        if (nombre.Length == 0 || nombre.Length > 80)
            return ResultadoOperacion.Falla("El nombre es obligatorio y admite hasta 80 caracteres.");

        if (hipotesis.Prioridad < 1 || hipotesis.Prioridad > 100)
            return ResultadoOperacion.Falla("La prioridad debe estar entre 1 y 100.");

        if (rutina.Length == 0 || rutina.Length > 400)
            return ResultadoOperacion.Falla("La rutina es obligatoria y admite hasta 400 caracteres.");

        if (dieta.Length == 0 || dieta.Length > 400)
            return ResultadoOperacion.Falla("La dieta es obligatoria y admite hasta 400 caracteres.");

        _repositorio.ActualizarHipotesis(new Hipotesis
        {
            IdHipotesis = hipotesis.IdHipotesis,
            Codigo      = hipotesis.Codigo,
            Nombre      = nombre,
            Area        = hipotesis.Area,
            Prioridad   = hipotesis.Prioridad,
            Rutina      = rutina,
            Dieta       = dieta
        });

        return ResultadoOperacion.Ok($"La hipótesis {hipotesis.Codigo} se actualizó.");
    }
}
