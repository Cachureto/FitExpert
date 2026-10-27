# FitExpert · Motor de inferencia (versión 2)

Sistema experto con consultas sucesivas y granulares a la base de conocimiento,
pesos probabilísticos y arquitectura en tres capas.


## Estructura

```
FitExpert.Motor/
├── Modelo/          Entidades y resultados
│   ├── Entidades.cs         Pregunta · Hecho · Hipotesis · VinculoHechoHipotesis
│   └── Resultados.cs        MemoriaTrabajo · AporteHecho · ResultadoHipotesis
├── Datos/           Capa de acceso a datos: todo el SQL vive aquí
│   ├── ConexionBD.cs
│   ├── IConocimientoRepository.cs   Las 3 consultas sucesivas
│   └── ConocimientoRepository.cs
├── Negocio/         Capa de lógica
│   ├── CalculadorCerteza.cs   Cobertura · hechos obligatorios · prioridad
│   ├── RedactorReglas.cs      SI … Y … ENTONCES y explicación
│   └── MotorInferencia.cs     Orquesta los 5 pasos
└── Presentacion/
    └── Program.cs             Cuestionario generado desde la tabla Pregunta
```

## Cómo ejecutarlo

1. Ejecutar `FitExpertDB.sql` en SSMS. Crea la base y carga 9 preguntas,
   32 hechos, 26 hipótesis y 90 vínculos.
2. Ajustar la cadena en `Datos/ConexionBD.cs` si el servidor no es
   `localhost\SQLEXPRESS`.
3. En la carpeta `FitExpert.Motor`: `dotnet restore` y después `dotnet run`.

## Modelo de pesos

| Paso | Fórmula |
|---|---|
| Cobertura | Σ pesos de los hechos declarados / Σ pesos de la hipótesis |
| Soporte necesario | hechos obligatorios declarados / hechos obligatorios |
| Certeza | Cobertura × (0,20 + 0,80 × Soporte) × Prioridad / 100 |
| Peso final | Certezaᵢ / Σ Certezas de las 5 finalistas × 100 |

## Ejemplo de salida

Usuario de 60 años o más, con fatiga moderada, restricción de rodilla y objetivo
de perder peso:

| # | Hipótesis | Peso |
|---|---|---|
| 1 | H3 Fuerza funcional y equilibrio | 29,6 % |
| 2 | H7 Semana de descarga | 26,6 % |
| 3 | H8 Rutina sin impacto en rodilla | 25,1 % |
| 4 | H21 Peso · Principiante | 13,4 % |
| 5 | H22 Peso · Intermedio | 5,2 % |
