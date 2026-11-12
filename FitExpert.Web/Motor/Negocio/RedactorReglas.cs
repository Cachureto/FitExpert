using System.Text;
using FitExpert.Motor.Modelo;

namespace FitExpert.Motor.Negocio;

/// <summary>
/// Redacta la regla y la explicacion de una hipotesis a partir de la base de
/// conocimiento, en el formato SI condicion1 Y condicion2 ENTONCES hipotesis.
/// No genera texto libre: solo combina los enunciados almacenados.
/// </summary>
public sealed class RedactorReglas
{
    public string Redactar(ResultadoHipotesis resultado)
    {
        var condiciones = resultado.Aportes
            .OrderBy(a => a.Condicion.Orden)
            .Select(a => a.Condicion.Hecho.Enunciado)
            .ToList();

        return $"{resultado.Regla.Codigo}: SI el usuario {string.Join(" Y ", condiciones)} ENTONCES " +
               $"{resultado.Hipotesis.Codigo} ({resultado.Hipotesis.Nombre})";
    }

    public string Explicar(ResultadoHipotesis resultado, RecorridoArbol recorrido)
    {
        var texto = new StringBuilder();

        var cumplidas = resultado.Aportes.Where(a => a.Declarado).OrderBy(a => a.Condicion.Orden).ToList();
        var faltantes = resultado.Aportes.Where(a => !a.Declarado).OrderBy(a => a.Condicion.Orden).ToList();

        if (resultado.ConclusionArbol)
        {
            texto.Append($"Es la conclusión del árbol de decisión: el recorrido ")
                 .Append(string.Join(" → ", recorrido.Pasos.Select(p => $"{p.Nodo.Codigo} «{p.Respuesta.Texto}»")))
                 .Append($" termina en el nodo {recorrido.Terminal.Codigo} ({resultado.Hipotesis.Codigo}) ")
                 .Append($"y se cumplen todas las condiciones de la regla {resultado.Regla.Codigo}. ");
        }
        else
        {
            texto.Append($"Alternativa compatible: la regla {resultado.Regla.Codigo} se cumple de forma parcial. ");
        }

        if (resultado.ConclusionArbol && resultado.Hipotesis.ConcluyeRecorrido)
            texto.Append($"Al ser una condición de riesgo, el recorrido concluye aquí y no se evalúan planes alternativos: {resultado.Hipotesis.Aviso} ");
        else
            texto.Append($"Peso entre las finalistas: {resultado.Peso:0.0} %. ");

        if (cumplidas.Count > 0)
            texto.Append("Condiciones que se cumplen: ")
                 .Append(string.Join("; ", cumplidas.Select(a => $"{a.Condicion.Hecho.Enunciado} (peso {a.Aporte:0.00})")))
                 .Append(". ");

        if (faltantes.Count > 0)
            texto.Append("Condiciones que no se cumplen: ")
                 .Append(string.Join("; ", faltantes.Select(a => a.Condicion.Hecho.Enunciado)))
                 .Append(". ");

        texto.Append($"Cobertura de la evidencia {resultado.Cobertura:P0}, ")
             .Append($"condiciones cumplidas {resultado.SoporteNecesario:P0}, ")
             .Append($"prioridad del área {resultado.Hipotesis.Area} {resultado.Hipotesis.Prioridad}.");

        return texto.ToString();
    }
}
