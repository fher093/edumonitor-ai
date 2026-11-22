using AIService.Agente;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIService.Tests;

/// <summary>
/// Pruebas del orquestador del agente (TT-10 / TT-11) con un modelo y un backend simulados:
/// uso de herramientas (HU-12, HU-13), formato de la respuesta (HU-14), recomendaciones justificadas (HU-15),
/// permisos (RN-12) y comportamiento ante fallos (RN-08, RN-09).
/// </summary>
public class AgenteTests
{
    private static readonly ContextoConsulta Contexto = new("jwt-del-docente", "consulta-test");

    private const string RespuestaValida = """
        {
          "hechos": [
            { "texto": "2 estudiantes tienen alertas activas.", "fuente": "identificar_alertas" },
            { "texto": "María Gómez promedia 4.2 sobre 10 en las últimas 3 evaluaciones (antes 8.0).", "fuente": "identificar_alertas" },
            { "texto": "El promedio del curso es 13.4/20.", "fuente": "generar_resumen" }
          ],
          "inferencias": [ "El descenso de María se concentra en las últimas evaluaciones." ],
          "recomendaciones": [
            { "accion": "Ofrecer una tutoría a María Gómez.", "justificacion": "Descenso de 8.0 a 4.2 sobre 10 (RN-04)." },
            { "accion": "Retroalimentar la Lección 3.", "justificacion": "logro 52.0 % < 60 %" }
          ]
        }
        """;

    private static (AgenteAcademico Agente, ModeloSimulado Modelo, BackendSimulado Backend) Crear(BackendSimulado? backend = null)
    {
        var modelo = new ModeloSimulado();
        var b = backend ?? Datos.BackendCompleto();
        var agente = new AgenteAcademico(modelo, new HerramientasAcademicas(b), NullLogger<AgenteAcademico>.Instance);
        return (agente, modelo, b);
    }

    [Fact]
    public async Task Pregunta_general_usa_tres_herramientas_y_separa_hechos_inferencias_y_recomendaciones()
    {
        var (agente, modelo, backend) = Crear();
        modelo
            .PideHerramientas(
                ("generar_resumen", """{"curso_id":1}"""),
                ("identificar_alertas", """{"curso_id":1,"tipo_alerta":"todas"}"""),
                ("obtener_progreso", """{"curso_id":1}"""))
            .Responde("Listo.")         // fin de la fase de herramientas
            .Responde(RespuestaValida); // respuesta estructurada

        var r = await agente.ResponderAsync(new PeticionChat("¿Qué estudiantes necesitan apoyo?", 1), Contexto, CancellationToken.None);

        Assert.Equal("ok", r.Estado);
        Assert.Equal(3, r.Hechos.Count);
        Assert.Single(r.Inferencias);
        Assert.Equal(2, r.Recomendaciones.Count);
        Assert.Equal(["generar_resumen", "identificar_alertas", "obtener_progreso"], r.Fuentes);
        Assert.Equal(3, r.Traza.Count);
        Assert.All(r.Traza, t => Assert.True(t.Exito));
        Assert.All(r.Traza, t => Assert.Equal("modelo", t.Origen)); // el modelo pidió las tres: no hay complemento
        Assert.Equal(Datos.Corte, r.FechaCorte);
        Assert.Empty(r.Advertencias);
        // Todas las consultas al backend llevan el JWT del docente y el nombre de la herramienta (RN-12, HU-17).
        Assert.All(backend.Llamadas, l => Assert.Equal("jwt-del-docente", l.Token));
        Assert.Contains(backend.Llamadas, l => l.Herramienta == "generar_resumen" && l.Ruta.EndsWith("/recomendaciones"));
    }

    [Fact]
    public async Task Las_herramientas_no_envian_cedula_ni_correo_al_modelo()
    {
        var (agente, modelo, _) = Crear();
        modelo.PideHerramientas(("obtener_calificaciones", """{"curso_id":1}""")).Responde("").Responde(RespuestaValida);

        await agente.ResponderAsync(new PeticionChat("Notas del curso", 1), Contexto, CancellationToken.None);

        var todo = string.Join("\n", modelo.Recibidos.SelectMany(m => m).Select(m => m.Contenido));
        Assert.Contains("María Gómez", todo);
        Assert.DoesNotContain("1800000012", todo);
        Assert.DoesNotContain("@uta.edu.ec", todo);
    }

    [Fact]
    public async Task Curso_ajeno_responde_sin_acceso_y_no_inventa_datos()
    {
        var (agente, modelo, _) = Crear();
        modelo.PideHerramientas(("generar_resumen", """{"curso_id":999}""")).Responde("");

        var r = await agente.ResponderAsync(new PeticionChat("Muéstrame el curso 999", 1), Contexto, CancellationToken.None);

        Assert.Equal("sin_acceso", r.Estado);
        Assert.Equal("No tienes acceso a ese curso.", r.Mensaje);
        Assert.Empty(r.Hechos);
        Assert.Empty(r.Recomendaciones);
        Assert.Equal(403, Assert.Single(r.Traza).CodigoHttp);
        Assert.Equal(2, modelo.Recibidos.Count); // no se pidió la respuesta final al modelo
    }

