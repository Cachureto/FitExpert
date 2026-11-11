namespace FitExpert.Motor.Modelo;

/// <summary>Hecho con el codigo de su pregunta, para listarlo en el panel de administracion.</summary>
public sealed record HechoCatalogo(int IdHecho, string Pregunta, string Codigo, string Texto, string Enunciado)
{
    public string Descripcion => $"{Pregunta} · {Texto}";
}

/// <summary>Regla SI ... Y ... ENTONCES derivada en la vista vw_Reglas.</summary>
public sealed record ReglaDerivada(string Hipotesis, string Regla, string Area, int Prioridad, int Condiciones);
