using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Datos;

/// <summary>
/// Consultas sucesivas y granulares a la base de conocimiento.
/// El motor no escribe SQL: cada paso de la inferencia es una de estas operaciones.
/// </summary>
public interface IConocimientoRepository
{
    /// <summary>Consulta 1: arbol de decision con sus preguntas, respuestas e hipotesis.</summary>
    ArbolDecision ObtenerArbol();

    /// <summary>Consulta 2: reglas activas con alguna condicion entre los hechos declarados.</summary>
    IReadOnlyList<Regla> ObtenerReglasCandidatas(IEnumerable<int> idsHechos);

    /// <summary>Consulta 3: condiciones (parte SI) de una regla.</summary>
    IReadOnlyList<CondicionRegla> ObtenerCondicionesDeRegla(int idRegla);

    int ConsultasEjecutadas { get; }
}
