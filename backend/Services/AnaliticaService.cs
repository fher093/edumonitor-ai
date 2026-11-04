using System.Globalization;
using System.Text.Json;
using Backend.Domain;
using Backend.Dtos;

namespace Backend.Services;

public sealed record CursoAnalitica(
    int Id,
    string Nombre,
    string Codigo,
    string Periodo,
    decimal EscalaCalificacion);

public sealed record EstudianteAnalitica(
    int Id,
    string Identificacion,
    string Nombre,
    string Correo);

public sealed record ActividadAnalitica(
    int Id,
    string Nombre,
    string Tipo,
    string Componente,
    int Parcial,
    DateTime? FechaEntrega,
    decimal PuntajeMaximo);

public sealed record CalificacionAnalitica(
    int ActividadId,
    int EstudianteId,
    decimal? Nota,
    string? Estado,
    string? Observacion);

public sealed record ContextoAnalitica(
    CursoAnalitica Curso,
    IReadOnlyList<EstudianteAnalitica> Estudiantes,
    IReadOnlyList<ActividadAnalitica> Actividades,
    IReadOnlyList<CalificacionAnalitica> Calificaciones,
    IReadOnlyList<ReglaAlerta> Reglas,
    DateTime Ahora);

/// <summary>
/// Cálculos deterministas de indicadores, alertas y análisis grupal (RN-03).
/// No usa el modelo de lenguaje: recibe datos ya autorizados y devuelve métricas verificables.
/// </summary>
public sealed class AnaliticaService
{
    public const decimal UmbralResultadoBajo = 60m;
    public const decimal UmbralAprobacionFinal = 14m;
    public const decimal EscalaNotaFinal = 20m;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private sealed record ItemEvaluacion(
        ActividadAnalitica Actividad,
        string Estado,
        decimal? Nota,
        decimal? Porcentaje,
        bool Vencida)
    {
        public bool EsCompletada => EstadosActividad.EsCompletada(Estado);
    }

    private sealed record EvaluacionEstudiante(
        EstudianteAnalitica Estudiante,
        IReadOnlyList<ItemEvaluacion> Items,
        decimal? PromedioActividades,
        decimal? NotaParcial1,
        decimal? NotaParcial2,
        decimal? NotaFinal,
        bool Parcial1Completo,
        bool Parcial2Completo,
        IReadOnlyList<ComponenteEstudianteDto> Componentes,
        int Calificadas,
        int NotaCero,
        int SinEntrega,
        int Pendientes,
        int CompletadasSinNota,
        decimal? PorcentajeFinalizacion);

    public IReadOnlyList<ReglaAlerta> ReglasEfectivas(ContextoAnalitica contexto) =>
        contexto.Reglas.Count > 0 ? contexto.Reglas : ReglasPorDefecto(contexto.Ahora);

    public static IReadOnlyList<ReglaAlerta> ReglasPorDefecto(DateTime ahora) =>
    [
        new ReglaAlerta
        {
            Id = 0, Tipo = "descenso", Nombre = "Descenso en las últimas evaluaciones",
            ParametrosJson = "{\"k\":3,\"delta\":1.0}", Activa = true, FechaActualizacion = ahora
        },
        new ReglaAlerta
        {
            Id = 0, Tipo = "atraso", Nombre = "Actividades vencidas sin entrega",
            ParametrosJson = "{\"n\":2}", Activa = true, FechaActualizacion = ahora
        },
        new ReglaAlerta
        {
            Id = 0, Tipo = "baja_finalizacion", Nombre = "Baja finalización de actividades",
            ParametrosJson = "{\"p\":60}", Activa = true, FechaActualizacion = ahora
        }
    ];

