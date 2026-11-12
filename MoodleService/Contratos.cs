namespace MoodleService;

// ---------- Conexión (RF-02) ----------

public record ConexionDto(
    string? Url,
    bool TokenConfigurado,
    string TokenEnmascarado,
    string Origen,
    DateTime? FechaActualizacion,
    string? ActualizadoPor);

public record GuardarConexionRequest(string Url, string? Token);

public record PruebaConexionDto(
    bool Exitosa,
    string Mensaje,
    string? Sitio,
    string? VersionMoodle,
    string? UsuarioTecnico,
    int LatenciaMs,
    List<string> FuncionesFaltantes,
    DateTime Fecha);

// ---------- Extracción (RF-03) ----------
// Datos normalizados al modelo de EduMonitor. El backend los importa y registra la sincronización.

public record ExtraccionMoodleDto(
    string Sitio,
    string UrlMoodle,
    string VersionMoodle,
    DateTime FechaExtraccion,
    List<CursoMoodleDto> Cursos,
    List<AdvertenciaMoodleDto> Advertencias);

public record CursoMoodleDto(
    long MoodleId,
    string Codigo,
    string Nombre,
    string Periodo,
    bool Visible,
    List<UsuarioMoodleDto> Docentes,
    List<UsuarioMoodleDto> Estudiantes,
    List<ActividadMoodleDto> Actividades,
    List<CalificacionMoodleDto> Calificaciones);

/// <summary>
/// <c>FotoRuta</c>: ruta de la foto de perfil dentro de pluginfile.php (p. ej. "25/user/icon/boost/f1?rev=3"),
/// o null si el usuario usa la imagen genérica de Moodle.
/// </summary>
public record UsuarioMoodleDto(long MoodleId, string Nombre, string Correo, string? Identificacion, string? FotoRuta = null);

public record ActividadMoodleDto(
    long GradeItemId,
    string Nombre,
    string Tipo,
    string? Modulo,
    string Componente,
    int Parcial,
    DateTime? FechaEntrega,
    decimal PuntajeMaximo,
    string Mapeo);

/// <summary>Estado: calificada | nota_cero | completada (entregada sin nota).</summary>
public record CalificacionMoodleDto(
    long GradeItemId,
    long EstudianteMoodleId,
    decimal? Nota,
    string Estado,
    DateTime? FechaCalificacion);

public record AdvertenciaMoodleDto(long? CursoMoodleId, string Mensaje);

public record ErrorDto(string Message);
