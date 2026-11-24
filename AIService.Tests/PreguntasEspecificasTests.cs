using AIService.Agente;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AIService.Tests;

/// <summary>
/// El agente se adapta a la pregunta (hallazgo de la revisión del 23-nov-2026): reconoce al estudiante nombrado,
/// también en preguntas de seguimiento, consulta su ficha, recibe la conversación previa y da una respuesta directa.
/// </summary>
public class PreguntasEspecificasTests
{
    private static readonly ContextoConsulta Contexto = new("jwt-del-docente", "consulta-test");

    private const string Nomina = """
        {"curso_id":1,"calificaciones":[
          {"estudiante_id":11,"nombre":"Ana Torres"},
          {"estudiante_id":12,"nombre":"María Gómez"},
          {"estudiante_id":13,"nombre":"Pedro Castillo"},
          {"estudiante_id":14,"nombre":"Ana Paredes"}]}
        """;

    private static object FichaAna() => new
    {
        nombre = "Ana Torres", escalaCalificacion = 20m, promedioActividades = 9.4m, notaParcial1 = 5.1m, notaParcial2 = 3.2m,
        notaFinal = 8.3m, actividadesTotales = 3, porcentajeFinalizacion = 66.67m, calificadas = 2, completadas = 0,
        sinEntrega = 1, pendientes = 0, notaCero = 0,
        actividades = new object[]
        {
            new { nombre = "Lección 3", componenteEtiquetaCorta = "Contacto", parcial = 2, nota = 2.5m, puntajeMaximo = 10m, estado = "calificada" },
            new { nombre = "Taller 2", componenteEtiquetaCorta = "Práctico", parcial = 2, nota = (decimal?)null, puntajeMaximo = 10m, estado = "sin_entrega" },
            new { nombre = "Tarea 1", componenteEtiquetaCorta = "Autónomo", parcial = 1, nota = 8m, puntajeMaximo = 10m, estado = "calificada" }
        },
        alertas = Array.Empty<object>()
    };

    private static (AgenteAcademico, ModeloSimulado, BackendSimulado) Crear()
    {
        var modelo = new ModeloSimulado();
        var backend = Datos.BackendCompleto()
            .Con("api/indicadores/curso/1/estudiantes/11", FichaAna())
            .Con("api/analisis/curso/1/recomendaciones", new
            {
                cursoId = 1, fechaCalculo = Datos.Corte, advertencias = Array.Empty<string>(),
                recomendaciones = new object[]
                {
                    new { tipo = "estudiante", prioridad = "alta", estudianteId = (int?)11,
                          accion = "Conversar con Ana Torres sobre sus últimas evaluaciones y ofrecerle una tutoría.",
                          justificacion = "En las últimas 3 evaluaciones promedia 2.5 sobre 10.", indicador = "RN-04" },
                    new { tipo = "actividad", prioridad = "media", estudianteId = (int?)null,
                          accion = "Retroalimentar al grupo los contenidos de «Lección 3».",
                          justificacion = "El logro promedio fue 52.0 %.", indicador = "logro 52.0 % < 60 %" },
                    new { tipo = "estudiante", prioridad = "alta", estudianteId = (int?)12,
                          accion = "Conversar con María Gómez sobre sus últimas evaluaciones.",
                          justificacion = "Promedia 4.2 sobre 10.", indicador = "RN-04" }
                }
            });
        var agente = new AgenteAcademico(modelo, new HerramientasAcademicas(backend), NullLogger<AgenteAcademico>.Instance,
            Options.Create(new AgenteOptions { RutaRapida = true }));
        return (agente, modelo, backend);
    }

    [Theory]
    [InlineData("¿Cómo va Ana Torres?", 11)]
    [InlineData("¿y maria como va?", 12)]          // sin tilde, solo el nombre (único)
    [InlineData("Tutoría para Castillo", 13)]       // solo el apellido
    [InlineData("¿Qué pasa con Ana?", null)]        // ambiguo: hay dos Ana
    [InlineData("Resumen del curso", null)]
    public void Reconoce_al_estudiante_nombrado(string texto, int? esperado)
    {
        Assert.Equal(esperado, AgenteAcademico.EstudianteMencionado([texto], Nomina));
    }