    [Fact]
    public async Task Backend_caido_avisa_el_error_sin_responder_de_memoria()
    {
        var backend = Datos.BackendCompleto();
        backend.Caido = true;
        var (agente, modelo, _) = Crear(backend);
        modelo.PideHerramientas(("identificar_alertas", """{"curso_id":1}""")).Responde("");

        var r = await agente.ResponderAsync(new PeticionChat("¿Hay alertas?", 1), Contexto, CancellationToken.None);

        Assert.Equal("error_herramientas", r.Estado);
        Assert.Contains("no respondo sin datos", r.Mensaje);
        Assert.Empty(r.Hechos);
        Assert.False(Assert.Single(r.Traza).Exito);
    }

    [Fact]
    public async Task Si_el_modelo_no_usa_herramientas_se_consultan_resumen_y_alertas()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde("Creo que el curso va bien.").Responde(RespuestaValida);

        var r = await agente.ResponderAsync(new PeticionChat("¿Cómo va el curso?", 1), Contexto, CancellationToken.None);

        Assert.Equal("ok", r.Estado);
        Assert.Equal(["generar_resumen", "identificar_alertas", "obtener_progreso"], r.Traza.Select(t => t.Herramienta));
        Assert.All(r.Traza, t => Assert.Equal("orquestador", t.Origen));
        Assert.Contains(r.Advertencias, a => a.Contains("no pidió herramientas"));
    }

    [Fact]
    public async Task Hecho_con_cifra_inventada_se_descarta()
    {
        var (agente, modelo, _) = Crear();
        modelo.PideHerramientas(("identificar_alertas", """{"curso_id":1}""")).Responde("").Responde("""
            {
              "hechos": [
                { "texto": "Pedro Castillo no entregó 2 actividades.", "fuente": "identificar_alertas" },
                { "texto": "El 87 % del curso aprobará con 19.5 de nota.", "fuente": "identificar_alertas" }
              ],
              "inferencias": [ "Unos 45 estudiantes están en riesgo." ],
              "recomendaciones": [ { "accion": "Contactar a Pedro Castillo.", "justificacion": "2 actividades vencidas sin entrega" } ]
            }
            """);

        var r = await agente.ResponderAsync(new PeticionChat("¿Hay alertas?", 1), Contexto, CancellationToken.None);

        var hecho = Assert.Single(r.Hechos);
        Assert.Contains("Pedro Castillo", hecho.Texto);
        Assert.Empty(r.Inferencias);
        Assert.Single(r.Recomendaciones);
        Assert.Contains(r.Advertencias, a => a.Contains("Se descartaron 2"));
    }

    [Fact]
    public async Task Fuente_que_no_se_consulto_se_elimina()
    {
        var (agente, modelo, _) = Crear();
        modelo.PideHerramientas(("identificar_alertas", """{"curso_id":1}""")).Responde("").Responde("""
            { "hechos": [ { "texto": "Hay 2 alertas.", "fuente": "base_de_datos_secreta" } ], "inferencias": [], "recomendaciones": [] }
            """);

        var r = await agente.ResponderAsync(new PeticionChat("¿Alertas?", 1), Contexto, CancellationToken.None);

        Assert.Null(Assert.Single(r.Hechos).Fuente);
    }

    [Fact]
    public async Task Si_el_modelo_no_devuelve_json_se_responde_con_los_datos_deterministas()
    {
        var (agente, modelo, _) = Crear();
        modelo.PideHerramientas(("generar_resumen", """{"curso_id":1}"""), ("identificar_alertas", """{"curso_id":1}"""))
            .Responde("")
            .Responde("Lo siento, no sé responder en JSON.")
            .Responde("Sigo sin poder.");

        var r = await agente.ResponderAsync(new PeticionChat("Resumen del curso", 1), Contexto, CancellationToken.None);

        Assert.Equal("ok", r.Estado);
        Assert.NotEmpty(r.Hechos);
        Assert.Contains(r.Hechos, h => h.Texto.Contains("Lección 3"));
        Assert.Contains(r.Recomendaciones, x => x.Justificacion.Contains("logro 52.0 % < 60 %"));
        Assert.Contains(r.Advertencias, a => a.Contains("no devolvió una respuesta estructurada"));
    }

    [Fact]
    public async Task Curso_sin_calificaciones_responde_datos_insuficientes()
    {
        var backend = new BackendSimulado().Con("api/indicadores/curso/1", Datos.Dashboard(conNotas: false));
        var (agente, modelo, _) = Crear(backend);
        modelo.PideHerramientas(("obtener_calificaciones", """{"curso_id":1}""")).Responde("").Responde("""
            { "hechos": [ { "texto": "Datos insuficientes: no hay calificaciones registradas.", "fuente": "obtener_calificaciones" } ], "inferencias": [], "recomendaciones": [] }
            """);

        var r = await agente.ResponderAsync(new PeticionChat("¿Cómo van las notas?", 1), Contexto, CancellationToken.None);

        Assert.Equal("sin_datos", r.Estado);
        Assert.StartsWith("Datos insuficientes", r.Mensaje);
        Assert.Empty(r.Recomendaciones);
    }

    [Fact]
    public async Task Herramienta_desconocida_o_sin_curso_id_falla_sin_llamar_al_backend()
    {
        var (agente, modelo, backend) = Crear();
        modelo.PideHerramientas(("borrar_notas", """{"curso_id":1}"""), ("identificar_alertas", "{}")).Responde("");

        var r = await agente.ResponderAsync(new PeticionChat("Borra las notas", 1), Contexto, CancellationToken.None);

        Assert.Equal("error_herramientas", r.Estado);
        Assert.Empty(backend.Llamadas);
        Assert.Contains(r.Traza, t => t.Error!.Contains("no existe"));
        Assert.Contains(r.Traza, t => t.Error!.Contains("curso_id"));
    }

    [Fact]
    public async Task La_misma_herramienta_con_los_mismos_argumentos_se_ejecuta_una_sola_vez()
    {
        var (agente, modelo, backend) = Crear();
        modelo
            .PideHerramientas(("identificar_alertas", """{"curso_id":1}"""))
            .PideHerramientas(("identificar_alertas", """{"curso_id":1}"""))
            .Responde("")
            .Responde(RespuestaValida);

        var r = await agente.ResponderAsync(new PeticionChat("¿Alertas?", 1), Contexto, CancellationToken.None);

        Assert.Single(r.Traza, t => t.Herramienta == "identificar_alertas");
        Assert.Single(backend.Llamadas, l => l.Ruta == "api/alertas/curso/1");
    }

    [Fact]
    public async Task El_ciclo_de_herramientas_se_detiene_tras_el_maximo_de_rondas()
    {
        var (agente, modelo, _) = Crear();
        for (var i = 0; i < 10; i++) modelo.PideHerramientas(("obtener_progreso", $$"""{"curso_id":1,"ronda":{{i}}}"""));

        var r = await agente.ResponderAsync(new PeticionChat("Progreso", 1), Contexto, CancellationToken.None);

        Assert.Equal(AgenteAcademico.MaxRondas, r.Traza.Count(t => t.Origen == "modelo"));
    }

    [Fact]
    public async Task Si_el_modelo_usa_una_sola_herramienta_base_se_completa_el_analisis_del_curso()
    {
        var (agente, modelo, _) = Crear();
        modelo.PideHerramientas(("identificar_alertas", """{"curso_id":1,"tipo_alerta":"todas"}""")).Responde("").Responde(RespuestaValida);

        var r = await agente.ResponderAsync(new PeticionChat("¿Quién necesita apoyo?", 1), Contexto, CancellationToken.None);

        Assert.Equal(["identificar_alertas", "generar_resumen", "obtener_progreso"], r.Traza.Select(t => t.Herramienta));
        Assert.Equal(["modelo", "orquestador", "orquestador"], r.Traza.Select(t => t.Origen));
        Assert.Equal(3, r.Fuentes.Count);
    }

    [Fact]
    public async Task La_consulta_de_un_estudiante_no_se_completa_con_el_analisis_del_curso()
    {
        var backend = Datos.BackendCompleto().Con("api/indicadores/curso/1/estudiantes/12", new
        {
            nombre = "María Gómez", escalaCalificacion = 20m, promedioActividades = 11.5m, notaParcial1 = 6m, notaParcial2 = 5.2m,
            notaFinal = 11.2m, actividadesTotales = 8, porcentajeFinalizacion = 100m, calificadas = 8, completadas = 0,
            sinEntrega = 0, pendientes = 0, notaCero = 0, actividades = Array.Empty<object>(), alertas = Array.Empty<object>()
        });
        var (agente, modelo, _) = Crear(backend);
        modelo.PideHerramientas(("obtener_progreso", """{"curso_id":1,"estudiante_id":12}""")).Responde("").Responde(RespuestaValida);

        var r = await agente.ResponderAsync(new PeticionChat("¿Cómo va María?", 1), Contexto, CancellationToken.None);

        Assert.Single(r.Traza);
    }

    [Fact]
    public async Task Modelo_caido_lanza_ModeloNoDisponible()
    {
        var (agente, modelo, _) = Crear();
        modelo.Caido = true;

        await Assert.ThrowsAsync<ModeloNoDisponibleException>(() =>
            agente.ResponderAsync(new PeticionChat("Hola", 1), Contexto, CancellationToken.None));
    }

    [Fact]
    public async Task El_prompt_indica_el_curso_activo_y_las_reglas_de_no_inventar()
    {
        var (agente, modelo, _) = Crear();
        modelo.PideHerramientas(("identificar_alertas", """{"curso_id":1}""")).Responde("").Responde(RespuestaValida);

        await agente.ResponderAsync(new PeticionChat("¿Alertas?", 7), Contexto, CancellationToken.None);

        var sistema = modelo.Recibidos[0][0];
        Assert.Equal("system", sistema.Rol);
        Assert.Contains("curso_id=7", sistema.Contenido);
        Assert.Contains("Nunca inventes", sistema.Contenido);
    }
}
