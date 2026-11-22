using System.Text.Json;
using AIService.Agente;

namespace AIService.Tests;

/// <summary>Modelo de lenguaje simulado: devuelve respuestas guionizadas y registra lo que recibió.</summary>
public sealed class ModeloSimulado : IModeloLenguaje
{
    private readonly Queue<Func<IReadOnlyList<MensajeChat>, MensajeChat>> _guion = new();

    public string NombreModelo => "modelo-simulado";
    public List<IReadOnlyList<MensajeChat>> Recibidos { get; } = [];
    public bool Caido { get; set; }

    /// <summary>El modelo pide estas herramientas (nombre, argumentos JSON).</summary>
    public ModeloSimulado PideHerramientas(params (string Nombre, string Args)[] llamadas)
    {
        _guion.Enqueue(_ => new MensajeChat("assistant", string.Empty,
            llamadas.Select(l => new LlamadaHerramienta(l.Nombre, JsonDocument.Parse(l.Args).RootElement.Clone())).ToList()));
        return this;
    }

    /// <summary>El modelo responde texto (sin herramientas).</summary>
    public ModeloSimulado Responde(string contenido)
    {
        _guion.Enqueue(_ => new MensajeChat("assistant", contenido, []));
        return this;
    }

    public Task<MensajeChat> ChatAsync(
        IReadOnlyList<MensajeChat> mensajes, IReadOnlyList<DefinicionHerramienta>? herramientas,
        JsonElement? formato, CancellationToken ct)
    {
        if (Caido) throw new ModeloNoDisponibleException("Ollama no responde (simulado).");
        Recibidos.Add(mensajes.ToList());
        // Sin guion: el modelo deja de pedir herramientas y no produce JSON.
        var respuesta = _guion.Count > 0 ? _guion.Dequeue()(mensajes) : new MensajeChat("assistant", string.Empty, []);
        return Task.FromResult(respuesta);
    }
}

/// <summary>Backend simulado: rutas → respuestas; registra cada llamada con la herramienta usada.</summary>
public sealed class BackendSimulado : IBackendAcademico
{
    private readonly Dictionary<string, RespuestaBackend> _rutas = new(StringComparer.OrdinalIgnoreCase);

    public List<(string Ruta, string Herramienta, string Token)> Llamadas { get; } = [];
    public bool Caido { get; set; }

    public BackendSimulado Con(string ruta, object cuerpo, int codigo = 200)
    {
        _rutas[ruta] = new RespuestaBackend(codigo, JsonSerializer.SerializeToElement(cuerpo, Datos.Json));
        return this;
    }

    public BackendSimulado ConCodigo(string ruta, int codigo)
    {
        _rutas[ruta] = new RespuestaBackend(codigo, null);
        return this;
    }

    public Task<RespuestaBackend> GetAsync(string ruta, string herramienta, ContextoConsulta contexto, CancellationToken ct)
    {
        Llamadas.Add((ruta, herramienta, contexto.Token));
        if (Caido) return Task.FromResult(new RespuestaBackend(503, null));
        // Un curso que el backend no conoce para este docente devuelve 403, como la API real.
        return Task.FromResult(_rutas.TryGetValue(ruta, out var r) ? r : new RespuestaBackend(403, null));
    }
}

