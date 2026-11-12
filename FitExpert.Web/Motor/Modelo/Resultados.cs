namespace FitExpert.Motor.Modelo;

/// <summary>Hechos que el usuario declaro al recorrer el arbol de decision.</summary>
public sealed class MemoriaTrabajo
{
    private readonly HashSet<int> _ids = new();
    private readonly List<Hecho> _hechos = new();

    public IReadOnlyList<Hecho> Hechos => _hechos;
    public IReadOnlyCollection<int> Ids => _ids;

    public void Registrar(Hecho hecho)
    {
        if (_ids.Add(hecho.IdHecho))
            _hechos.Add(hecho);
    }

    public bool Contiene(int idHecho) => _ids.Contains(idHecho);
}

/// <summary>Un paso del recorrido: el nodo que se mostro y la respuesta elegida.</summary>
public sealed record PasoRecorrido(int Paso, NodoDecision Nodo, Hecho Respuesta);

/// <summary>Camino completo desde la raiz hasta un nodo terminal.</summary>
public sealed class RecorridoArbol
{
    public required IReadOnlyList<PasoRecorrido> Pasos { get; init; }
    public required NodoDecision Terminal { get; init; }
}

/// <summary>Una condicion de la regla y el aporte que hace a su certeza.</summary>
public sealed record AporteHecho(CondicionRegla Condicion, bool Declarado, decimal Aporte);

/// <summary>Hipotesis evaluada a traves de una de sus reglas, con su certeza y su peso normalizado.</summary>
public sealed class ResultadoHipotesis
{
    public required Hipotesis Hipotesis { get; init; }
    public required Regla Regla { get; init; }
    public required IReadOnlyList<AporteHecho> Aportes { get; init; }

    public decimal Cobertura { get; set; }        // evidencia reunida sobre la total
    public decimal SoporteNecesario { get; set; } // condiciones de la regla que se cumplen
    public decimal Certeza { get; set; }          // 0 a 1
    public decimal Peso { get; set; }             // porcentaje entre las finalistas
    public int Posicion { get; set; }
    public bool ConclusionArbol { get; set; }     // la regla se cumple completa
    public string ReglaTexto { get; set; } = "";
    public string Explicacion { get; set; } = "";
}

/// <summary>Salida completa de una ejecucion del motor.</summary>
public sealed class ResultadoInferencia
{
    public required RecorridoArbol Recorrido { get; init; }
    public required IReadOnlyList<ResultadoHipotesis> Hipotesis { get; init; }
    public required int HipotesisEvaluadas { get; init; }
    public int ConsultasEjecutadas { get; init; }
    public DateTime FechaHora { get; init; } = DateTime.UtcNow;
}
