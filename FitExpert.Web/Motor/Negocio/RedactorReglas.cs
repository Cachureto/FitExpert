using System.Text;
using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Redacta la regla y la explicacion de una hipotesis. La regla no esta
/// almacenada: se arma con los hechos obligatorios vinculados a la hipotesis,
/// en el formato SI hecho1 Y hecho2 ENTONCES hipotesis.
/// </summary>
public sealed class RedactorReglas
{
    public string Redactar(ResultadoHipotesis resultado)
    {
        var obligatorios = resultado.Aportes
            .Where(a => a.Vinculo.Necesario)
            .Select(a => a.Vinculo.Hecho.Enunciado)
            .ToList();

        var antecedente = obligatorios.Count > 0
            ? string.Join(" Y ", obligatorios)
            : "no se declara ningún hecho obligatorio";

        return $"SI el usuario {antecedente} ENTONCES " +
               $"{resultado.Hipotesis.Codigo} ({resultado.Hipotesis.Nombre})";
    }

    public string Explicar(ResultadoHipotesis resultado)
    {
        var texto = new StringBuilder();

        var cumplidos = resultado.Aportes.Where(a => a.Declarado).ToList();
        var faltantes = resultado.Aportes.Where(a => !a.Declarado).ToList();

        texto.Append($"Se concluye {resultado.Hipotesis.Codigo} con un peso de ")
             .Append($"{resultado.Peso:0.0} %. ");

        texto.Append("Hechos que la respaldan: ")
             .Append(string.Join("; ", cumplidos.Select(a =>
                 $"{a.Vinculo.Hecho.Enunciado} (aporte {a.Aporte:0.00}" +
                 (a.Vinculo.Necesario ? ", obligatorio)" : ")"))))
             .Append(". ");

        texto.Append(faltantes.Count == 0
            ? "No falta ningún hecho de la hipótesis. "
            : $"Hechos no declarados: {string.Join("; ", faltantes.Select(a => a.Vinculo.Hecho.Enunciado))}. ");

        texto.Append($"Cobertura de la evidencia {resultado.Cobertura:P0}, ")
             .Append($"hechos obligatorios cubiertos {resultado.SoporteNecesario:P0}, ")
             .Append($"prioridad del área {resultado.Hipotesis.Area} {resultado.Hipotesis.Prioridad}.");

        return texto.ToString();
    }
}
