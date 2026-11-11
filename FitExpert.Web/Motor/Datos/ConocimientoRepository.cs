using FitExpert.Motor.Modelo;
using Microsoft.Data.SqlClient;

namespace FitExpert.Motor.Datos;

/// <summary>
/// Implementacion sobre SQL Server. Las cuatro tablas del modelo son
/// Pregunta, Hecho, Hipotesis y HechoHipotesis.
/// </summary>
public sealed class ConocimientoRepository : IConocimientoRepository
{
    private readonly ConexionBD _conexion;
    private int _consultas;

    public ConocimientoRepository(ConexionBD conexion) => _conexion = conexion;

    public int ConsultasEjecutadas => _consultas;

    public IReadOnlyList<Pregunta> ObtenerPreguntas()
    {
        const string sql = @"
            SELECT p.IdPregunta, p.Codigo, p.Texto, p.Orden, p.Obligatoria,
                   h.IdHecho, h.Codigo, h.Texto, h.Enunciado, h.Orden
            FROM Pregunta p
            JOIN Hecho h ON h.IdPregunta = p.IdPregunta
            ORDER BY p.Orden, h.Orden;";

        var preguntas = new Dictionary<int, Pregunta>();

        _consultas++;
        using var conexion = _conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        using var lector = comando.ExecuteReader();

        while (lector.Read())
        {
            var idPregunta = lector.GetInt32(0);

            if (!preguntas.TryGetValue(idPregunta, out var pregunta))
            {
                pregunta = new Pregunta
                {
                    IdPregunta  = idPregunta,
                    Codigo      = lector.GetString(1),
                    Texto       = lector.GetString(2),
                    Orden       = lector.GetInt32(3),
                    Obligatoria = lector.GetBoolean(4)
                };
                preguntas.Add(idPregunta, pregunta);
            }

            pregunta.Opciones.Add(new Hecho
            {
                IdHecho    = lector.GetInt32(5),
                IdPregunta = idPregunta,
                Codigo     = lector.GetString(6),
                Texto      = lector.GetString(7),
                Enunciado  = lector.GetString(8),
                Orden      = lector.GetInt32(9)
            });
        }

        return preguntas.Values.OrderBy(p => p.Orden).ToList();
    }

    public IReadOnlyList<Hipotesis> ObtenerHipotesisCandidatas(IEnumerable<int> idsHechos)
    {
        var ids = idsHechos.ToList();
        var parametros = ids.Select((_, i) => "@h" + i).ToList();

        var sql = $@"
            SELECT DISTINCT h.IdHipotesis, h.Codigo, h.Nombre, h.Area,
                            h.Prioridad, h.Rutina, h.Dieta
            FROM Hipotesis h
            JOIN HechoHipotesis hh ON hh.IdHipotesis = h.IdHipotesis
            WHERE hh.IdHecho IN ({string.Join(", ", parametros)})
            ORDER BY h.IdHipotesis;";

        return Leer(sql,
            comando =>
            {
                for (var i = 0; i < ids.Count; i++)
                    comando.Parameters.AddWithValue(parametros[i], ids[i]);
            },
            lector => new Hipotesis
            {
                IdHipotesis = lector.GetInt32(0),
                Codigo      = lector.GetString(1),
                Nombre      = lector.GetString(2),
                Area        = lector.GetString(3),
                Prioridad   = lector.GetInt32(4),
                Rutina      = lector.GetString(5),
                Dieta       = lector.GetString(6)
            });
    }

    public IReadOnlyList<VinculoHechoHipotesis> ObtenerVinculosDeHipotesis(int idHipotesis)
    {
        const string sql = @"
            SELECT f.IdHecho, f.IdPregunta, f.Codigo, f.Texto, f.Enunciado, f.Orden,
                   hh.IdHipotesis, hh.Peso, hh.Necesario
            FROM HechoHipotesis hh
            JOIN Hecho f ON f.IdHecho = hh.IdHecho
            WHERE hh.IdHipotesis = @idHipotesis
            ORDER BY hh.Necesario DESC, hh.Peso DESC;";

        return Leer(sql,
            comando => comando.Parameters.AddWithValue("@idHipotesis", idHipotesis),
            lector => new VinculoHechoHipotesis
            {
                Hecho = new Hecho
                {
                    IdHecho    = lector.GetInt32(0),
                    IdPregunta = lector.GetInt32(1),
                    Codigo     = lector.GetString(2),
                    Texto      = lector.GetString(3),
                    Enunciado  = lector.GetString(4),
                    Orden      = lector.GetInt32(5)
                },
                IdHipotesis = lector.GetInt32(6),
                Peso        = lector.GetDecimal(7),
                Necesario   = lector.GetBoolean(8)
            });
    }

    private List<T> Leer<T>(string sql, Action<SqlCommand> parametros, Func<SqlDataReader, T> mapear)
    {
        _consultas++;
        var filas = new List<T>();

        using var conexion = _conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        parametros(comando);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            filas.Add(mapear(lector));

        return filas;
    }
}
