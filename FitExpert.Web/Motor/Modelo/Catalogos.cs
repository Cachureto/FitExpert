namespace FitExpert.Motor.Modelo;

/// <summary>Regla SI ... Y ... ENTONCES tal como la presenta la vista vw_Reglas.</summary>
public sealed record ReglaDerivada(string Regla, string Hipotesis, string Texto, string Area, int Prioridad, int Condiciones);