    // ---------------------------------------------------------------------
    // Dashboard del curso (HU-07 / RF-05)
    // ---------------------------------------------------------------------
    public DashboardCursoDto CalcularDashboard(ContextoAnalitica contexto)
    {
        var evaluaciones = CalcularEvaluaciones(contexto);
        var reglas = ReglasEfectivas(contexto);
        var alertas = CalcularAlertas(contexto, evaluaciones, reglas);
        var conNota = evaluaciones.Where(e => e.PromedioActividades.HasValue).ToList();

        var promedio = conNota.Count == 0
            ? (decimal?)null
            : Redondear(conNota.Average(e => e.PromedioActividades!.Value));

        var desviacion = CalcularDesviacion(conNota.Select(e => e.PromedioActividades!.Value));

        var advertencias = new List<string>();
        if (contexto.Estudiantes.Count == 0) advertencias.Add("No hay estudiantes matriculados en el curso.");
        if (contexto.Actividades.Count == 0) advertencias.Add("No hay actividades registradas en el curso.");
        if (contexto.Actividades.Count > 0 && conNota.Count == 0)
            advertencias.Add("No hay calificaciones registradas: los indicadores se muestran como datos insuficientes.");
        var sinNota = evaluaciones.Count - conNota.Count;
        if (sinNota > 0 && conNota.Count > 0)
            advertencias.Add($"{sinNota} estudiante(s) aún no tienen calificaciones registradas.");
        if (evaluaciones.Any(e => !e.Parcial1Completo || !e.Parcial2Completo) && conNota.Count > 0)
            advertencias.Add("Uno o más parciales tienen componentes sin datos; la nota final solo se calcula cuando los tres componentes del parcial están completos.");

        var progreso = ConstruirProgreso(contexto, evaluaciones);
        var componentes = ConstruirResumenComponentes(contexto);
        var parciales = ConstruirResumenParciales(evaluaciones);
        var distribucion = ConstruirDistribucion(conNota, contexto.Curso.EscalaCalificacion);
        var estudiantes = evaluaciones
            .Select(e => new EstudianteResumenDto(
                e.Estudiante.Id,
                e.Estudiante.Identificacion,
                e.Estudiante.Nombre,
                e.Estudiante.Correo,
                e.PromedioActividades,
                e.NotaParcial1,
                e.NotaParcial2,
                e.NotaFinal,
                e.Calificadas,
                e.NotaCero,
                e.SinEntrega,
                e.Pendientes,
                e.CompletadasSinNota,
                e.PorcentajeFinalizacion,
                alertas.Count(a => a.EstudianteId == e.Estudiante.Id)))
            .OrderBy(e => e.Nombre)
            .ToList();

        var aprobados = evaluaciones.Count(e => e.NotaFinal is not null && e.NotaFinal.Value >= UmbralAprobacionFinal);
        var enRiesgo = alertas.Select(a => a.EstudianteId).Distinct().Count();
        var actividadesEvaluadas = contexto.Actividades.Count(a =>
            contexto.Calificaciones.Any(c => c.ActividadId == a.Id && EstadosActividad.EsCalificada(c.Estado)));

        return new DashboardCursoDto(
            contexto.Curso.Id,
            contexto.Curso.Nombre,
            contexto.Curso.Codigo,
            contexto.Curso.Periodo,
            contexto.Curso.EscalaCalificacion,
            EscalaNotaFinal,
            contexto.Ahora,
            contexto.Estudiantes.Count,
            contexto.Actividades.Count,
            actividadesEvaluadas,
            conNota.Count,
            promedio,
            desviacion,
            aprobados,
            enRiesgo,
            distribucion,
            progreso,
            componentes,
            parciales,
            estudiantes,
            advertencias);
    }

