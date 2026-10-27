using FitExpert.Motor.Datos;
using FitExpert.Motor.Modelo;
using FitExpert.Motor.Negocio;

namespace FitExpert.Motor.Presentacion;

/// <summary>
/// Capa de presentacion. El cuestionario se arma con las preguntas y los hechos
/// que la base de conocimiento declara, por lo que no hay preguntas escritas
/// en el codigo.
/// </summary>
public static class Program
{
    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var conocimiento = new ConocimientoRepository(ConexionBD.PorDefecto());

        var motor = new MotorInferencia(
            conocimiento,
            new CalculadorCerteza(),
            new RedactorReglas());

        Console.WriteLine("FitExpert · Motor de inferencia");
        Console.WriteLine(new string('=', 70));

        var memoria = Encuestar(conocimiento);
        var resultado = motor.Ejecutar(memoria);

        Mostrar(resultado);
    }

    /// <summary>Paso 1: el cuestionario se construye con la tabla Pregunta.</summary>
    private static MemoriaTrabajo Encuestar(IConocimientoRepository conocimiento)
    {
        var memoria = new MemoriaTrabajo();

        foreach (var pregunta in conocimiento.ObtenerPreguntas())
        {
            Console.WriteLine($"\n{pregunta.Codigo}. {pregunta.Texto}");

            for (var i = 0; i < pregunta.Opciones.Count; i++)
                Console.WriteLine($"   {i + 1}) {pregunta.Opciones[i].Texto}");

            var hecho = LeerOpcion(pregunta);

            if (hecho is not null)
                memoria.Registrar(hecho);
        }

        return memoria;
    }

    private static Hecho? LeerOpcion(Pregunta pregunta)
    {
        while (true)
        {
            Console.Write("   > ");
            var entrada = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(entrada) && !pregunta.Obligatoria)
                return null;

            if (int.TryParse(entrada, out var opcion) &&
                opcion >= 1 && opcion <= pregunta.Opciones.Count)
                return pregunta.Opciones[opcion - 1];

            Console.WriteLine($"   Escriba un número entre 1 y {pregunta.Opciones.Count}.");
        }
    }

    private static void Mostrar(ResultadoInferencia resultado)
    {
        Console.WriteLine("\n" + new string('=', 70));
        Console.WriteLine($"Hipótesis identificadas: {resultado.Hipotesis.Count}");
        Console.WriteLine($"Consultas a la base de conocimiento: {resultado.ConsultasEjecutadas}");
        Console.WriteLine($"Hipótesis evaluadas: {resultado.Evaluadas.Count}");
        Console.WriteLine(new string('=', 70));

        foreach (var hipotesis in resultado.Hipotesis)
        {
            Console.WriteLine($"\n{hipotesis.Posicion}. {hipotesis.Hipotesis.Codigo} · " +
                              $"{hipotesis.Hipotesis.Nombre}   [{hipotesis.Peso:0.0} %]");
            Console.WriteLine($"   {new string('█', (int)(hipotesis.Peso / 2))}");
            Console.WriteLine($"   Regla:   {hipotesis.Regla}");
            Console.WriteLine($"   Rutina:  {hipotesis.Hipotesis.Rutina}");
            Console.WriteLine($"   Dieta:   {hipotesis.Hipotesis.Dieta}");
            Console.WriteLine($"   Por qué: {hipotesis.Explicacion}");
        }

        Console.WriteLine("\nOrientación académica. No sustituye la valoración de un profesional.");
    }
}
