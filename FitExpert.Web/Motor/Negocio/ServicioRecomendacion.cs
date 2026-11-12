using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Punto de entrada de la capa de presentacion al motor de inferencia.
/// Cada evaluacion usa un repositorio nuevo, de modo que el conteo de
/// consultas corresponde solo a esa ejecucion.
/// </summary>
public sealed class ServicioRecomendacion
{
    private readonly Func<IConocimientoRepository> _crearRepositorio;

    public ServicioRecomendacion(Func<IConocimientoRepository> crearRepositorio) =>
        _crearRepositorio = crearRepositorio;

    /// <summary>Arbol de decision que la interfaz recorre pregunta por pregunta.</summary>
    public ArbolDecision ObtenerArbol() => _crearRepositorio().ObtenerArbol();

    /// <summary>Recorrido del arbol e inferencia sobre los hechos declarados, en orden.</summary>
    public ResultadoInferencia Evaluar(IReadOnlyList<int> respuestas)
    {
        if (respuestas.Count == 0)
            throw new RecorridoInvalidoException("Debe responder al menos una pregunta.");

        var repositorio = _crearRepositorio();
        var motor = new MotorInferencia(repositorio, new CalculadorCerteza(), new RedactorReglas());

        return motor.Ejecutar(repositorio.ObtenerArbol(), respuestas);
    }
}