    // ---------------------------------------------------------------------
    // Ficha individual (HU-08 / RF-06)
    // ---------------------------------------------------------------------
    public FichaEstudianteDto? CalcularFicha(ContextoAnalitica contexto, int estudianteId)
    {
        var evaluaciones = CalcularEvaluaciones(contexto);
        var evaluacion = evaluaciones.FirstOrDefault(e => e.Estudiante.Id == estudianteId);
        if (evaluacion is null) return null;

        var reglas = ReglasEfectivas(contexto);
        var alertas = CalcularAlertas(contexto, evaluaciones, reglas)
            .Where(a => a.EstudianteId == estudianteId)
            .ToList();

        var actividades = evaluacion.Items
            .Select(i => new ActividadEstudianteDto(
                i.Actividad.Id,
                i.Actividad.Nombre,
                i.Actividad.Tipo,
                i.Actividad.Componente,
                ComponentesEvaluacion.Etiqueta(i.Actividad.Componente),
                ComponentesEvaluacion.EtiquetaCorta(i.Actividad.Componente),
                i.Actividad.Parcial,
                i.Actividad.FechaEntrega,
                i.Actividad.PuntajeMaximo,
                i.Nota,
                i.Porcentaje,
                i.Estado,
                EstadosActividad.Etiqueta(i.Estado),
                string.Empty,
                i.Vencida))
            .ToList();

        // Completar observaciones desde los registros explícitos.
        var observaciones = contexto.Calificaciones
            .Where(c => c.EstudianteId == estudianteId)
            .ToDictionary(c => c.ActividadId, c => c.Observacion);
        for (var i = 0; i < actividades.Count; i++)
        {
            if (observaciones.TryGetValue(actividades[i].ActividadId, out var obs) && !string.IsNullOrWhiteSpace(obs))
                actividades[i] = actividades[i] with { Observacion = obs };
        }

        var evolucion = evaluacion.Items
            .Where(i => i.Nota.HasValue)
            .OrderBy(i => i.Actividad.FechaEntrega ?? DateTime.MaxValue)
            .ThenBy(i => i.Actividad.Id)
            .Select(i => new EvolucionNotaDto(
                i.Actividad.Id,
                i.Actividad.Nombre,
                i.Actividad.FechaEntrega,
                i.Nota,
                i.Nota.HasValue && i.Actividad.PuntajeMaximo > 0
                    ? Redondear(i.Nota.Value / i.Actividad.PuntajeMaximo * 10m)
                    : null,
                i.Estado))
            .ToList();

        return new FichaEstudianteDto(
            contexto.Curso.Id,
            contexto.Curso.Nombre,
            contexto.Curso.Codigo,
            contexto.Curso.Periodo,
            contexto.Curso.EscalaCalificacion,
            evaluacion.Estudiante.Id,
            evaluacion.Estudiante.Identificacion,
            evaluacion.Estudiante.Nombre,
            evaluacion.Estudiante.Correo,
            evaluacion.PromedioActividades,
            evaluacion.NotaParcial1,
            evaluacion.NotaParcial2,
            evaluacion.NotaFinal,
            evaluacion.Parcial1Completo,
            evaluacion.Parcial2Completo,
            evaluacion.Items.Count,
            evaluacion.Calificadas,
            evaluacion.NotaCero,
            evaluacion.SinEntrega,
            evaluacion.Pendientes,
            evaluacion.CompletadasSinNota,
            evaluacion.PorcentajeFinalizacion,
            evaluacion.Componentes.ToList(),
            actividades,
            evolucion,
            alertas);
    }

    // ---------------------------------------------------------------------
    // Análisis grupal (HU-11 / RF-08)
    // ---------------------------------------------------------------------
    public AnalisisGrupalDto CalcularAnalisis(ContextoAnalitica contexto)
    {
        var evaluaciones = CalcularEvaluaciones(contexto);
        var reglas = ReglasEfectivas(contexto);
        var alertas = CalcularAlertas(contexto, evaluaciones, reglas);

        var actividades = contexto.Actividades
            .OrderBy(a => a.FechaEntrega ?? DateTime.MaxValue)
            .ThenBy(a => a.Id)
            .Select(a =>
            {
                var items = evaluaciones.SelectMany(e => e.Items).Where(i => i.Actividad.Id == a.Id).ToList();
                var conNota = items.Where(i => i.Nota.HasValue).ToList();
                var promedioNota = conNota.Count == 0 ? (decimal?)null : Redondear(conNota.Average(i => i.Nota!.Value));
                var promedioPct = conNota.Count == 0
                    ? (decimal?)null
                    : Redondear(conNota.Average(i => i.Porcentaje ?? 0m));
                var completadas = items.Count(i => i.EsCompletada);
                var pctFinalizacion = items.Count == 0
                    ? (decimal?)null
                    : Redondear((decimal)completadas / items.Count * 100m);

                return new ActividadAnalisisDto(
                    a.Id,
                    a.Nombre,
                    a.Componente,
                    ComponentesEvaluacion.EtiquetaCorta(a.Componente),
                    a.Parcial,
                    a.FechaEntrega,
                    a.PuntajeMaximo,
                    items.Count,
                    conNota.Count,
                    promedioNota,
                    promedioPct,
                    pctFinalizacion,
                    promedioPct is not null && promedioPct.Value < UmbralResultadoBajo);
            })
            .ToList();

        var tendencias = ConstruirTendencias(actividades);
        var (tendenciaGeneral, variacion) = CalcularTendenciaGeneral(actividades);
        var componentes = ConstruirResumenComponentes(contexto);
        var conNota = evaluaciones.Where(e => e.PromedioActividades.HasValue).ToList();
        var promedioCurso = conNota.Count == 0
            ? (decimal?)null
            : Redondear(conNota.Average(e => e.PromedioActividades!.Value));
        var desviacion = CalcularDesviacion(conNota.Select(e => e.PromedioActividades!.Value));
        var enRiesgo = alertas.Select(a => a.EstudianteId).Distinct().Count();
        var bajas = actividades.Count(a => a.ResultadoBajo);

        var hallazgos = new List<string>();
        if (actividades.Count > 0)
        {
            var evaluadas = actividades.Count(a => a.Calificados > 0);
            hallazgos.Add($"{bajas} de {evaluadas} actividades evaluadas presentan un promedio inferior al {UmbralResultadoBajo:0} %.");
            var peor = actividades
                .Where(a => a.PromedioPorcentaje.HasValue)
                .OrderBy(a => a.PromedioPorcentaje)
                .FirstOrDefault();
            if (peor is not null)
                hallazgos.Add($"La actividad con menor rendimiento es «{peor.Nombre}» ({peor.PromedioPorcentaje:0.0} % de logro).");
            var componenteBajo = componentes
                .Where(c => c.PromedioSobre10.HasValue)
                .OrderBy(c => c.PromedioSobre10)
                .FirstOrDefault();
            if (componenteBajo is not null)
                hallazgos.Add($"El componente con menor promedio es {componenteBajo.EtiquetaCorta} del parcial {componenteBajo.Parcial} ({componenteBajo.PromedioSobre10:0.00}/10).");
            hallazgos.Add($"La tendencia general del curso es {tendenciaGeneral}{(variacion is null ? "" : $" ({variacion:+0.0;-0.0;0.0} puntos porcentuales)")}.");
        }
        if (enRiesgo > 0)
            hallazgos.Add($"{enRiesgo} estudiante(s) requieren atención según las reglas de alerta configuradas.");

        var advertencias = new List<string>();
        if (contexto.Actividades.Count == 0) advertencias.Add("No hay actividades registradas: no es posible analizar tendencias.");
        if (contexto.Actividades.Count > 0 && conNota.Count == 0) advertencias.Add("No hay calificaciones registradas: datos insuficientes para el análisis grupal.");
        if (contexto.Estudiantes.Count == 0) advertencias.Add("No hay estudiantes matriculados en el curso.");

        return new AnalisisGrupalDto(
            contexto.Curso.Id,
            contexto.Curso.Nombre,
            contexto.Curso.Codigo,
            contexto.Curso.Periodo,
            contexto.Curso.EscalaCalificacion,
            contexto.Ahora,
            contexto.Estudiantes.Count,
            contexto.Actividades.Count,
            bajas,
            enRiesgo,
            promedioCurso,
            desviacion,
            tendenciaGeneral,
            variacion,
            actividades,
            componentes,
            tendencias,
            hallazgos,
            advertencias);
    }

