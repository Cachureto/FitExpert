using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Datos;

/// <summary>
/// Consultas sucesivas y granulares a la base de conocimiento.
/// El motor no escribe SQL: cada paso de la inferencia es una de estas operaciones.
/// </summary>
public interface IConocimientoRepository
{
    /// <summary>Consulta 1: preguntas con sus hechos, para construir el formulario.</summary>
    IReadOnlyList<Pregunta> ObtenerPreguntas();

    /// <summary>Consulta 2: hipotesis vinculadas a alguno de los hechos declarados.</summary>
    IReadOnlyList<Hipotesis> ObtenerHipotesisCandidatas(IEnumerable<int> idsHechos);

    /// <summary>Consulta 3: todos los hechos que respaldan una hipotesis.</summary>
    IReadOnlyList<VinculoHechoHipotesis> ObtenerVinculosDeHipotesis(int idHipotesis);

    int ConsultasEjecutadas { get; }
}