    [Theory]
    [InlineData("Sus notas bajaron: en las últimas 3 evaluaciones promedia 4.2 sobre 10.", "María Gómez: Sus notas bajaron: en las últimas 3 evaluaciones promedia 4.2 sobre 10.")]
    [InlineData("María Gómez bajó sus notas.", "María Gómez bajó sus notas.")]         // ya nombra a alguien
    [InlineData("4 estudiantes requieren atención.", "4 estudiantes requieren atención.")] // no es evidencia de una alerta
    public void Los_hechos_copiados_de_una_alerta_llevan_el_nombre_del_estudiante(string hecho, string esperado)
    {
        var alertas = new ResultadoHerramienta("identificar_alertas", "{}", true, 200,
            """
            {"alertas":[
              {"estudiante_id":12,"nombre":"María Gómez","evidencia":"María Gómez: Sus notas bajaron: en las últimas 3 evaluaciones promedia 4.2 sobre 10, cuando antes tenía 8.0."},
              {"estudiante_id":13,"nombre":"Pedro Castillo","evidencia":"Pedro Castillo: No entregó 2 actividades que ya vencieron."}]}
            """,
            null, 1, null);

        Assert.Equal(esperado, AgenteAcademico.ConSujeto(hecho, [alertas]));
    }

        [Theory]
    [InlineData("creame un deber rapido de 5 preguntas para la Leccion 2 para ayudar a Maria Gomez", true)]
    [InlineData("Genera 3 ejercicios del Taller 2", true)]
    [InlineData("hazme una guía de estudio para Ana Torres", true)]
    [InlineData("¿Qué estudiantes necesitan apoyo esta semana?", false)]
    [InlineData("¿Qué preguntas tuvieron más errores?", false)] // pregunta por datos, no pide crear
    public void Detecta_cuando_se_pide_crear_material(string mensaje, bool esperado)
    {
        Assert.Equal(esperado, AgenteAcademico.PideMaterial(mensaje));
    }

    [Fact]
    public async Task Pedir_un_deber_devuelve_material_generado_aparte_de_los_hechos_verificados()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde("""
            {
              "respuesta": "Deber de 5 preguntas sobre la Lección 3 para Ana Torres.",
              "material": { "titulo": "Deber: Lección 3", "items": [
                "¿Qué es una API REST?", "Nombra 2 métodos HTTP.", "¿Qué devuelve un código 404?",
                "Explica qué es JSON.", "Diseña 1 endpoint para listar cursos." ] },
              "hechos": [
                { "texto": "Ana Torres obtuvo 2.5/10 en la Lección 3.", "fuente": "obtener_calificaciones" },
                { "texto": "Ana Torres obtuvo 9.9/10 en la Lección 3.", "fuente": "obtener_calificaciones" }
              ],
              "recomendaciones_ids": []
            }
            """);

        var r = await agente.ResponderAsync(
            new PeticionChat("Créame un deber de 5 preguntas de la Lección 3 para Ana Torres", 1), Contexto, CancellationToken.None);

