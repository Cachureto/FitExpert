using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Motor de inferencia. No contiene conocimiento del dominio: recorre la base de
/// conocimiento con consultas sucesivas y aplica el modelo de pesos.
///
/// Paso 1  Consultar las preguntas y sus hechos (capa de presentacion).
/// Paso 2  Consultar las hipotesis vinculadas a los hechos declarados.
/// Paso 3  Por cada hipotesis, consultar los hechos que la respaldan.
/// Paso 4  Calcular su certeza con cobertura, hechos obligatorios y prioridad.
/// Paso 5  Ordenar, tomar las cinco primeras y normalizar sus pesos.
/// </summary>
public sealed class MotorInferencia
{
    private readonly IConocimientoRepository _conocimiento;
    private readonly CalculadorCerteza _calculador;
    private readonly RedactorReglas _redactor;

    /// <summary>Numero de hipotesis que el sistema entrega al usuario.</summary>
    public int HipotesisEsperadas { get; init; } = 5;

    public MotorInferencia(
        IConocimientoRepository conocimiento,
        CalculadorCerteza calculador,
        RedactorReglas redactor)
    {
        _conocimiento = conocimiento;
        _calculador = calculador;
        _redactor = redactor;
    }

    public ResultadoInferencia Ejecutar(MemoriaTrabajo memoria)
    {
        // Paso 2
        var candidatas = _conocimiento.ObtenerHipotesisCandidatas(memoria.Ids);
        var evaluadas = new List<ResultadoHipotesis>();

        foreach (var hipotesis in candidatas)
        {
            // Paso 3
            var vinculos = _conocimiento.ObtenerVinculosDeHipotesis(hipotesis.IdHipotesis);

            // Paso 4
            evaluadas.Add(_calculador.Evaluar(hipotesis, vinculos, memoria));
        }

        // Paso 5
        var finalistas = evaluadas
            .Where(h => h.Certeza > 0)
            .OrderByDescending(h => h.Certeza)
            .ThenByDescending(h => h.Hipotesis.Prioridad)
            .ThenBy(h => h.Hipotesis.IdHipotesis)
            .Take(HipotesisEsperadas)
            .ToList();

        _calculador.Normalizar(finalistas);

        for (var i = 0; i < finalistas.Count; i++)
        {
            finalistas[i].Posicion = i + 1;
            finalistas[i].Regla = _redactor.Redactar(finalistas[i]);
            finalistas[i].Explicacion = _redactor.Explicar(finalistas[i]);
        }

        return new ResultadoInferencia
        {
            Hipotesis           = finalistas,
            Evaluadas           = evaluadas,
            ConsultasEjecutadas = _conocimiento.ConsultasEjecutadas
        };
    }
}
