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
    IReadOnlyList<HechoCatalogo> ListarHechos();
    IReadOnlyList<VinculoHechoHipotesis> ListarVinculos(int idHipotesis);
    void GuardarVinculo(int idHecho, int idHipotesis, decimal peso, bool necesario);
    void EliminarVinculo(int idHecho, int idHipotesis);
}
