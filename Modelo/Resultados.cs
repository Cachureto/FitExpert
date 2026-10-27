namespace FitExpert.Motor.Modelo;

/// <summary>Hechos que el usuario declaro al responder las preguntas.</summary>
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

/// <summary>Un hecho de la hipotesis y el aporte que hace a su certeza.</summary>
public sealed record AporteHecho(VinculoHechoHipotesis Vinculo, bool Declarado, decimal Aporte);

/// <summary>Hipotesis evaluada, con su regla, su certeza y su peso normalizado.</summary>
public sealed class ResultadoHipotesis
{
    public required Hipotesis Hipotesis { get; init; }
    public required IReadOnlyList<AporteHecho> Aportes { get; init; }

    public decimal Cobertura { get; set; }        // evidencia reunida sobre la total
    public decimal SoporteNecesario { get; set; } // hechos obligatorios declarados
    public decimal Certeza { get; set; }          // 0 a 1
    public decimal Peso { get; set; }             // porcentaje entre las finalistas
    public int Posicion { get; set; }
    public string Regla { get; set; } = "";
    public string Explicacion { get; set; } = "";
}

/// <summary>Salida completa de una ejecucion del motor.</summary>
public sealed class ResultadoInferencia
{
    public required IReadOnlyList<ResultadoHipotesis> Hipotesis { get; init; }
    public required IReadOnlyList<ResultadoHipotesis> Evaluadas { get; init; }
    public int ConsultasEjecutadas { get; init; }
    public DateTime FechaHora { get; init; } = DateTime.Now;
}
