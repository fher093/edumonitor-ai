using Backend.Domain;
using Backend.Dtos;
using Backend.Services;

namespace Backend.Tests;

/// <summary>
/// Pruebas deterministas de indicadores, notas UTA, alertas y análisis grupal
/// (TT-08: los cálculos no dependen del modelo de lenguaje).
/// </summary>
public class AnaliticaServiceTests
{
    private static readonly DateTime Ahora = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly AnaliticaService Servicio = new();

    private static EstudianteAnalitica Estudiante(int id = 1) =>
        new(id, $"170000000{id}", $"Estudiante {id}", $"estudiante{id}@uta.edu.ec");

    private static ContextoAnalitica Contexto(
        IEnumerable<ActividadAnalitica> actividades,
        IEnumerable<CalificacionAnalitica> calificaciones,
        IEnumerable<EstudianteAnalitica>? estudiantes = null,
        IReadOnlyList<ReglaAlerta>? reglas = null,
        decimal escala = 20m)
    {
        var lista = estudiantes?.ToList() ?? [Estudiante()];
        return new ContextoAnalitica(
            new CursoAnalitica(1, "Curso de prueba", "TEST-01", "2026-B", escala),
            lista,
            actividades.ToList(),
            calificaciones.ToList(),
            reglas ?? [],
            Ahora);
    }

    private static ActividadAnalitica Actividad(
        int id, string componente, int parcial, int dias, decimal maximo = 10m) =>
        new(id, $"Actividad {id}", "Tarea", componente, parcial, Ahora.AddDays(dias), maximo);

    private static CalificacionAnalitica Nota(int actividadId, int estudianteId, decimal nota) =>
        new(actividadId, estudianteId, nota,
            nota == 0m ? EstadosActividad.NotaCero : EstadosActividad.Calificada, null);

    private static CalificacionAnalitica Estado(int actividadId, int estudianteId, string estado) =>
        new(actividadId, estudianteId, null, estado, null);

    private static List<ActividadAnalitica> ParcialCompleto(int parcial, int offset, decimal nota) =>
    [
        Actividad(offset + 1, ComponentesEvaluacion.ContactoDocente, parcial, -20 + offset, 10m),
        Actividad(offset + 2, ComponentesEvaluacion.PracticoExperimental, parcial, -19 + offset, 10m),
        Actividad(offset + 3, ComponentesEvaluacion.Autonomo, parcial, -18 + offset, 10m)
    ];

    [Fact]
    public void Calcula_parciales_con_ponderacion_40_30_30_y_nota_final_sobre_20()
    {
        var actividades = new List<ActividadAnalitica>();
        actividades.AddRange(ParcialCompleto(1, 0, 0m));
        actividades.AddRange(ParcialCompleto(2, 10, 0m));

        var calificaciones = new List<CalificacionAnalitica>
        {
            Nota(1, 1, 10m), // contacto  -> 10.0 * 40 % = 4.0
            Nota(2, 1, 5m),  // práctico  ->  5.0 * 30 % = 1.5
            Nota(3, 1, 8m),  // autónomo  ->  8.0 * 30 % = 2.4  => 7.9
            Nota(11, 1, 10m),
            Nota(12, 1, 10m),
            Nota(13, 1, 10m) // parcial 2 = 10.0
        };

        var dashboard = Servicio.CalcularDashboard(Contexto(actividades, calificaciones));
        var alumno = Assert.Single(dashboard.Estudiantes);

        Assert.Equal(7.9m, alumno.NotaParcial1);
        Assert.Equal(10m, alumno.NotaParcial2);
        Assert.Equal(17.9m, alumno.NotaFinal); // sobre 20
        Assert.Equal(1, dashboard.AprobadosProyectados); // >= 14 aprueba
        Assert.Equal(2, dashboard.Parciales.Count);
        Assert.All(dashboard.Parciales, p => Assert.True(p.Completo));
        Assert.Equal(6, dashboard.Componentes.Count);
        Assert.Equal(5, dashboard.Distribucion.Count);
    }