    // ---------------------------------------------------------------------
    // Alertas (HU-10 / RF-07)
    // ---------------------------------------------------------------------
    public AlertasCursoDto CalcularAlertasCurso(ContextoAnalitica contexto)
    {
        var evaluaciones = CalcularEvaluaciones(contexto);
        var reglas = ReglasEfectivas(contexto);
        var alertas = CalcularAlertas(contexto, evaluaciones, reglas);

        var advertencias = new List<string>();
        if (contexto.Actividades.Count == 0) advertencias.Add("No hay actividades registradas: no se pueden evaluar las reglas de alerta.");
        if (contexto.Estudiantes.Count == 0) advertencias.Add("No hay estudiantes matriculados en el curso.");
        if (reglas.All(r => !r.Activa)) advertencias.Add("Todas las reglas de alerta están desactivadas.");

        var reglasDto = reglas
            .OrderBy(r => r.Tipo)
            .Select(r => new ReglaAlertaDto(r.Id, r.Tipo, r.Nombre, r.ParametrosJson, r.Activa, r.FechaActualizacion))
            .ToList();

        return new AlertasCursoDto(
            contexto.Curso.Id,
            contexto.Curso.Nombre,
            contexto.Curso.Codigo,
            contexto.Curso.Periodo,
            contexto.Curso.EscalaCalificacion,
            contexto.Ahora,
            alertas,
            new ResumenAlertasDto(
                alertas.Count,
                alertas.Count(a => a.Tipo == "descenso"),
                alertas.Count(a => a.Tipo == "atraso"),
                alertas.Count(a => a.Tipo == "baja_finalizacion"),
                alertas.Select(a => a.EstudianteId).Distinct().Count()),
            reglasDto,
            advertencias);
    }

