using Backend.Data;
using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Construye enlaces hacia Moodle para que el docente cree, edite o califique allí
/// (EduMonitor solo consulta los datos sincronizados).
/// </summary>
public static class EnlacesMoodle
{
    /// <summary>URL pública del sitio Moodle según la última sincronización registrada.</summary>
    public static async Task<string?> ObtenerUrlBaseAsync(AppDbContext db, CancellationToken ct)
    {
        var url = await db.SincronizacionesMoodle.AsNoTracking()
            .Where(s => s.UrlMoodle != null && s.Estado != EstadosSincronizacion.Fallida)
            .OrderByDescending(s => s.FechaInicio)
            .Select(s => s.UrlMoodle)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(url) ? null : url.TrimEnd('/');
    }

    /// <summary>Página del curso en Moodle.</summary>
    public static string? Curso(string? urlBase, long? moodleCourseId) =>
        urlBase is null || moodleCourseId is null
            ? null
            : $"{urlBase}/course/view.php?id={moodleCourseId}";

    /// <summary>
    /// Vista "Single view" del ítem de calificación: muestra y permite calificar
    /// a todos los estudiantes en esa actividad.
    /// </summary>
    public static string? Actividad(string? urlBase, long? moodleCourseId, long? gradeItemId) =>
        urlBase is null || moodleCourseId is null || gradeItemId is null
            ? null
            : $"{urlBase}/grade/report/singleview/index.php?id={moodleCourseId}&item=grade&itemid={gradeItemId}";
}
