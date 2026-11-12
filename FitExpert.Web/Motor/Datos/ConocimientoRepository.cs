using FitExpert.Motor.Modelo;
using Microsoft.Data.SqlClient;

namespace FitExpert.Motor.Datos;

/// <summary>
/// Implementacion sobre SQL Server. Tablas de la base de conocimiento:
/// Pregunta, Hecho, Hipotesis, NodoDecision, Regla y CondicionRegla.
/// </summary>
public sealed class ConocimientoRepository : IConocimientoRepository
{
    private readonly ConexionBD _conexion;
    private int _consultas;

    public ConocimientoRepository(ConexionBD conexion) => _conexion = conexion;

    public int ConsultasEjecutadas => _consultas;

    public ArbolDecision ObtenerArbol()
    {
        const string sqlPreguntas = @"
            SELECT p.IdPregunta, p.Codigo, p.Texto, p.Ayuda,
                   h.IdHecho, h.Codigo, h.Texto, h.Enunciado, h.Orden
            FROM Pregunta p
            JOIN Hecho h ON h.IdPregunta = p.IdPregunta
            ORDER BY p.IdPregunta, h.Orden;";

        var preguntas = new Dictionary<int, Pregunta>();
        foreach (var (pregunta, hecho) in Leer(sqlPreguntas, _ => { }, lector => (
            new Pregunta
            {
                IdPregunta = lector.GetInt32(0),
                Codigo     = lector.GetString(1),
                Texto      = lector.GetString(2),
                Ayuda      = lector.IsDBNull(3) ? null : lector.GetString(3)
            },
            new Hecho
            {
                IdHecho    = lector.GetInt32(4),
                IdPregunta = lector.GetInt32(0),
                Codigo     = lector.GetString(5),
                Texto      = lector.GetString(6),
                Enunciado  = lector.GetString(7),
                Orden      = lector.GetInt32(8)
            })))
        {
            if (!preguntas.TryGetValue(pregunta.IdPregunta, out var existente))
                preguntas.Add(pregunta.IdPregunta, existente = pregunta);
            existente.Opciones.Add(hecho);
        }

        const string sqlNodos = @"
            SELECT n.IdNodo, n.Codigo, n.IdNodoPadre, n.IdHechoRama, n.IdPregunta,
                   h.IdHipotesis, h.Codigo, h.Nombre, h.Area, h.Prioridad, h.Rutina, h.Dieta, h.Aviso
            FROM NodoDecision n
            LEFT JOIN Hipotesis h ON h.IdHipotesis = n.IdHipotesis
            ORDER BY n.IdNodo;";

        var nodos = Leer(sqlNodos, _ => { }, lector => new NodoDecision
        {
            IdNodo      = lector.GetInt32(0),
            Codigo      = lector.GetString(1),
            IdNodoPadre = lector.IsDBNull(2) ? null : lector.GetInt32(2),
            IdHechoRama = lector.IsDBNull(3) ? null : lector.GetInt32(3),
            Pregunta    = lector.IsDBNull(4) ? null : preguntas[lector.GetInt32(4)],
            Hipotesis   = lector.IsDBNull(5) ? null : LeerHipotesis(lector, 5)
        });

        return new ArbolDecision { Nodos = nodos };
    }

    public IReadOnlyList<Regla> ObtenerReglasCandidatas(IEnumerable<int> idsHechos)
    {
        var ids = idsHechos.ToList();
        if (ids.Count == 0) return Array.Empty<Regla>();
        var parametros = ids.Select((_, i) => "@h" + i).ToList();

        var sql = $@"
            SELECT r.IdRegla, r.Codigo, r.IdNodoTerminal,
                   h.IdHipotesis, h.Codigo, h.Nombre, h.Area, h.Prioridad, h.Rutina, h.Dieta, h.Aviso
            FROM Regla r
            JOIN Hipotesis h ON h.IdHipotesis = r.IdHipotesis
            WHERE r.Activa = 1
              AND EXISTS (SELECT 1 FROM CondicionRegla cr
                          WHERE cr.IdRegla = r.IdRegla AND cr.IdHecho IN ({string.Join(", ", parametros)}))
            ORDER BY r.IdRegla;";

        return Leer(sql,
            comando =>
            {
                for (var i = 0; i < ids.Count; i++)
                    comando.Parameters.AddWithValue(parametros[i], ids[i]);
            },
            lector => new Regla
            {
                IdRegla        = lector.GetInt32(0),
                Codigo         = lector.GetString(1),
                IdNodoTerminal = lector.GetInt32(2),
                Hipotesis      = LeerHipotesis(lector, 3)
            });
    }

    public IReadOnlyList<CondicionRegla> ObtenerCondicionesDeRegla(int idRegla)
    {
        const string sql = @"
            SELECT f.IdHecho, f.IdPregunta, f.Codigo, f.Texto, f.Enunciado, f.Orden,
                   cr.IdRegla, cr.Orden, cr.Peso
            FROM CondicionRegla cr
            JOIN Hecho f ON f.IdHecho = cr.IdHecho
            WHERE cr.IdRegla = @idRegla
            ORDER BY cr.Orden;";

        return Leer(sql,
            comando => comando.Parameters.AddWithValue("@idRegla", idRegla),
            lector => new CondicionRegla
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
                IdRegla = lector.GetInt32(6),
                Orden   = lector.GetInt32(7),
                Peso    = lector.GetDecimal(8)
            });
    }

    private static Hipotesis LeerHipotesis(SqlDataReader lector, int inicio) => new()
    {
        IdHipotesis = lector.GetInt32(inicio),
        Codigo      = lector.GetString(inicio + 1),
        Nombre      = lector.GetString(inicio + 2),
        Area        = lector.GetString(inicio + 3),
        Prioridad   = lector.GetInt32(inicio + 4),
        Rutina      = lector.GetString(inicio + 5),
        Dieta       = lector.GetString(inicio + 6),
        Aviso       = lector.IsDBNull(inicio + 7) ? null : lector.GetString(inicio + 7)
    };

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