    private List<AlertaDto> CalcularAlertas(
        ContextoAnalitica contexto,
        IReadOnlyList<EvaluacionEstudiante> evaluaciones,
        IReadOnlyList<ReglaAlerta> reglas)
    {
        var alertas = new List<AlertaDto>();
        var activas = reglas.Where(r => r.Activa).ToList();
        var reglaDescenso = activas.FirstOrDefault(r => r.Tipo == "descenso");
        var reglaAtraso = activas.FirstOrDefault(r => r.Tipo == "atraso");
        var reglaFinalizacion = activas.FirstOrDefault(r => r.Tipo == "baja_finalizacion");

        foreach (var evaluacion in evaluaciones.OrderBy(e => e.Estudiante.Nombre))
        {
            if (reglaDescenso is not null &&
                IntentarDescenso(evaluacion, reglaDescenso, contexto.Ahora, out var evidenciaDescenso))
            {
                alertas.Add(CrearAlerta("descenso", "RN-04", "Descenso en las últimas evaluaciones", "alta",
                    evaluacion, evidenciaDescenso, contexto.Ahora));
            }

            if (reglaAtraso is not null &&
                IntentarAtraso(evaluacion, reglaAtraso, out var evidenciaAtraso))
            {
                alertas.Add(CrearAlerta("atraso", "RN-05", "Actividades vencidas sin entrega", "media",
                    evaluacion, evidenciaAtraso, contexto.Ahora));
            }

            if (reglaFinalizacion is not null &&
                IntentarBajaFinalizacion(evaluacion, reglaFinalizacion, out var evidenciaFinalizacion))
            {
                alertas.Add(CrearAlerta("baja_finalizacion", "RN-06", "Baja finalización de actividades", "alta",
                    evaluacion, evidenciaFinalizacion, contexto.Ahora));
            }
        }

        return alertas;
    }

    private static AlertaDto CrearAlerta(
        string tipo, string reglaCodigo, string reglaNombre, string severidad,
        EvaluacionEstudiante evaluacion, string evidencia, DateTime ahora) =>
        new(
            tipo,
            EtiquetaTipoAlerta(tipo),
            $"{reglaCodigo} · {reglaNombre}",
            evaluacion.Estudiante.Id,
            evaluacion.Estudiante.Identificacion,
            evaluacion.Estudiante.Nombre,
            evidencia,
            severidad,
            ahora);

    private static string EtiquetaTipoAlerta(string tipo) => tipo switch
    {
        "descenso" => "Descenso de rendimiento",
        "atraso" => "Atraso en entregas",
        "baja_finalizacion" => "Baja finalización",
        _ => tipo
    };

    private static bool IntentarDescenso(
        EvaluacionEstudiante evaluacion, ReglaAlerta regla, DateTime ahora, out string evidencia)
    {
        evidencia = string.Empty;
        var k = (int)Parametro(regla, "k", 3);
        var delta = Parametro(regla, "delta", 1.0m);
        if (k < 1) k = 3;

        var conNota = evaluacion.Items
            .Where(i => i.Nota.HasValue && i.Actividad.PuntajeMaximo > 0)
            .OrderBy(i => i.Actividad.FechaEntrega ?? DateTime.MaxValue)
            .ThenBy(i => i.Actividad.Id)
            .ToList();

        if (conNota.Count < k + 1) return false;

        var ultimas = conNota.Skip(conNota.Count - k).ToList();
        var anteriores = conNota.Take(conNota.Count - k).ToList();
        var promedioUltimas = ultimas.Average(i => i.Nota!.Value / i.Actividad.PuntajeMaximo * 10m);
        var promedioAnteriores = anteriores.Average(i => i.Nota!.Value / i.Actividad.PuntajeMaximo * 10m);

        if (promedioAnteriores - promedioUltimas <= delta) return false;

        evidencia = string.Format(Inv,
            "El promedio de las últimas {0} evaluaciones es {1:0.00}/10, frente a {2:0.00}/10 de las anteriores; la caída de {3:0.00} puntos supera el umbral Δ={4:0.0}.",
            k, promedioUltimas, promedioAnteriores, promedioAnteriores - promedioUltimas, delta);
        return true;
    }

    private static bool IntentarAtraso(
        EvaluacionEstudiante evaluacion, ReglaAlerta regla, out string evidencia)
    {
        evidencia = string.Empty;
        var n = (int)Parametro(regla, "n", 2);
        if (n < 1) n = 2;

        var sinEntrega = evaluacion.Items
            .Where(i => i.Vencida && i.Estado == EstadosActividad.SinEntrega)
            .OrderBy(i => i.Actividad.FechaEntrega)
            .ToList();

        if (sinEntrega.Count < n) return false;

        var nombres = string.Join(", ", sinEntrega.Take(4).Select(i => $"«{i.Actividad.Nombre}»"));
        if (sinEntrega.Count > 4) nombres += "…";
        evidencia = string.Format(Inv,
            "Registra {0} actividades vencidas sin entrega (umbral n={1}): {2}.",
            sinEntrega.Count, n, nombres);
        return true;
    }

