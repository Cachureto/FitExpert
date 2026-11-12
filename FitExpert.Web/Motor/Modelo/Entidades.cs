namespace FitExpert.Motor.Modelo;

/// <summary>Pregunta que formula un nodo de decision. Tabla Pregunta.</summary>
public sealed class Pregunta
{
    public int IdPregunta { get; init; }
    public string Codigo { get; init; } = "";
    public string Texto { get; init; } = "";
    public string? Ayuda { get; init; }
    public List<Hecho> Opciones { get; } = new();
}

/// <summary>Respuesta posible de una pregunta (rama del arbol). Tabla Hecho.</summary>
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

    /// <summary>Aviso de condicion de riesgo. Si existe, el recorrido concluye en esta hipotesis.</summary>
    public string? Aviso { get; init; }

    public bool ConcluyeRecorrido => !string.IsNullOrWhiteSpace(Aviso);

    public decimal PesoPrevio => Prioridad / 100m;
}

/// <summary>
/// Nodo del arbol de decision. Tabla NodoDecision.
/// Un nodo de decision tiene Pregunta; un nodo terminal tiene Hipotesis.
/// IdHechoRama es la respuesta del nodo padre que lleva a este nodo.
/// </summary>
public sealed class NodoDecision
{
    public int IdNodo { get; init; }
    public string Codigo { get; init; } = "";
    public int? IdNodoPadre { get; init; }
    public int? IdHechoRama { get; init; }
    public Pregunta? Pregunta { get; init; }
    public Hipotesis? Hipotesis { get; init; }

    public bool EsTerminal => Hipotesis is not null;
}

/// <summary>Arbol de decision completo, tal como esta en la base de conocimiento.</summary>
public sealed class ArbolDecision
{
    public required IReadOnlyList<NodoDecision> Nodos { get; init; }

    public NodoDecision Raiz => Nodos.Single(n => n.IdNodoPadre is null);

    /// <summary>Nodo al que conduce la respuesta <paramref name="idHecho"/> desde <paramref name="nodo"/>.</summary>
    public NodoDecision? Hijo(NodoDecision nodo, int idHecho) =>
        Nodos.FirstOrDefault(n => n.IdNodoPadre == nodo.IdNodo && n.IdHechoRama == idHecho);
}

/// <summary>Regla de produccion asociada a un nodo terminal. Tabla Regla.</summary>
public sealed class Regla
{
    public int IdRegla { get; init; }
    public string Codigo { get; init; } = "";
    public int IdNodoTerminal { get; init; }
    public required Hipotesis Hipotesis { get; init; }
}

/// <summary>Condicion (parte SI) de una regla. Tabla CondicionRegla.</summary>
public sealed class CondicionRegla
{
    public required Hecho Hecho { get; init; }
    public int IdRegla { get; init; }
    public int Orden { get; init; }
    public decimal Peso { get; init; }
}
