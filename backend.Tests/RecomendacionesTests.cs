using Backend.Domain;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Pruebas TDD de las recomendaciones deterministas (HU-15 / RF-10, RN-03).
/// Cada recomendación debe tener una acción concreta y el indicador que la justifica.
/// </summary>
public class RecomendacionesTests
{
    private static readonly DateTime Ahora = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly AnaliticaService Servicio = new();

    private static EstudianteAnalitica Estudiante(int id) =>
        new(id, $"170000000{id}", $"Estudiante {id}", $"estudiante{id}@uta.edu.ec");

    private static ActividadAnalitica Actividad(int id, string componente, int parcial, int dias, decimal maximo = 10m) =>
        new(id, $"Actividad {id}", "Tarea", componente, parcial, Ahora.AddDays(dias), maximo);

    private static CalificacionAnalitica Nota(int actividadId, int estudianteId, decimal nota) =>
        new(actividadId, estudianteId, nota,
            nota == 0m ? EstadosActividad.NotaCero : EstadosActividad.Calificada, null);

    private static ContextoAnalitica Contexto(
        IEnumerable<ActividadAnalitica> actividades,
        IEnumerable<CalificacionAnalitica> calificaciones,
        IEnumerable<EstudianteAnalitica> estudiantes) =>
        new(new CursoAnalitica(1, "Curso de prueba", "TEST-01", "2026-B", 20m),
            estudiantes.ToList(), actividades.ToList(), calificaciones.ToList(), [], Ahora);

    /// <summary>Seis actividades vencidas que completan los dos parciales.</summary>
    private static List<ActividadAnalitica> DosParciales() =>
    [
        Actividad(1, ComponentesEvaluacion.ContactoDocente, 1, -30),
        Actividad(2, ComponentesEvaluacion.PracticoExperimental, 1, -29),
        Actividad(3, ComponentesEvaluacion.Autonomo, 1, -28),
        Actividad(4, ComponentesEvaluacion.ContactoDocente, 2, -10),
        Actividad(5, ComponentesEvaluacion.PracticoExperimental, 2, -9),
        Actividad(6, ComponentesEvaluacion.Autonomo, 2, -8)
    ];

    private static IEnumerable<CalificacionAnalitica> TodasCon(int estudianteId, decimal nota) =>
        Enumerable.Range(1, 6).Select(a => Nota(a, estudianteId, nota));

    [Fact]
    public void Estudiante_con_atrasos_recibe_recomendacion_de_contacto_con_la_evidencia()
    {
        var calificaciones = TodasCon(1, 9m).ToList();
        // El estudiante 2 solo entregó las 4 primeras: 2 vencidas sin entrega (RN-05).
        calificaciones.AddRange(Enumerable.Range(1, 4).Select(a => Nota(a, 2, 9m)));

        var resultado = Servicio.CalcularRecomendaciones(
            Contexto(DosParciales(), calificaciones, [Estudiante(1), Estudiante(2)]));

        var rec = Assert.Single(resultado.Recomendaciones, r => r.EstudianteId == 2);
        Assert.Equal("estudiante", rec.Tipo);
        Assert.Contains("Estudiante 2", rec.Accion);
        Assert.Contains("RN-05", rec.Indicador);
        Assert.Contains("2 actividades", rec.Justificacion);
        Assert.DoesNotContain(resultado.Recomendaciones, r => r.EstudianteId == 1);
    }

    [Fact]
    public void Actividad_bajo_el_umbral_genera_recomendacion_grupal_con_el_logro()
    {
        var calificaciones = new List<CalificacionAnalitica>();
        foreach (var est in new[] { 1, 2 })
        {
            calificaciones.AddRange(Enumerable.Range(1, 6)
                .Select(a => Nota(a, est, a == 3 ? 4m : 9m))); // Actividad 3: logro 40 %.
        }

        var resultado = Servicio.CalcularRecomendaciones(
            Contexto(DosParciales(), calificaciones, [Estudiante(1), Estudiante(2)]));

        var rec = Assert.Single(resultado.Recomendaciones, r => r.Tipo == "actividad");
        Assert.Equal(3, rec.ActividadId);
        Assert.Contains("Actividad 3", rec.Accion);
        Assert.Equal("logro 40.0 % < 60 %", rec.Indicador);
        Assert.Contains("60 %", rec.Justificacion);
    }

    [Fact]
    public void Nota_final_proyectada_bajo_14_genera_recomendacion_de_alta_prioridad()
    {
        // 6/10 en todo: parciales de 6.0 y nota final 12/20, sin alertas.
        var resultado = Servicio.CalcularRecomendaciones(
            Contexto(DosParciales(), TodasCon(1, 6m), [Estudiante(1)]));

        var rec = Assert.Single(resultado.Recomendaciones, r => r.EstudianteId == 1);
        Assert.Equal("alta", rec.Prioridad);
        Assert.Equal("nota final 12.00/20 < 14", rec.Indicador);
    }

    [Fact]
    public void Curso_sin_problemas_no_recibe_recomendaciones_genericas()
    {
        var calificaciones = TodasCon(1, 9m).Concat(TodasCon(2, 10m));

        var resultado = Servicio.CalcularRecomendaciones(
            Contexto(DosParciales(), calificaciones, [Estudiante(1), Estudiante(2)]));

        Assert.Empty(resultado.Recomendaciones);
        Assert.Empty(resultado.Advertencias);
    }

    [Fact]
    public void Sin_calificaciones_advierte_datos_insuficientes()
    {
        var futuras = new List<ActividadAnalitica> { Actividad(1, ComponentesEvaluacion.ContactoDocente, 1, 10) };

        var resultado = Servicio.CalcularRecomendaciones(Contexto(futuras, [], [Estudiante(1)]));

        Assert.Empty(resultado.Recomendaciones);
        Assert.Contains(resultado.Advertencias, a => a.StartsWith("Datos insuficientes"));
    }

    [Fact]
    public void Las_recomendaciones_de_alta_prioridad_van_primero()
    {
        var calificaciones = new List<CalificacionAnalitica>();
        calificaciones.AddRange(TodasCon(1, 6m)); // nota final 12 -> alta
        // Estudiante 2: notas altas y la actividad 3 baja para el grupo -> media.
        calificaciones.AddRange(Enumerable.Range(1, 6).Select(a => Nota(a, 2, a == 3 ? 5m : 10m)));

        var resultado = Servicio.CalcularRecomendaciones(
            Contexto(DosParciales(), calificaciones, [Estudiante(1), Estudiante(2)]));

        Assert.True(resultado.Recomendaciones.Count >= 2);
        var prioridades = resultado.Recomendaciones.Select(r => r.Prioridad).ToList();
        Assert.Equal(prioridades.OrderBy(p => p == "alta" ? 0 : 1), prioridades);
    }
}
