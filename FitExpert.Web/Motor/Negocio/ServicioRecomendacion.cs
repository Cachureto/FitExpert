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

    /// <summary>Paso 1: preguntas y hechos para construir el cuestionario.</summary>
    public IReadOnlyList<Pregunta> ObtenerCuestionario() => _crearRepositorio().ObtenerPreguntas();

    /// <summary>Pasos 2 a 5: inferencia sobre los hechos declarados.</summary>
    public ResultadoInferencia Evaluar(IEnumerable<Hecho> respuestas)
    {
        var memoria = new MemoriaTrabajo();
        foreach (var hecho in respuestas)
            memoria.Registrar(hecho);

        if (memoria.Hechos.Count == 0)
            throw new InvalidOperationException("Debe responder al menos una pregunta.");

        var motor = new MotorInferencia(
            _crearRepositorio(),
            new CalculadorCerteza(),
            new RedactorReglas());

        return motor.Ejecutar(memoria);
    }
}
