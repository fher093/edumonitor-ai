using AIService.Agente;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AIService.Tests;

/// <summary>
/// Optimización de tiempo de respuesta con el modelo local: ruta rápida (una sola llamada al modelo) y
/// recomendaciones elegidas por número en lugar de redactadas.
/// </summary>
public class RutaRapidaTests
{
    private static readonly ContextoConsulta Contexto = new("jwt-del-docente", "consulta-test");

    private static (AgenteAcademico Agente, ModeloSimulado Modelo, BackendSimulado Backend) Crear(BackendSimulado? backend = null)
    {
        var modelo = new ModeloSimulado();
        var b = backend ?? Datos.BackendCompleto();
        var agente = new AgenteAcademico(modelo, new HerramientasAcademicas(b), NullLogger<AgenteAcademico>.Instance,
            Options.Create(new AgenteOptions { RutaRapida = true }));
        return (agente, modelo, b);
    }

    private const string RespuestaConIds = """
        {
          "hechos": [ { "texto": "Hay 2 alertas activas.", "fuente": "identificar_alertas" } ],
          "inferencias": [ "El grupo necesita apoyo en la Lección 3." ],
          "recomendaciones_ids": [2]
        }
        """;

    [Fact]
    public async Task Pregunta_sobre_el_curso_usa_una_sola_llamada_al_modelo_y_las_tres_herramientas()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde(RespuestaConIds);

        var r = await agente.ResponderAsync(new PeticionChat("Resumen del curso", 1), Contexto, CancellationToken.None);

        Assert.Equal("ok", r.Estado);
        Assert.Single(modelo.Recibidos); // solo la respuesta final
        Assert.Equal(["generar_resumen", "identificar_alertas", "obtener_progreso"], r.Traza.Select(t => t.Herramienta));
        Assert.All(r.Traza, t => Assert.Equal("orquestador", t.Origen));
        Assert.Empty(r.Advertencias);
    }

    [Fact]
    public async Task Las_recomendaciones_elegidas_por_numero_se_copian_exactas_del_backend()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde(RespuestaConIds);

        var r = await agente.ResponderAsync(new PeticionChat("¿Qué actividades debo reforzar?", 1), Contexto, CancellationToken.None);

        var rec = Assert.Single(r.Recomendaciones);
        Assert.StartsWith("Retroalimentar al grupo los contenidos de «Lección 3»", rec.Accion);
        Assert.Contains("52.0 %", rec.Justificacion);
        // El modelo recibió las recomendaciones numeradas y el esquema con recomendaciones_ids.
        Assert.Contains("\"id\":2", modelo.Recibidos[0][1].Contenido);
        Assert.Contains("recomendaciones_ids", modelo.Recibidos[0][0].Contenido);
    }

    [Fact]
    public async Task Si_el_modelo_no_elige_ninguna_valida_se_muestran_las_dos_de_mayor_prioridad()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde("""{ "hechos": [], "inferencias": [], "recomendaciones_ids": [99] }""");

        var r = await agente.ResponderAsync(new PeticionChat("Resumen del curso", 1), Contexto, CancellationToken.None);

        Assert.Equal(2, r.Recomendaciones.Count);
        Assert.Contains("María Gómez", r.Recomendaciones[0].Accion);
    }

    [Fact]
    public async Task Preguntas_sobre_estudiantes_agregan_las_calificaciones()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde(RespuestaConIds);

        var r = await agente.ResponderAsync(new PeticionChat("¿Qué estudiantes necesitan apoyo?", 1), Contexto, CancellationToken.None);

        Assert.Contains(r.Traza, t => t.Herramienta == "obtener_calificaciones");
        Assert.Single(modelo.Recibidos);
    }

    [Fact]
    public async Task Curso_ajeno_responde_sin_acceso_sin_llamar_al_modelo()
    {
        var (agente, modelo, _) = Crear();

        var r = await agente.ResponderAsync(new PeticionChat("Muéstrame el curso 9999", 1), Contexto, CancellationToken.None);

        Assert.Equal("sin_acceso", r.Estado);
        Assert.Empty(modelo.Recibidos);
        Assert.Single(r.Traza); // se detiene en la primera herramienta denegada
    }

    [Fact]
    public async Task Preguntas_sobre_otros_cursos_usan_el_ciclo_en_el_que_el_modelo_elige()
    {
        var (agente, modelo, _) = Crear();
        modelo.PideHerramientas(("listar_cursos_autorizados", "{}")).Responde("").Responde(
            """{ "hechos": [ { "texto": "Tienes 1 curso.", "fuente": "listar_cursos_autorizados" } ], "inferencias": [], "recomendaciones": [] }""");

        var r = await agente.ResponderAsync(new PeticionChat("¿Qué cursos tengo este período?", 1), Contexto, CancellationToken.None);

        Assert.Equal("listar_cursos_autorizados", r.Traza[0].Herramienta);
        Assert.Equal("modelo", r.Traza[0].Origen);
    }

    [Theory]
    [InlineData("Resumen del curso", 1, 3)]
    [InlineData("¿Quién no ha entregado?", 1, 4)]
    [InlineData("¿Cómo van las notas del curso 7?", 1, 4)]
    public void El_plan_rapido_elige_las_herramientas_segun_la_pregunta(string pregunta, int curso, int herramientas)
    {
        var plan = AgenteAcademico.PlanRapido(new PeticionChat(pregunta, curso));

        Assert.NotNull(plan);
        Assert.Equal(herramientas, plan!.Count);
    }

    [Theory]
    [InlineData("¿Qué cursos tengo?", 1)]
    [InlineData("Muéstrame mis cursos del período 2026-B", 1)]
    [InlineData("Resumen", null)]
    public void Sin_plan_rapido_cuando_hace_falta_que_el_modelo_elija(string pregunta, int? curso)
    {
        Assert.Null(AgenteAcademico.PlanRapido(new PeticionChat(pregunta, curso)));
    }
}