/// <summary>Respuestas del backend con la misma forma (camelCase) que la API real, para el curso 1.</summary>
public static class Datos
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly DateTime Corte = new(2026, 11, 20, 15, 30, 0, DateTimeKind.Utc);

    public static object Estudiante(int id, string nombre, decimal? promedio, decimal? final, int sinEntrega, int alertas) => new
    {
        estudianteId = id, identificacion = $"180000000{id}", nombre, correo = $"e{id}@uta.edu.ec",
        promedioActividades = promedio, notaParcial1 = final / 2, notaParcial2 = final / 2, notaFinal = final,
        calificadas = 5, notaCero = 0, sinEntrega, pendientes = 0, completadasSinNota = 0,
        porcentajeFinalizacion = sinEntrega > 0 ? 60m : 100m, alertas
    };

    public static object Dashboard(bool conNotas = true) => new
    {
        cursoId = 1, cursoNombre = "Metodologías Ágiles", cursoCodigo = "SW-401", periodo = "2026-B",
        escalaCalificacion = 20m, escalaNotaFinal = 20m, fechaCalculo = Corte,
        totalMatriculados = 3, actividadesTotales = 8, actividadesEvaluadas = conNotas ? 7 : 0,
        estudiantesConNota = conNotas ? 3 : 0, promedioCurso = conNotas ? 13.4m : (decimal?)null,
        desviacionEstandar = conNotas ? 3.1m : (decimal?)null, aprobadosProyectados = 1, estudiantesEnRiesgo = 2,
        distribucion = new[] { new { rango = "0-59", etiqueta = "0 % – 59 % (crítico)", cantidad = 1, porcentaje = 33.3m } },
        progreso = new
        {
            totalEsperado = 24, completadas = 18, calificadas = 16, notaCero = 1, sinEntrega = 4, pendientes = 2,
            actividadesVencidas = 21, porcentajeAvance = 75m, porcentajeFinalizacionVencidas = 78.5m
        },
        componentes = Array.Empty<object>(),
        parciales = Array.Empty<object>(),
        estudiantes = conNotas
            ? new[]
            {
                Estudiante(11, "Ana Torres", 18.2m, 18.1m, 0, 0),
                Estudiante(12, "María Gómez", 11.5m, 11.2m, 0, 1),
                Estudiante(13, "Pedro Castillo", 9.8m, null, 2, 2)
            }
            : new[]
            {
                Estudiante(11, "Ana Torres", null, null, 0, 0)
            },
        advertencias = Array.Empty<string>()
    };

    public static object Alertas() => new
    {
        cursoId = 1, cursoNombre = "Metodologías Ágiles", cursoCodigo = "SW-401", periodo = "2026-B",
        escalaCalificacion = 20m, fechaCalculo = Corte,
        alertas = new[]
        {
            new { tipo = "descenso", tipoEtiqueta = "Descenso de rendimiento", regla = "RN-04 · Descenso en las últimas evaluaciones",
                  estudianteId = 12, identificacion = "1800000012", nombre = "María Gómez",
                  evidencia = "Sus notas bajaron: en las últimas 3 evaluaciones promedia 4.2 sobre 10, cuando antes tenía 8.0. Se avisa cuando la baja supera 1 punto.",
                  severidad = "alta", fechaCalculo = Corte },
            new { tipo = "atraso", tipoEtiqueta = "Atraso en entregas", regla = "RN-05 · Actividades vencidas sin entrega",
                  estudianteId = 13, identificacion = "1800000013", nombre = "Pedro Castillo",
                  evidencia = "No entregó 2 actividades que ya vencieron: Lección 2 y Taller 3.",
                  severidad = "media", fechaCalculo = Corte }
        },
        resumen = new { total = 2, descenso = 1, atraso = 1, bajaFinalizacion = 0, estudiantesAfectados = 2 },
        reglas = Array.Empty<object>(),
        advertencias = Array.Empty<string>()
    };

    public static object Analisis() => new
    {
        cursoId = 1, fechaCalculo = Corte, tendenciaGeneral = "descenso", variacionTendencia = -12.5m,
        detalleActividades = new[]
        {
            new { actividadId = 3, nombre = "Lección 3", parcial = 2, promedioPorcentaje = 52.0m, resultadoBajo = true },
            new { actividadId = 1, nombre = "Taller 1", parcial = 1, promedioPorcentaje = 85.0m, resultadoBajo = false }
        },
        hallazgos = new[]
        {
            "1 de 7 actividades evaluadas presentan un promedio inferior al 60 %.",
            "La actividad con menor rendimiento es «Lección 3» (52.0 % de logro)."
        }
    };

    public static object Recomendaciones() => new
    {
        cursoId = 1, fechaCalculo = Corte,
        recomendaciones = new[]
        {
            new { tipo = "estudiante", prioridad = "alta", accion = "Conversar con María Gómez sobre las dificultades de sus últimas evaluaciones y ofrecerle una tutoría.",
                  justificacion = "Sus notas bajaron: en las últimas 3 evaluaciones promedia 4.2 sobre 10, cuando antes tenía 8.0.", indicador = "RN-04" },
            new { tipo = "actividad", prioridad = "media", accion = "Retroalimentar al grupo los contenidos de «Lección 3» (parcial 2) antes de la siguiente evaluación.",
                  justificacion = "El logro promedio fue 52.0 % con 3 estudiante(s) calificados, por debajo del umbral del 60 %.", indicador = "logro 52.0 % < 60 %" }
        },
        advertencias = Array.Empty<string>()
    };

    public static object Cursos() => new[]
    {
        new { id = 1, codigo = "SW-401", nombre = "Metodologías Ágiles", periodo = "2026-B", totalEstudiantes = 3 }
    };

    /// <summary>Backend con el curso 1 completo.</summary>
    public static BackendSimulado BackendCompleto() => new BackendSimulado()
        .Con("api/cursos", Cursos())
        .Con("api/indicadores/curso/1", Dashboard())
        .Con("api/alertas/curso/1", Alertas())
        .Con("api/analisis/curso/1", Analisis())
        .Con("api/analisis/curso/1/recomendaciones", Recomendaciones());
}
