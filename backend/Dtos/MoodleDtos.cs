namespace Backend.Dtos;

// ---------- Contrato de extracción que entrega MoodleService (GET /api/moodle/extraccion) ----------

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

public record CalificacionMoodleDto(
    long GradeItemId,
    long EstudianteMoodleId,
    decimal? Nota,
    string Estado,
    DateTime? FechaCalificacion);

public record AdvertenciaMoodleDto(long? CursoMoodleId, string Mensaje);

// ---------- Sincronización (RF-03) ----------

/// <summary>Cursos de Moodle a sincronizar; vacío o null = todos los visibles para el usuario técnico.</summary>
public record SincronizarMoodleRequest(List<long>? CursosMoodle = null);

/// <summary>Tipo: error (curso omitido) | advertencia (dato incompleto) | info (acción realizada).</summary>
public record DetalleSincronizacionDto(string Tipo, long? CursoMoodleId, string Mensaje);

public record SincronizacionMoodleDto(
    int Id,
    DateTime FechaInicio,
    DateTime? FechaFin,
    string Estado,
    string? UsuarioCorreo,
    string? UrlMoodle,
    int CursosImportados,
    int CursosOmitidos,
    int EstudiantesImportados,
    int MatriculasActivas,
    int MatriculasRetiradas,
    int ActividadesImportadas,
    int CalificacionesImportadas,
    string? Mensaje,
    List<DetalleSincronizacionDto> Detalle);

/// <summary>Resumen de la última sincronización, visible para docentes (RNF-08: fuente y fecha).</summary>
public record EstadoSincronizacionDto(
    DateTime? UltimaSincronizacion,
    string? Estado,
    string? UrlMoodle,
    int CursosMoodle,
    bool SincronizacionAutomatica = false,
    int? IntervaloMinutos = null);