    private static bool IntentarBajaFinalizacion(
        EvaluacionEstudiante evaluacion, ReglaAlerta regla, out string evidencia)
    {
        evidencia = string.Empty;
        var p = Parametro(regla, "p", 60m);
        if (p <= 0 || p > 100) p = 60m;

        var vencidas = evaluacion.Items.Where(i => i.Vencida).ToList();
        if (vencidas.Count == 0) return false;

        var completadas = vencidas.Count(i => i.EsCompletada);
        var porcentaje = (decimal)completadas / vencidas.Count * 100m;
        if (porcentaje >= p) return false;

        evidencia = string.Format(Inv,
            "Finalización de actividades vencidas: {0:0.0} % ({1} de {2}); el umbral mínimo configurado es p={3:0.0} %.",
            porcentaje, completadas, vencidas.Count, p);
        return true;
    }

    private static decimal Parametro(ReglaAlerta? regla, string nombre, decimal porDefecto)
    {
        if (regla is null || string.IsNullOrWhiteSpace(regla.ParametrosJson)) return porDefecto;
        try
        {
            using var doc = JsonDocument.Parse(regla.ParametrosJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(nombre, out var propiedad) &&
                propiedad.TryGetDecimal(out var valor))
            {
                return valor;
            }
        }
        catch (JsonException)
        {
            // Parámetros inválidos: se usan los valores por defecto deterministas.
        }
        return porDefecto;
    }

    // ---------------------------------------------------------------------
    // Cálculos auxiliares
    // ---------------------------------------------------------------------
    private List<EvaluacionEstudiante> CalcularEvaluaciones(ContextoAnalitica contexto)
    {
        var calificaciones = contexto.Calificaciones
            .GroupBy(c => (c.ActividadId, c.EstudianteId))
            .ToDictionary(g => g.Key, g => g.First());

        return contexto.Estudiantes
            .Select(estudiante =>
            {
                var items = contexto.Actividades
                    .Select(actividad =>
                    {
                        calificaciones.TryGetValue((actividad.Id, estudiante.Id), out var registro);
                        var estado = EstadosActividad.Resolver(
                            registro?.Estado, registro?.Nota, actividad.FechaEntrega, contexto.Ahora);
                        var nota = estado switch
                        {
                            EstadosActividad.NotaCero => 0m,
                            EstadosActividad.Calificada => registro?.Nota,
                            _ => (decimal?)null
                        };
                        var porcentaje = nota.HasValue && actividad.PuntajeMaximo > 0
                            ? nota.Value / actividad.PuntajeMaximo * 100m
                            : (decimal?)null;
                        var vencida = actividad.FechaEntrega is not null && actividad.FechaEntrega.Value <= contexto.Ahora;
                        return new ItemEvaluacion(actividad, estado, nota, porcentaje, vencida);
                    })
                    .OrderBy(i => i.Actividad.FechaEntrega ?? DateTime.MaxValue)
                    .ThenBy(i => i.Actividad.Id)
                    .ToList();

                var conNota = items.Where(i => i.Nota.HasValue).ToList();
                decimal? promedio = null;
                if (conNota.Count > 0)
                {
                    var sumaMax = conNota.Sum(i => i.Actividad.PuntajeMaximo);
                    if (sumaMax > 0)
                        promedio = Redondear(conNota.Sum(i => i.Nota!.Value) / sumaMax * contexto.Curso.EscalaCalificacion);
                }

                var componentes = ConstruirComponentesEstudiante(items);
                var p1 = CalcularParcial(componentes, 1);
                var p2 = CalcularParcial(componentes, 2);
                var p1Completo = componentes.Where(c => c.Parcial == 1).All(c => c.TieneDatos);
                var p2Completo = componentes.Where(c => c.Parcial == 2).All(c => c.TieneDatos);
                var notaFinal = p1Completo && p2Completo && p1.HasValue && p2.HasValue
                    ? Redondear(p1.Value + p2.Value)
                    : (decimal?)null;

                var vencidas = items.Where(i => i.Vencida).ToList();
                var porcentajeFinalizacion = vencidas.Count == 0
                    ? (decimal?)null
                    : Redondear((decimal)vencidas.Count(i => i.EsCompletada) / vencidas.Count * 100m);

                return new EvaluacionEstudiante(
                    estudiante,
                    items,
                    promedio,
                    p1,
                    p2,
                    notaFinal,
                    p1Completo,
                    p2Completo,
                    componentes,
                    items.Count(i => i.Estado == EstadosActividad.Calificada),
                    items.Count(i => i.Estado == EstadosActividad.NotaCero),
                    items.Count(i => i.Estado == EstadosActividad.SinEntrega),
                    items.Count(i => i.Estado == EstadosActividad.Pendiente),
                    items.Count(i => i.Estado == EstadosActividad.Completada),
                    porcentajeFinalizacion);
            })
            .ToList();
    }