    [Fact]
    public void Nota_final_es_nula_si_falta_un_componente_del_parcial()
    {
        var actividades = new List<ActividadAnalitica>();
        actividades.AddRange(ParcialCompleto(1, 0, 0m));
        actividades.AddRange(ParcialCompleto(2, 10, 0m));

        // Parcial 1 completo y parcial 2 sin el componente autónomo.
        var calificaciones = new List<CalificacionAnalitica>
        {
            Nota(1, 1, 10m), Nota(2, 1, 10m), Nota(3, 1, 10m),
            Nota(11, 1, 10m), Nota(12, 1, 10m)
        };

        var dashboard = Servicio.CalcularDashboard(Contexto(actividades, calificaciones));
        var alumno = Assert.Single(dashboard.Estudiantes);

        Assert.Equal(10m, alumno.NotaParcial1);
        Assert.Null(alumno.NotaParcial2);
        Assert.Null(alumno.NotaFinal);
        Assert.False(dashboard.Parciales.Single(p => p.Parcial == 2).Completo);
        Assert.Contains(dashboard.Advertencias, a => a.Contains("parciales"));
    }

    [Fact]
    public void Actividad_vencida_sin_registro_es_sin_entrega_y_futura_es_pendiente()
    {
        var actividades = new List<ActividadAnalitica>
        {
            Actividad(1, ComponentesEvaluacion.Autonomo, 1, -2),
            Actividad(2, ComponentesEvaluacion.Autonomo, 1, 5)
        };

        var ficha = Servicio.CalcularFicha(Contexto(actividades, []), 1);

        Assert.NotNull(ficha);
        Assert.Equal(EstadosActividad.SinEntrega,
            ficha!.Actividades.Single(a => a.ActividadId == 1).Estado);
        Assert.Equal(EstadosActividad.Pendiente,
            ficha.Actividades.Single(a => a.ActividadId == 2).Estado);
        Assert.Equal(1, ficha.SinEntrega);
        Assert.Equal(1, ficha.Pendientes);
        Assert.Equal(0m, ficha.PorcentajeFinalizacion);
    }

    [Fact]
    public void Estado_completada_cuenta_como_finalizada_pero_no_como_nota()
    {
        var actividades = new List<ActividadAnalitica>
        {
            Actividad(1, ComponentesEvaluacion.Autonomo, 1, -2)
        };

        var contexto = Contexto(actividades,
            [Estado(1, 1, EstadosActividad.Completada)]);

        var ficha = Servicio.CalcularFicha(contexto, 1);

        Assert.NotNull(ficha);
        Assert.Equal(1, ficha!.Completadas);
        Assert.Equal(100m, ficha.PorcentajeFinalizacion);
        Assert.Null(ficha.PromedioActividades);
        Assert.Equal(EstadosActividad.Completada, ficha.Actividades[0].Estado);
    }

    [Fact]
    public void Alerta_de_descenso_incluye_evidencia_verificable()
    {
        var actividades = Enumerable.Range(1, 4)
            .Select(i => Actividad(i, ComponentesEvaluacion.Autonomo, 1, -10 + i))
            .ToList();

        var calificaciones = new List<CalificacionAnalitica>
        {
            Nota(1, 1, 10m), Nota(2, 1, 10m), Nota(3, 1, 10m), Nota(4, 1, 5m)
        };

        var alertas = Servicio.CalcularAlertasCurso(Contexto(actividades, calificaciones));

        var alerta = Assert.Single(alertas.Alertas, a => a.Tipo == "descenso");
        Assert.Contains("RN-04", alerta.Regla);
        Assert.Contains("caída", alerta.Evidencia);
        Assert.Equal("alta", alerta.Severidad);
        Assert.Equal(1, alertas.Resumen.Descenso);
    }

    [Fact]
    public void Alerta_de_atraso_y_baja_finalizacion_usan_actividades_vencidas()
    {
        var actividades = Enumerable.Range(1, 4)
            .Select(i => Actividad(i, ComponentesEvaluacion.Autonomo, 1, -10 + i))
            .ToList();

        var calificaciones = new List<CalificacionAnalitica>
        {
            Nota(1, 1, 5m),
            Nota(2, 1, 6m),
            Estado(3, 1, EstadosActividad.SinEntrega),
            Estado(4, 1, EstadosActividad.SinEntrega)
        };

        var alertas = Servicio.CalcularAlertasCurso(Contexto(actividades, calificaciones));

        var atraso = Assert.Single(alertas.Alertas, a => a.Tipo == "atraso");
        Assert.Contains("2 actividades vencidas", atraso.Evidencia);

        var finalizacion = Assert.Single(alertas.Alertas, a => a.Tipo == "baja_finalizacion");
        Assert.Contains("50.0 %", finalizacion.Evidencia);
        Assert.Equal(1, alertas.Resumen.EstudiantesAfectados);
    }

