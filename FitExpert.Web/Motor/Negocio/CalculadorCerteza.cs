using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Modelo de pesos. Para cada hipotesis mide cuanta de su evidencia aporto el
/// usuario y cuanto de esa evidencia corresponde a hechos obligatorios.
///
///   Cobertura        = suma de pesos declarados / suma de pesos de la hipotesis
///   SoporteNecesario = hechos obligatorios declarados / hechos obligatorios
///   Certeza          = Cobertura * (Base + (1 - Base) * SoporteNecesario) * PesoPrevio
///
/// Ninguna hipotesis se descarta de golpe: una coincidencia parcial baja el peso
/// en lugar de eliminarla, que es lo que permite entregar una lista ordenada.
/// </summary>
public sealed class CalculadorCerteza
{
    /// <summary>Fraccion de certeza que conserva una hipotesis sin sus hechos obligatorios.</summary>
    public decimal BaseSinObligatorios { get; init; } = 0.20m;

    public ResultadoHipotesis Evaluar(
        Hipotesis hipotesis,
        IReadOnlyList<VinculoHechoHipotesis> vinculos,
        MemoriaTrabajo memoria)
    {
        var aportes = vinculos
            .Select(vinculo =>
            {
                var declarado = memoria.Contiene(vinculo.Hecho.IdHecho);
                var aporte = declarado ? vinculo.Peso : 0m;
                return new AporteHecho(vinculo, declarado, aporte);
            })
            .ToList();

        var pesoTotal = vinculos.Sum(v => v.Peso);
        var cobertura = pesoTotal > 0 ? aportes.Sum(a => a.Aporte) / pesoTotal : 0m;

        var obligatorios = aportes.Where(a => a.Vinculo.Necesario).ToList();
        var soporte = obligatorios.Count > 0
            ? (decimal)obligatorios.Count(a => a.Declarado) / obligatorios.Count
            : 1m;

        var factor = BaseSinObligatorios + (1 - BaseSinObligatorios) * soporte;
        var certeza = cobertura * factor * hipotesis.PesoPrevio;

        return new ResultadoHipotesis
        {
            Hipotesis        = hipotesis,
            Aportes          = aportes,
            Cobertura        = Math.Round(cobertura, 4),
            SoporteNecesario = Math.Round(soporte, 4),
            Certeza          = Math.Round(certeza, 4)
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