    private static List<ComponenteEstudianteDto> ConstruirComponentesEstudiante(IReadOnlyList<ItemEvaluacion> items)
    {
        var componentes = new List<ComponenteEstudianteDto>();
        foreach (var parcial in new[] { 1, 2 })
        {
            foreach (var componente in ComponentesEvaluacion.Todos)
            {
                var delComponente = items
                    .Where(i => i.Actividad.Parcial == parcial && i.Actividad.Componente == componente)
                    .ToList();
                var conNota = delComponente.Where(i => i.Nota.HasValue).ToList();
                decimal? promedio = null;
                if (conNota.Count > 0)
                {
                    var sumaMax = conNota.Sum(i => i.Actividad.PuntajeMaximo);
                    if (sumaMax > 0)
                        promedio = Redondear(conNota.Sum(i => i.Nota!.Value) / sumaMax * 10m);
                }

                componentes.Add(new ComponenteEstudianteDto(
                    componente,
                    ComponentesEvaluacion.Etiqueta(componente),
                    ComponentesEvaluacion.EtiquetaCorta(componente),
                    parcial,
                    ComponentesEvaluacion.Ponderacion(componente),
                    conNota.Count,
                    promedio,
                    conNota.Count > 0));
            }
        }
        return componentes;
    }

    private static decimal? CalcularParcial(IReadOnlyList<ComponenteEstudianteDto> componentes, int parcial)
    {
        var delParcial = componentes.Where(c => c.Parcial == parcial).ToList();
        if (delParcial.Count != ComponentesEvaluacion.Todos.Count) return null;
        if (delParcial.Any(c => !c.PromedioSobre10.HasValue)) return null;
        return Redondear(delParcial.Sum(c => c.PromedioSobre10!.Value * c.Ponderacion));
    }

    private static ProgresoCursoDto ConstruirProgreso(
        ContextoAnalitica contexto, IReadOnlyList<EvaluacionEstudiante> evaluaciones)
    {
        var items = evaluaciones.SelectMany(e => e.Items).ToList();
        var total = items.Count;
        var completadas = items.Count(i => i.EsCompletada);
        var vencidas = items.Where(i => i.Vencida).ToList();
        var pctAvance = total == 0 ? (decimal?)null : Redondear((decimal)completadas / total * 100m);
        var pctVencidas = vencidas.Count == 0
            ? (decimal?)null
            : Redondear((decimal)vencidas.Count(i => i.EsCompletada) / vencidas.Count * 100m);

        return new ProgresoCursoDto(
            total,
            completadas,
            items.Count(i => i.Estado == EstadosActividad.Calificada),
            items.Count(i => i.Estado == EstadosActividad.NotaCero),
            items.Count(i => i.Estado == EstadosActividad.SinEntrega),
            items.Count(i => i.Estado == EstadosActividad.Pendiente),
            vencidas.Count,
            pctAvance,
            pctVencidas);
    }

    private static List<ResumenComponenteDto> ConstruirResumenComponentes(ContextoAnalitica contexto)
    {
        var resultado = new List<ResumenComponenteDto>();
        foreach (var parcial in new[] { 1, 2 })
        {
            foreach (var componente in ComponentesEvaluacion.Todos)
            {
                var actividades = contexto.Actividades
                    .Where(a => a.Parcial == parcial && a.Componente == componente)
                    .ToList();
                var ids = actividades.Select(a => a.Id).ToHashSet();
                var calificaciones = contexto.Calificaciones
                    .Where(c => ids.Contains(c.ActividadId) && EstadosActividad.EsCalificada(c.Estado) && c.Nota.HasValue)
                    .ToList();
                var sumMax = actividades
                    .Where(a => calificaciones.Any(c => c.ActividadId == a.Id))
                    .Sum(a => a.PuntajeMaximo);
                decimal? promedio = null;
                if (calificaciones.Count > 0 && sumMax > 0)
                {
                    // Cada calificación se pondera por el máximo de su actividad.
                    var sumaNotas = calificaciones.Sum(c =>
                    {
                        var act = actividades.First(a => a.Id == c.ActividadId);
                        return act.PuntajeMaximo > 0 ? c.Nota!.Value / act.PuntajeMaximo * 10m : 0m;
                    });
                    promedio = Redondear(sumaNotas / calificaciones.Count);
                }

                resultado.Add(new ResumenComponenteDto(
                    componente,
                    ComponentesEvaluacion.Etiqueta(componente),
                    ComponentesEvaluacion.EtiquetaCorta(componente),
                    parcial,
                    ComponentesEvaluacion.Ponderacion(componente),
                    actividades.Count,
                    calificaciones.Count,
                    promedio));
            }
        }
        return resultado;
    }