    [Fact]
    public void Actividades_futuras_sin_entrega_no_generan_alertas()
    {
        var actividades = new List<ActividadAnalitica>
        {
            Actividad(1, ComponentesEvaluacion.Autonomo, 1, 5),
            Actividad(2, ComponentesEvaluacion.Autonomo, 1, 6)
        };

        var alertas = Servicio.CalcularAlertasCurso(Contexto(actividades, []));

        Assert.Empty(alertas.Alertas);
        Assert.Equal(0, alertas.Resumen.Total);
    }

    [Fact]
    public void Reglas_desactivadas_no_generan_alertas()
    {
        var actividades = Enumerable.Range(1, 4)
            .Select(i => Actividad(i, ComponentesEvaluacion.Autonomo, 1, -10 + i))
            .ToList();

        var calificaciones = new List<CalificacionAnalitica>
        {
            Nota(1, 1, 10m), Nota(2, 1, 10m), Nota(3, 1, 10m), Nota(4, 1, 2m)
        };

        var reglas = AnaliticaService.ReglasPorDefecto(Ahora)
            .Select(r => new ReglaAlerta
            {
                Id = r.Id, Tipo = r.Tipo, Nombre = r.Nombre,
                ParametrosJson = r.ParametrosJson, Activa = false,
                FechaActualizacion = r.FechaActualizacion
            })
            .ToList();

        var alertas = Servicio.CalcularAlertasCurso(Contexto(actividades, calificaciones, reglas: reglas));

        Assert.Empty(alertas.Alertas);
        Assert.Contains(alertas.Advertencias, a => a.Contains("desactivadas"));
    }

    [Fact]
    public void Analisis_grupal_detecta_tendencia_de_mejora_y_actividades_bajas()
    {
        var actividades = Enumerable.Range(1, 4)
            .Select(i => Actividad(i, ComponentesEvaluacion.Autonomo, 1, -10 + i))
            .ToList();

        // Promedios por actividad: 50 %, 60 %, 80 % y 90 % de logro.
        var calificaciones = new List<CalificacionAnalitica>
        {
            Nota(1, 1, 5m), Nota(2, 1, 6m), Nota(3, 1, 8m), Nota(4, 1, 9m)
        };

        var analisis = Servicio.CalcularAnalisis(Contexto(actividades, calificaciones));

        Assert.Equal("mejora", analisis.TendenciaGeneral);
        Assert.Equal(30m, analisis.VariacionTendencia);
        Assert.Equal(1, analisis.ActividadesBajas); // solo 50 % queda bajo el umbral de 60 %
        Assert.All(analisis.Tendencias, t => Assert.NotNull(t.PromedioPorcentaje));
        Assert.NotEmpty(analisis.Hallazgos);
    }

    [Fact]
    public void Escala_de_curso_distinta_de_20_se_respeta_en_el_promedio()
    {
        var actividades = new List<ActividadAnalitica>
        {
            Actividad(1, ComponentesEvaluacion.Autonomo, 1, -2, 10m)
        };

        var dashboard = Servicio.CalcularDashboard(
            Contexto(actividades, [Nota(1, 1, 9m)], escala: 10m));

        Assert.Equal(9m, dashboard.PromedioCurso);
        var bucket = dashboard.Distribucion.Single(d => d.Rango == "90-100");
        Assert.Equal(1, bucket.Cantidad);
        Assert.Equal(100m, bucket.Porcentaje);
    }

    [Fact]
    public void Ficha_de_estudiante_no_matriculado_devuelve_null()
    {
        var actividades = new List<ActividadAnalitica>
        {
            Actividad(1, ComponentesEvaluacion.Autonomo, 1, -2)
        };

        var ficha = Servicio.CalcularFicha(Contexto(actividades, []), 999);

        Assert.Null(ficha);
    }
}
