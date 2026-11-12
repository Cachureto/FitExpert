using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Motor de inferencia. No contiene conocimiento del dominio: recorre la base de
/// conocimiento con consultas sucesivas y aplica el modelo de pesos.
///
/// Paso 1  Validar el recorrido: cada respuesta debe ser una rama del nodo
///         actual del arbol de decision, hasta llegar a un nodo terminal.
/// Paso 2  Consultar las reglas que tienen alguna condicion entre los hechos declarados.
/// Paso 3  Por cada regla, consultar sus condiciones (parte SI).
/// Paso 4  Calcular su certeza con cobertura, condiciones cumplidas y prioridad.
///         La unica regla que se cumple completa es la del nodo terminal
///         alcanzado: esa es la conclusion del arbol.
/// Paso 5  Ordenar (primero la conclusion del arbol), tomar las cinco
///         primeras hipotesis y normalizar sus pesos. Si la conclusion es una
///         condicion de riesgo (hipotesis con aviso), el recorrido termina en
///         ella: no se presentan alternativas y se remite a un profesional.
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

    public ResultadoInferencia Ejecutar(ArbolDecision arbol, IReadOnlyList<int> respuestas)
    {
        // Paso 1
        var recorrido = Recorrer(arbol, respuestas);

        var memoria = new MemoriaTrabajo();
        foreach (var paso in recorrido.Pasos)
            memoria.Registrar(paso.Respuesta);

        // Paso 2
        var reglas = _conocimiento.ObtenerReglasCandidatas(memoria.Ids);
        var evaluadas = new List<ResultadoHipotesis>();

        foreach (var regla in reglas)
        {
            // Paso 3
            var condiciones = _conocimiento.ObtenerCondicionesDeRegla(regla.IdRegla);

            // Paso 4
            evaluadas.Add(_calculador.Evaluar(regla, condiciones, memoria));
        }

        var conclusion = evaluadas.Where(r => r.ConclusionArbol).ToList();
        if (conclusion.Count != 1 || conclusion[0].Regla.IdNodoTerminal != recorrido.Terminal.IdNodo)
            throw new InvalidOperationException(
                $"La base de conocimiento no es coherente: el nodo {recorrido.Terminal.Codigo} debe tener exactamente una regla activa que se cumpla.");

        // Paso 5. Cada hipotesis aparece una sola vez, con su mejor regla.
        var finalistas = evaluadas
            .GroupBy(r => r.Hipotesis.IdHipotesis)
            .Select(g => g.OrderByDescending(r => r.ConclusionArbol).ThenByDescending(r => r.Certeza).First())
            .Where(r => r.Certeza > 0)
            .OrderByDescending(r => r.ConclusionArbol)
            .ThenByDescending(r => r.Certeza)
            .ThenByDescending(r => r.Hipotesis.Prioridad)
            .ThenBy(r => r.Hipotesis.IdHipotesis)
            .Take(conclusion[0].Hipotesis.ConcluyeRecorrido ? 1 : HipotesisEsperadas)
            .ToList();

        _calculador.Normalizar(finalistas);

        for (var i = 0; i < finalistas.Count; i++)
        {
            finalistas[i].Posicion = i + 1;
            finalistas[i].ReglaTexto = _redactor.Redactar(finalistas[i]);
            finalistas[i].Explicacion = _redactor.Explicar(finalistas[i], recorrido);
        }

        return new ResultadoInferencia
        {
            Recorrido           = recorrido,
            Hipotesis           = finalistas,
            HipotesisEvaluadas  = evaluadas.Select(r => r.Hipotesis.IdHipotesis).Distinct().Count(),
            ConsultasEjecutadas = _conocimiento.ConsultasEjecutadas
        };
    }

    /// <summary>Sigue las respuestas desde la raiz y exige terminar en un nodo terminal.</summary>
    public static RecorridoArbol Recorrer(ArbolDecision arbol, IReadOnlyList<int> respuestas)
    {
        var actual = arbol.Raiz;
        var pasos = new List<PasoRecorrido>();

        foreach (var idHecho in respuestas)
        {
            if (actual.EsTerminal)
                throw new RecorridoInvalidoException(
                    $"El recorrido ya terminó en {actual.Hipotesis!.Codigo}; sobran respuestas.");

            var hecho = actual.Pregunta!.Opciones.FirstOrDefault(o => o.IdHecho == idHecho)
                ?? throw new RecorridoInvalidoException(
                    $"La respuesta {idHecho} no corresponde a la pregunta «{actual.Pregunta.Texto}».");

            pasos.Add(new PasoRecorrido(pasos.Count + 1, actual, hecho));

            actual = arbol.Hijo(actual, idHecho)
                ?? throw new RecorridoInvalidoException(
                    $"El árbol no tiene una rama para «{hecho.Texto}» en el nodo {actual.Codigo}.");
        }

        if (!actual.EsTerminal)
            throw new RecorridoInvalidoException(
                $"Falta responder: «{actual.Pregunta!.Texto}».");

        return new RecorridoArbol { Pasos = pasos, Terminal = actual };
    }
}

/// <summary>Las respuestas no siguen un camino valido del arbol de decision.</summary>
public sealed class RecorridoInvalidoException(string mensaje) : Exception(mensaje);