    private static List<ResumenParcialDto> ConstruirResumenParciales(IReadOnlyList<EvaluacionEstudiante> evaluaciones)
    {
        return new[] { 1, 2 }
            .Select(parcial =>
            {
                var valores = evaluaciones
                    .Select(e => parcial == 1 ? e.NotaParcial1 : e.NotaParcial2)
                    .ToList();
                var conNota = valores.Where(v => v.HasValue).Select(v => v!.Value).ToList();
                var completo = evaluaciones.Count > 0 && evaluaciones.All(e => parcial == 1 ? e.Parcial1Completo : e.Parcial2Completo);
                return new ResumenParcialDto(
                    parcial,
                    conNota.Count == 0 ? null : Redondear(conNota.Average()),
                    conNota.Count,
                    evaluaciones.Count - conNota.Count,
                    completo);
            })
            .ToList();
    }

    private static List<DistribucionNotaDto> ConstruirDistribucion(
        IReadOnlyList<EvaluacionEstudiante> conNota, decimal escala)
    {
        var rangos = new (string Rango, string Etiqueta, decimal Min, decimal Max)[]
        {
            ("0-59", "0 % – 59 % (crítico)", 0m, 60m),
            ("60-69", "60 % – 69 % (bajo)", 60m, 70m),
            ("70-79", "70 % – 79 % (aceptable)", 70m, 80m),
            ("80-89", "80 % – 89 % (bueno)", 80m, 90m),
            ("90-100", "90 % – 100 % (destacado)", 90m, 100.01m)
        };

        var porcentajes = conNota
            .Where(e => e.PromedioActividades.HasValue && escala > 0)
            .Select(e => e.PromedioActividades!.Value / escala * 100m)
            .ToList();

        return rangos.Select(r =>
        {
            var cantidad = porcentajes.Count(p => p >= r.Min && p < r.Max);
            var pct = porcentajes.Count == 0 ? 0m : Redondear((decimal)cantidad / porcentajes.Count * 100m);
            return new DistribucionNotaDto(r.Rango, r.Etiqueta, cantidad, pct);
        }).ToList();
    }

    private static List<TendenciaAnalisisDto> ConstruirTendencias(IReadOnlyList<ActividadAnalisisDto> actividades)
    {
        decimal? anterior = null;
        var resultado = new List<TendenciaAnalisisDto>();
        foreach (var actividad in actividades)
        {
            decimal? variacion = null;
            string? tendencia = null;
            if (actividad.PromedioPorcentaje.HasValue && anterior.HasValue)
            {
                variacion = Redondear(actividad.PromedioPorcentaje.Value - anterior.Value);
                tendencia = variacion.Value >= 5m ? "mejora" : variacion.Value <= -5m ? "descenso" : "estable";
            }
            resultado.Add(new TendenciaAnalisisDto(
                actividad.ActividadId,
                actividad.Nombre,
                actividad.FechaEntrega,
                actividad.PromedioPorcentaje,
                tendencia,
                variacion));
            if (actividad.PromedioPorcentaje.HasValue) anterior = actividad.PromedioPorcentaje;
        }
        return resultado;
    }

    private static (string Tendencia, decimal? Variacion) CalcularTendenciaGeneral(
        IReadOnlyList<ActividadAnalisisDto> actividades)
    {
        var valores = actividades
            .Where(a => a.PromedioPorcentaje.HasValue)
            .Select(a => a.PromedioPorcentaje!.Value)
            .ToList();
        if (valores.Count < 2) return ("datos insuficientes", null);

        var mitad = valores.Count / 2;
        var primera = valores.Take(mitad).Average();
        var segunda = valores.Skip(mitad).Average();
        var variacion = Redondear(segunda - primera);
        var tendencia = variacion >= 5m ? "mejora" : variacion <= -5m ? "descenso" : "estable";
        return (tendencia, variacion);
    }

    private static decimal? CalcularDesviacion(IEnumerable<decimal> valores)
    {
        var lista = valores.ToList();
        if (lista.Count == 0) return null;
        var promedio = lista.Average();
        var varianza = lista.Sum(v => (v - promedio) * (v - promedio)) / lista.Count;
        return Redondear((decimal)Math.Sqrt((double)varianza));
    }

    private static decimal Redondear(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
