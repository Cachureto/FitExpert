namespace FitExpert.Motor.Modelo;

/// <summary>Pregunta que el sistema formula al usuario. Tabla Pregunta.</summary>
public sealed class Pregunta
{
    public int IdPregunta { get; init; }
    public string Codigo { get; init; } = "";
    public string Texto { get; init; } = "";
    public int Orden { get; init; }
    public bool Obligatoria { get; init; }
    public List<Hecho> Opciones { get; } = new();
}

/// <summary>Respuesta posible de una pregunta. Tabla Hecho.</summary>
public sealed class Hecho
{
    public int IdHecho { get; init; }
    public int IdPregunta { get; init; }
    public string Codigo { get; init; } = "";
    public string Texto { get; init; } = "";       // se muestra como opcion
    public string Enunciado { get; init; } = "";   // se usa al redactar la regla
    public int Orden { get; init; }
}

/// <summary>Conclusion del sistema (H1 a H26). Tabla Hipotesis.</summary>
public sealed class Hipotesis
{
    public int IdHipotesis { get; init; }
    public string Codigo { get; init; } = "";
    public string Nombre { get; init; } = "";
    public string Area { get; init; } = "";
    public int Prioridad { get; init; }
    public string Rutina { get; init; } = "";
    public string Dieta { get; init; } = "";

    public decimal PesoPrevio => Prioridad / 100m;
}

/// <summary>
/// Vinculo entre un hecho y una hipotesis. Tabla HechoHipotesis.
/// Los vinculos necesarios de una hipotesis forman su regla
/// SI hecho1 Y hecho2 ENTONCES hipotesis.
/// </summary>
public sealed class VinculoHechoHipotesis
{
    public required Hecho Hecho { get; init; }
    public int IdHipotesis { get; init; }
    public decimal Peso { get; init; }
    public bool Necesario { get; init; }
}
