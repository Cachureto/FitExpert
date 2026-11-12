using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Modelo de pesos (granularidad). Para cada regla mide cuanta de su evidencia
/// aporto el usuario y cuantas de sus condiciones se cumplen.
///
///   Cobertura        = suma de pesos declarados / suma de pesos de la regla
///   SoporteNecesario = condiciones cumplidas / condiciones de la regla
///   Certeza          = Cobertura * (Base + (1 - Base) * SoporteNecesario) * PesoPrevio
///
/// Una regla que se cumple completa es la conclusion del arbol. Las demas no se
/// descartan de golpe: una coincidencia parcial baja su peso, lo que permite
/// entregar una lista ordenada de alternativas.
/// </summary>
public sealed class CalculadorCerteza
{
    /// <summary>Fraccion de certeza que conserva una regla sin condiciones cumplidas.</summary>
    public decimal BaseSinObligatorios { get; init; } = 0.20m;

    public ResultadoHipotesis Evaluar(
        Regla regla,
        IReadOnlyList<CondicionRegla> condiciones,
        MemoriaTrabajo memoria)
    {
        var aportes = condiciones
            .Select(condicion =>
            {
                var declarado = memoria.Contiene(condicion.Hecho.IdHecho);
                return new AporteHecho(condicion, declarado, declarado ? condicion.Peso : 0m);
            })
            .ToList();

        var pesoTotal = condiciones.Sum(c => c.Peso);
        var cobertura = pesoTotal > 0 ? aportes.Sum(a => a.Aporte) / pesoTotal : 0m;

        var soporte = aportes.Count > 0
            ? (decimal)aportes.Count(a => a.Declarado) / aportes.Count
            : 0m;

        var factor = BaseSinObligatorios + (1 - BaseSinObligatorios) * soporte;
        var certeza = cobertura * factor * regla.Hipotesis.PesoPrevio;

        return new ResultadoHipotesis
        {
            Hipotesis        = regla.Hipotesis,
            Regla            = regla,
            Aportes          = aportes,
            Cobertura        = Math.Round(cobertura, 4),
            SoporteNecesario = Math.Round(soporte, 4),
            Certeza          = Math.Round(certeza, 4),
            ConclusionArbol  = aportes.Count > 0 && aportes.All(a => a.Declarado)
        };
    }

    /// <summary>Reparte el 100 % entre las hipotesis finalistas.</summary>
    public void Normalizar(IReadOnlyList<ResultadoHipotesis> finalistas)
    {
        var total = finalistas.Sum(h => h.Certeza);
        if (total <= 0) return;

        foreach (var hipotesis in finalistas)
            hipotesis.Peso = Math.Round(hipotesis.Certeza / total * 100m, 2);
    }
}
