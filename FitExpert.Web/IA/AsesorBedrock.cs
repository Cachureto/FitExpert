using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Runtime;

namespace FitExpert.Web.IA;

/// <summary>
/// Coach de IA con Amazon Bedrock (modelo Amazon Nova).
///
/// El sistema experto sigue tomando la decision: el motor de inferencia
/// elige la hipotesis con sus reglas y pesos. El modelo de lenguaje solo
/// recibe ese resultado ya calculado y lo explica en lenguaje natural,
/// lo organiza en una semana y responde preguntas sobre el, sin cambiarlo.
/// Es un enfoque hibrido: razonamiento simbolico + IA generativa.
/// </summary>
public sealed class AsesorBedrock
{
    public const string ModeloPorDefecto = "us.amazon.nova-lite-v1:0";

    private const string Instrucciones = """
        Eres FitCoach, el asistente de la aplicación académica FitExpert.
        Recibes el resultado que ya calculó un sistema experto basado en reglas:
        la hipótesis principal con su rutina, su dieta, la regla que se activó,
        las alternativas y los hechos que declaró el usuario.

        Reglas que debes cumplir siempre:
        1. No cambies la recomendación ni propongas otra hipótesis: explica la que eligió el sistema experto.
        2. Usa solo los datos recibidos. No inventes cifras, diagnósticos ni suplementos.
        3. Si el usuario pregunta por dolor, lesiones, enfermedades o medicamentos, recomiéndale consultar a un profesional de la salud.
        4. Si la pregunta no tiene relación con entrenamiento, alimentación, descanso o hábitos, dilo con amabilidad y vuelve al plan.
        5. Responde en español, con tono motivador y cercano, en máximo 230 palabras.
        6. Escribe en texto plano: párrafos cortos y viñetas que empiecen con "- ". No uses tablas ni encabezados con #.
        """;

    private const string SolicitudPorDefecto = """
        Explícame mi plan en dos frases, organízalo en una semana de lunes a domingo
        (indicando los días de descanso), y dame tres consejos de hábitos para cumplirlo.
        Termina con una frase motivadora corta.
        """;

    private readonly Lazy<IAmazonBedrockRuntime> _cliente;

    public string Modelo { get; }

    public AsesorBedrock(string? modelo)
    {
        Modelo = string.IsNullOrWhiteSpace(modelo) ? ModeloPorDefecto : modelo.Trim();

        // El cliente toma la region y las credenciales del entorno: en Lambda,
        // la region de la funcion y su rol de ejecucion.
        _cliente = new Lazy<IAmazonBedrockRuntime>(() => new AmazonBedrockRuntimeClient());
    }

    public async Task<string> ConsultarAsync(string contexto, string? pregunta, CancellationToken cancelacion)
    {
        var solicitudUsuario = string.IsNullOrWhiteSpace(pregunta) ? SolicitudPorDefecto : pregunta.Trim();

        var solicitud = new ConverseRequest
        {
            ModelId = Modelo,
            System = new List<SystemContentBlock> { new() { Text = Instrucciones } },
            Messages = new List<Message>
            {
                new()
                {
                    Role = ConversationRole.User,
                    Content = new List<ContentBlock>
                    {
                        new() { Text = $"Resultado del sistema experto FitExpert (JSON):\n{contexto}\n\nSolicitud del usuario: {solicitudUsuario}" }
                    }
                }
            },
            InferenceConfig = new InferenceConfiguration { MaxTokens = 700, Temperature = 0.4f }
        };

        var respuesta = await _cliente.Value.ConverseAsync(solicitud, cancelacion);

        var bloques = respuesta.Output?.Message?.Content ?? new List<ContentBlock>();
        var texto = string.Concat(bloques.Select(b => b.Text ?? ""));

        return string.IsNullOrWhiteSpace(texto)
            ? "El coach no generó una respuesta. Intenta de nuevo."
            : texto.Trim();
    }

    /// <summary>Reduce el resultado guardado a lo que el modelo necesita.</summary>
    public static string ResumirResultado(string resultadoJson)
    {
        var raiz = JsonNode.Parse(resultadoJson)?.AsObject() ?? new JsonObject();
        var hipotesis = raiz["hipotesis"]?.AsArray() ?? new JsonArray();
        var principal = hipotesis.FirstOrDefault()?.AsObject();

        var resumen = new JsonObject
        {
            ["recomendacion"] = principal is null ? null : new JsonObject
            {
                ["codigo"] = principal["codigo"]?.DeepClone(),
                ["nombre"] = principal["nombre"]?.DeepClone(),
                ["area"] = principal["area"]?.DeepClone(),
                ["certezaPorcentaje"] = principal["peso"]?.DeepClone(),
                ["rutina"] = principal["rutina"]?.DeepClone(),
                ["dieta"] = principal["dieta"]?.DeepClone(),
                ["reglaActivada"] = principal["regla"]?.DeepClone(),
                ["explicacion"] = principal["explicacion"]?.DeepClone()
            },
            ["alternativas"] = new JsonArray(hipotesis.Skip(1)
                .Select(h => (JsonNode)new JsonObject
                {
                    ["codigo"] = h?["codigo"]?.DeepClone(),
                    ["nombre"] = h?["nombre"]?.DeepClone(),
                    ["certezaPorcentaje"] = h?["peso"]?.DeepClone()
                }).ToArray()),
            ["hechosDeclarados"] = raiz["hechosDeclarados"]?.DeepClone()
        };

        return resumen.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Mensaje claro para los errores mas comunes de configuracion.</summary>
    public string DescribirError(Exception error) => error switch
    {
        AccessDeniedException =>
            "La función no tiene permiso para usar Amazon Bedrock. Agregue la política de Bedrock al rol de ejecución de Lambda (ver guía, paso 3E).",
        ValidationException =>
            $"Amazon Bedrock rechazó el modelo «{Modelo}». Revise la variable Bedrock__Modelo o la región de la función.",
        ThrottlingException =>
            "Amazon Bedrock está recibiendo demasiadas solicitudes. Espere unos segundos e intente de nuevo.",
        AmazonServiceException servicio =>
            $"Amazon Bedrock respondió con un error: {servicio.Message}",
        AmazonClientException =>
            "No se encontraron credenciales de AWS. En Lambda se usan las del rol; en el computador, ejecute «aws configure».",
        _ => "No fue posible consultar al coach de IA en este momento."
    };
}
