using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Datos;

/// <summary>
/// Operaciones del ingeniero del conocimiento sobre la base de conocimiento
/// (caso de uso CU-04). El motor de inferencia no usa esta interfaz.
/// </summary>
public interface IAdministracionRepository
{
    IReadOnlyList<ReglaDerivada> ListarReglas();
    IReadOnlyList<Hipotesis> ListarHipotesis();
    void ActualizarHipotesis(Hipotesis hipotesis);
}