        Assert.Contains("crear material de apoyo", modelo.Recibidos[0][0].Contenido); // instrucciones del modo material
        Assert.NotNull(r.Material);
        Assert.Equal(5, r.Material!.Items.Count);  // el material no pasa por el verificador de cifras
        Assert.Equal(AgenteAcademico.NotaMaterial, r.Material.Nota);
        Assert.Equal("Deber de 5 preguntas sobre la Lección 3 para Ana Torres.", r.Respuesta);
        var hecho = Assert.Single(r.Hechos);         // los hechos sí se verifican: 9.9/10 no está en los datos
        Assert.Contains("2.5/10", hecho.Texto);
    }

    [Theory]
    [InlineData("creame un deber de 5 preguntas", 5)]
    [InlineData("Genera tres ejercicios del Taller 2", 3)]
    [InlineData("hazme una guía de estudio", null)]
    public void Detecta_la_cantidad_pedida(string mensaje, int? esperado)
    {
        Assert.Equal(esperado, AgenteAcademico.CantidadPedida(mensaje));
    }

    [Fact]
    public void Limpia_la_numeracion_y_respeta_la_cantidad()
    {
        var items = AgenteAcademico.LimpiarItems(["1. ¿Qué es HTML?", "2) Explica CSS.", "c- Define HTTP.", "4. Extra", "5. Extra"], 3);

        Assert.Equal(["¿Qué es HTML?", "Explica CSS.", "Define HTTP."], items);
    }

    [Fact]
    public async Task Las_preguntas_normales_no_devuelven_material()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde("""{ "respuesta": "Ana Torres necesita apoyo.", "hechos": [], "inferencias": [], "recomendaciones_ids": [1], "material": { "titulo": "x", "items": ["y"] } }""");

        var r = await agente.ResponderAsync(new PeticionChat("¿Cómo va Ana Torres?", 1), Contexto, CancellationToken.None);

        Assert.Null(r.Material);
    }

    [Theory]
    [InlineData("¿cuáles son los otros 4 estudiantes?", true)]
    [InlineData("¿y los demás?", true)]
    [InlineData("¿cómo va el resto del curso?", true)]
    [InlineData("¿y por cuál empiezo?", false)]
    [InlineData("¿qué temas debe tener la tutoría?", false)]
    public void Distingue_preguntas_sobre_el_grupo(string mensaje, bool esperado)
    {
        Assert.Equal(esperado, AgenteAcademico.PreguntaPorElGrupo(mensaje));
    }

    [Fact]
    public async Task Una_pregunta_sobre_el_grupo_no_hereda_el_estudiante_de_la_conversacion()
    {
        var (agente, modelo, backend) = Crear();
        modelo.Responde("""{ "respuesta": "Los otros son María Gómez y Pedro Castillo.", "hechos": [], "inferencias": [], "recomendaciones_ids": [] }""");
        var historial = new List<MensajeHistorial> { new("docente", "¿Qué temas debe tener la tutoría para Ana Torres?") };

        var r = await agente.ResponderAsync(
            new PeticionChat("¿cuáles son los otros estudiantes?", 1, historial), Contexto, CancellationToken.None);

        Assert.DoesNotContain(backend.Llamadas, l => l.Ruta.Contains("/estudiantes/"));  // no se consulta la ficha de Ana
        var aModelo = modelo.Recibidos[0][1].Contenido;
        Assert.Contains("María Gómez", aModelo);                                          // el modelo ve al grupo
        Assert.Contains("Pedro Castillo", aModelo);
    }

    [Fact]
    public void En_un_seguimiento_usa_el_estudiante_de_la_pregunta_anterior()
    {
        var id = AgenteAcademico.EstudianteMencionado(["¿Qué temas debe tener la tutoría?", "¿Qué necesita Ana Torres?"], Nomina);
        Assert.Equal(11, id);
    }

    [Fact]
    public async Task Pregunta_de_seguimiento_consulta_la_ficha_del_estudiante_y_envia_la_conversacion()
    {
        var (agente, modelo, backend) = Crear();
        modelo.Responde("""
            {
              "respuesta": "La tutoría de Ana Torres debería centrarse en la Lección 3 y el Taller 2 del parcial 2.",
              "hechos": [ { "texto": "Ana Torres obtuvo 2.5/10 en la Lección 3.", "fuente": "obtener_calificaciones" } ],
              "inferencias": [],
              "recomendaciones_ids": [],
              "recomendaciones_nuevas": [
                { "accion": "Repasar con Ana Torres los contenidos de la Lección 3.", "justificacion": "obtuvo 2.5/10" },
                { "accion": "Acordar con Ana Torres la entrega del Taller 2.", "justificacion": "está sin entrega" }
              ]
            }
            """);
        var historial = new List<MensajeHistorial>
        {
            new("docente", "¿Qué necesita Ana Torres?"),
            new("agente", "Hecho: Ana Torres tiene notas bajas.")
        };

        var r = await agente.ResponderAsync(
            new PeticionChat("¿Qué temas debe tener la tutoría?", 1, historial), Contexto, CancellationToken.None);

        Assert.Contains(backend.Llamadas, l => l.Ruta == "api/indicadores/curso/1/estudiantes/11");
        Assert.Contains(r.Traza, t => t.Argumentos.Contains("\"estudiante_id\":11"));
        var aModelo = modelo.Recibidos[0][1].Contenido;
        Assert.Contains("PREGUNTAS ANTERIORES DEL DOCENTE", aModelo);
        Assert.Contains("¿Qué necesita Ana Torres?", aModelo);
        // Las respuestas previas del asistente no se reenvían: un error no se arrastra a la siguiente respuesta.
        Assert.DoesNotContain("Hecho: Ana Torres tiene notas bajas.", aModelo);
        Assert.Contains("Lección 3", aModelo); // la ficha de Ana llega al modelo
        Assert.Equal("La tutoría de Ana Torres debería centrarse en la Lección 3 y el Taller 2 del parcial 2.", r.Respuesta);
        Assert.Equal(2, r.Recomendaciones.Count);
        Assert.StartsWith("Repasar con Ana Torres", r.Recomendaciones[0].Accion);
    }

    [Fact]
    public async Task En_una_pregunta_sobre_un_estudiante_el_modelo_no_recibe_datos_de_otros()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde("""{ "respuesta": "Ana Torres necesita apoyo.", "hechos": [], "inferencias": [], "recomendaciones_ids": [1] }""");

        var r = await agente.ResponderAsync(new PeticionChat("¿Cómo va Ana Torres?", 1), Contexto, CancellationToken.None);

        var aModelo = modelo.Recibidos[0][1].Contenido;
        Assert.DoesNotContain("María Gómez", aModelo);   // alertas y recomendaciones de otra estudiante
        Assert.DoesNotContain("Pedro Castillo", aModelo);
        Assert.DoesNotContain("actividades_bajo_60", aModelo); // datos por actividad del grupo
        Assert.Contains("Ana Torres", aModelo);
        Assert.Contains("Lección 3", aModelo);               // su ficha
        // La traza sigue mostrando todas las consultas.
        Assert.Contains(r.Traza, t => t.Herramienta == "identificar_alertas");
    }

    [Fact]
    public async Task La_tutoria_se_arma_con_sus_notas_mas_bajas_y_entregas_pendientes()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde($$"""{ "respuesta": "Conviene una tutoría.", "hechos": [], "inferencias": [], "recomendaciones_ids": [{{AgenteAcademico.IdTutoria}}] }""");

        var r = await agente.ResponderAsync(new PeticionChat("¿Qué temas debe tener la tutoría de Ana Torres?", 1), Contexto, CancellationToken.None);

        var rec = Assert.Single(r.Recomendaciones);
        Assert.Equal("Preparar una tutoría con Ana Torres centrada en Lección 3 y Tarea 1, y acordar la entrega de Taller 2.", rec.Accion);
        Assert.Equal("Sus notas más bajas: Lección 3 2.5/10, Tarea 1 8/10. Sin entregar: Taller 2.", rec.Justificacion);
        Assert.Contains("actividades_mas_bajas", modelo.Recibidos[0][1].Contenido);
    }

    [Fact]
    public async Task Respuesta_directa_con_cifras_inventadas_se_descarta()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde("""
            { "respuesta": "Ana Torres aprobará con 17.5 puntos.", "hechos": [], "inferencias": [], "recomendaciones_ids": [1] }
            """);

        var r = await agente.ResponderAsync(new PeticionChat("¿Cómo va Ana Torres?", 1), Contexto, CancellationToken.None);

        Assert.Null(r.Respuesta);
        Assert.Contains(r.Advertencias, a => a.Contains("Se descartaron 1"));
        Assert.Single(r.Recomendaciones);
    }

    [Fact]
    public async Task Recomendacion_propia_sin_respaldo_en_los_datos_se_descarta_y_se_mantienen_las_base()
    {
        var (agente, modelo, _) = Crear();
        modelo.Responde("""
            {
              "respuesta": "Ana Torres necesita apoyo.",
              "hechos": [], "inferencias": [],
              "recomendaciones_ids": [2],
              "recomendaciones_nuevas": [ { "accion": "Darle 3 horas de tutoría.", "justificacion": "promedia 1.1/10" } ]
            }
            """);

        var r = await agente.ResponderAsync(new PeticionChat("¿Cómo va Ana Torres?", 1), Contexto, CancellationToken.None);

        // La #2 es del grupo y no se le muestra al modelo en una pregunta sobre Ana. Sin una elección válida
        // se muestran la tutoría determinista y la recomendación de Ana (#1).
        Assert.Equal(2, r.Recomendaciones.Count);
        Assert.StartsWith("Preparar una tutoría con Ana Torres", r.Recomendaciones[0].Accion);
        Assert.All(r.Recomendaciones, x => Assert.Contains("Ana Torres", x.Accion));
    }
}
