using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Gestion de la base de conocimiento (CU-04). Valida los cambios antes de
/// guardarlos para que ninguna hipotesis se quede sin regla.
/// </summary>
public sealed class ServicioConocimiento
{
    private const string SinObligatorios =
        "La hipótesis debe conservar al menos un hecho obligatorio; de lo contrario su regla SI … ENTONCES quedaría vacía.";

    private readonly IAdministracionRepository _repositorio;

    public ServicioConocimiento(IAdministracionRepository repositorio) => _repositorio = repositorio;

    public IReadOnlyList<ReglaDerivada> ListarReglas() => _repositorio.ListarReglas();
    public IReadOnlyList<Hipotesis> ListarHipotesis() => _repositorio.ListarHipotesis();
    public IReadOnlyList<HechoCatalogo> ListarHechos() => _repositorio.ListarHechos();
    public IReadOnlyList<VinculoHechoHipotesis> ListarVinculos(int idHipotesis) =>
        _repositorio.ListarVinculos(idHipotesis);

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

    public ResultadoOperacion GuardarVinculo(int idHecho, int idHipotesis, decimal peso, bool necesario)
    {
        if (peso <= 0m || peso > 1m)
            return ResultadoOperacion.Falla("El peso debe estar entre 0,01 y 1,00.");

        var actuales = _repositorio.ListarVinculos(idHipotesis);
        var existente = actuales.FirstOrDefault(v => v.Hecho.IdHecho == idHecho);

        if (!necesario && existente is { Necesario: true } && actuales.Count(v => v.Necesario) == 1)
            return ResultadoOperacion.Falla(SinObligatorios);

        _repositorio.GuardarVinculo(idHecho, idHipotesis, peso, necesario);

        return ResultadoOperacion.Ok(existente is null ? "Vínculo agregado." : "Vínculo actualizado.");
    }

    public ResultadoOperacion EliminarVinculo(int idHecho, int idHipotesis)
    {
        var actuales = _repositorio.ListarVinculos(idHipotesis);
        var existente = actuales.FirstOrDefault(v => v.Hecho.IdHecho == idHecho);

        if (existente is null)
            return ResultadoOperacion.Falla("El vínculo seleccionado ya no existe.");

        if (existente.Necesario && actuales.Count(v => v.Necesario) == 1)
            return ResultadoOperacion.Falla(SinObligatorios);

        _repositorio.EliminarVinculo(idHecho, idHipotesis);
        return ResultadoOperacion.Ok("Vínculo eliminado.");
    }
}
