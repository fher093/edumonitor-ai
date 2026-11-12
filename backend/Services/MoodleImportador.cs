using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public class ResultadoImportacion
{
    public int CursosImportados { get; set; }
    public int CursosOmitidos { get; set; }
    public HashSet<int> Estudiantes { get; } = [];
    public int MatriculasActivas { get; set; }
    public int MatriculasRetiradas { get; set; }
    public int ActividadesImportadas { get; set; }
    public int CalificacionesImportadas { get; set; }
    public List<DetalleSincronizacionDto> Detalle { get; } = [];

    public void Error(long? curso, string mensaje) => Detalle.Add(new("error", curso, mensaje));
    public void Advertencia(long? curso, string mensaje) => Detalle.Add(new("advertencia", curso, mensaje));
    public void Info(long? curso, string mensaje) => Detalle.Add(new("info", curso, mensaje));
}

/// <summary>
/// Importa a la base de EduMonitor los datos normalizados que entrega MoodleService (RF-03).
/// Moodle es la fuente de verdad: por cada curso se crean/actualizan estudiantes, matrículas,
/// actividades y calificaciones, y se retiran o eliminan los que ya no existen en Moodle.
/// Es idempotente: sincronizar dos veces los mismos datos no genera cambios.
/// </summary>
public class MoodleImportador(AppDbContext db, TimeProvider clock, ILogger<MoodleImportador> logger)
{
    public async Task<ResultadoImportacion> ImportarAsync(ExtraccionMoodleDto datos, CancellationToken ct)
    {
        var r = new ResultadoImportacion();
        foreach (var a in datos.Advertencias)
            r.Advertencia(a.CursoMoodleId, a.Mensaje);

        var docentes = await db.Usuarios
            .Where(u => u.Rol.Nombre == RolNombres.Docente && u.Activo)
            .ToListAsync(ct);

        foreach (var curso in datos.Cursos)
        {
            try
            {
                if (await ImportarCursoAsync(curso, docentes, r, ct)) r.CursosImportados++;
                else r.CursosOmitidos++;
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
            {
                // Se descartan los cambios pendientes del curso fallido y se continúa con el siguiente.
                db.ChangeTracker.Clear();
                docentes = await db.Usuarios.Where(u => u.Rol.Nombre == RolNombres.Docente && u.Activo).ToListAsync(ct);
                logger.LogWarning(ex, "Error importando el curso Moodle {CursoId}", curso.MoodleId);
                r.CursosOmitidos++;
                r.Error(curso.MoodleId, $"{curso.Nombre}: no se pudo guardar ({ex.GetBaseException().Message}).");
            }
        }

        return r;
    }

    private async Task<bool> ImportarCursoAsync(
        CursoMoodleDto m, List<Usuario> docentes, ResultadoImportacion r, CancellationToken ct)
    {
        var ahora = clock.GetUtcNow().UtcDateTime;

        // 1. Docente responsable: se empareja por correo con una cuenta Docente de EduMonitor.
        var correosDocentes = m.Docentes
            .Select(d => d.Correo.Trim().ToLowerInvariant())
            .Where(c => c.Length > 0)
            .ToList();
        var docente = docentes.FirstOrDefault(u => correosDocentes.Contains(u.Correo));
        if (docente is null)
        {
            r.Error(m.MoodleId, m.Docentes.Count == 0
                ? $"{m.Nombre} ({m.Codigo}): omitido, el curso no tiene docente en Moodle."
                : $"{m.Nombre} ({m.Codigo}): omitido, el docente de Moodle ({string.Join(", ", correosDocentes)}) " +
                  "no tiene una cuenta de docente activa en EduMonitor con ese correo.");
            return false;
        }

        var docenteMoodle = m.Docentes.First(d => d.Correo.Trim().Equals(docente.Correo, StringComparison.OrdinalIgnoreCase));
        docente.MoodleUserId = docenteMoodle.MoodleId;

        // 2. Curso: por id de Moodle o, si existe uno local con el mismo código/período/docente, se vincula.
        var codigo = m.Codigo.Trim();
        var periodo = m.Periodo.Trim();
        var curso = await db.Cursos.FirstOrDefaultAsync(c => c.MoodleCourseId == m.MoodleId, ct);
        var cursoIdActual = curso?.Id ?? 0;
        var conflicto = await db.Cursos.FirstOrDefaultAsync(c =>
            c.DocenteId == docente.Id && c.Codigo == codigo && c.Periodo == periodo && c.Id != cursoIdActual, ct);

        if (conflicto is not null)
        {
            if (curso is not null || conflicto.MoodleCourseId is not null)
            {
                r.Error(m.MoodleId, $"{m.Nombre} ({codigo}): omitido, ya existe otro curso {codigo} en {periodo} para el mismo docente.");
                return false;
            }

            curso = conflicto;
            var locales = await db.Actividades.Where(a => a.CursoId == curso.Id && a.MoodleGradeItemId == null).ToListAsync(ct);
            db.Actividades.RemoveRange(locales);
            r.Info(m.MoodleId, $"{m.Nombre} ({codigo}): el curso local existente se vinculó con Moodle" +
                               (locales.Count > 0 ? $"; se reemplazaron {locales.Count} actividades registradas localmente." : "."));
        }

        if (curso is null)
        {
            curso = new Curso { FechaCreacion = ahora, EscalaCalificacion = 20.0m };
            db.Cursos.Add(curso);
        }

        curso.DocenteId = docente.Id;
        curso.Nombre = m.Nombre.Trim();
        curso.Codigo = codigo;
        curso.Periodo = periodo;
        curso.Origen = OrigenesDatos.Moodle;
        curso.MoodleCourseId = m.MoodleId;
        curso.FechaSincronizacion = ahora;
        curso.FechaActualizacion = ahora;
        await db.SaveChangesAsync(ct);

        // 3. Estudiantes: se emparejan por id de Moodle, luego cédula (idnumber) y luego correo.
        var porMoodleId = await ResolverEstudiantesAsync(m.Estudiantes, ahora, r, ct);
        foreach (var id in porMoodleId.Values) r.Estudiantes.Add(id);

        // 4. Matrículas: activas las de Moodle; las demás quedan retiradas (no se borra el historial).
        var matriculas = await db.Matriculas.Where(x => x.CursoId == curso.Id).ToListAsync(ct);
        var activos = porMoodleId.Values.ToHashSet();
        foreach (var estudianteId in activos)
        {
            var mat = matriculas.FirstOrDefault(x => x.EstudianteId == estudianteId);
            if (mat is null)
                db.Matriculas.Add(new Matricula
                {
                    CursoId = curso.Id, EstudianteId = estudianteId, FechaMatricula = ahora, Estado = MatriculaEstados.Activa
                });
            else
                mat.Estado = MatriculaEstados.Activa;
            r.MatriculasActivas++;
        }
        foreach (var mat in matriculas.Where(x => !activos.Contains(x.EstudianteId) && x.Estado == MatriculaEstados.Activa))
        {
            mat.Estado = MatriculaEstados.Retirada;
            r.MatriculasRetiradas++;
        }

        // 5. Actividades (ítems de calificación).
        var actividades = await db.Actividades
            .Where(a => a.CursoId == curso.Id && a.MoodleGradeItemId != null)
            .ToListAsync(ct);
        var idsItems = m.Actividades.Select(a => a.GradeItemId).ToHashSet();
        var eliminadas = actividades.Where(a => !idsItems.Contains(a.MoodleGradeItemId!.Value)).ToList();
        db.Actividades.RemoveRange(eliminadas);
        if (eliminadas.Count > 0)
            r.Info(m.MoodleId, $"{m.Nombre}: se eliminaron {eliminadas.Count} actividad(es) que ya no existen en Moodle.");

        var porItem = new Dictionary<long, Actividad>();
        foreach (var a in m.Actividades)
        {
            var act = actividades.FirstOrDefault(x => x.MoodleGradeItemId == a.GradeItemId);
            if (act is null)
            {
                act = new Actividad { CursoId = curso.Id, MoodleGradeItemId = a.GradeItemId, FechaCreacion = ahora };
                db.Actividades.Add(act);
            }

            act.Nombre = Recortar(a.Nombre, 150);
            act.Tipo = Recortar(a.Tipo, 40);
            act.Componente = ComponentesEvaluacion.Todos.Contains(a.Componente) ? a.Componente : ComponentesEvaluacion.Autonomo;
            act.Parcial = a.Parcial is 1 or 2 ? a.Parcial : 1;
            act.FechaEntrega = a.FechaEntrega;
            act.PuntajeMaximo = a.PuntajeMaximo > 0 ? Math.Min(a.PuntajeMaximo, 999.99m) : 10m;
            act.MapeoComponente = a.Mapeo;
            act.FechaActualizacion = ahora;
            porItem[a.GradeItemId] = act;
            r.ActividadesImportadas++;
        }
        await db.SaveChangesAsync(ct);

        // 6. Calificaciones y estados (RN-02). Sin registro en Moodle => sin registro local:
        //    EduMonitor resuelve "sin entrega" o "pendiente" según la fecha de entrega.
        var idsActividades = porItem.Values.Select(a => a.Id).ToList();
        var existentes = await db.Calificaciones
            .Where(c => idsActividades.Contains(c.ActividadId))
            .ToListAsync(ct);
        var vistas = new HashSet<(int, int)>();

        foreach (var c in m.Calificaciones)
        {
            if (!porItem.TryGetValue(c.GradeItemId, out var act) ||
                !porMoodleId.TryGetValue(c.EstudianteMoodleId, out var estudianteId))
                continue;

            var nota = c.Nota is null ? (decimal?)null : Math.Clamp(Math.Round(c.Nota.Value, 2), 0m, act.PuntajeMaximo);
            var estado = c.Estado switch
            {
                EstadosActividad.Calificada when nota == 0 => EstadosActividad.NotaCero,
                EstadosActividad.Calificada or EstadosActividad.NotaCero or EstadosActividad.Completada => c.Estado,
                _ => EstadosActividad.Pendiente
            };

            var cal = existentes.FirstOrDefault(x => x.ActividadId == act.Id && x.EstudianteId == estudianteId);
            if (cal is null)
            {
                cal = new Calificacion { ActividadId = act.Id, EstudianteId = estudianteId, FechaRegistro = c.FechaCalificacion ?? ahora };
                db.Calificaciones.Add(cal);
            }

            cal.Nota = estado == EstadosActividad.Completada ? null : nota;
            cal.Estado = estado;
            cal.Observacion = "Sincronizado desde Moodle";
            cal.FechaActualizacion = ahora;
            vistas.Add((act.Id, estudianteId));
            r.CalificacionesImportadas++;
        }

        db.Calificaciones.RemoveRange(existentes.Where(x => !vistas.Contains((x.ActividadId, x.EstudianteId))));
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Devuelve el mapa id de usuario Moodle → id de estudiante local, creando o actualizando registros.</summary>
    private async Task<Dictionary<long, int>> ResolverEstudiantesAsync(
        List<UsuarioMoodleDto> usuarios, DateTime ahora, ResultadoImportacion r, CancellationToken ct)
    {
        var idsMoodle = usuarios.Select(u => u.MoodleId).ToList();
        var cedulas = usuarios.Where(u => !string.IsNullOrWhiteSpace(u.Identificacion)).Select(u => u.Identificacion!.Trim()).ToList();
        var correos = usuarios.Select(u => u.Correo.Trim().ToLowerInvariant()).Where(c => c.Length > 0).ToList();

        var candidatos = await db.Estudiantes
            .Where(e => (e.MoodleUserId != null && idsMoodle.Contains(e.MoodleUserId.Value)) ||
                        cedulas.Contains(e.Identificacion) ||
                        correos.Contains(e.Correo))
            .ToListAsync(ct);

        var resultado = new List<(long MoodleId, Estudiante Estudiante)>();
        foreach (var u in usuarios)
        {
            var cedula = string.IsNullOrWhiteSpace(u.Identificacion) ? null : Recortar(u.Identificacion.Trim(), 20);
            var correo = u.Correo.Trim().ToLowerInvariant();

            var est = candidatos.FirstOrDefault(e => e.MoodleUserId == u.MoodleId)
                      ?? (cedula is null ? null : candidatos.FirstOrDefault(e => e.Identificacion == cedula && e.MoodleUserId == null))
                      ?? (correo.Length == 0 ? null : candidatos.FirstOrDefault(e => e.Correo == correo && e.MoodleUserId == null));

            if (est is null)
            {
                var identificacion = cedula ?? $"MDL-{u.MoodleId}";
                if (candidatos.Any(e => e.Identificacion == identificacion) ||
                    await db.Estudiantes.AnyAsync(e => e.Identificacion == identificacion, ct))
                {
                    r.Advertencia(null, $"La cédula {identificacion} de {u.Nombre} ya pertenece a otro estudiante; se registró como MDL-{u.MoodleId}.");
                    identificacion = $"MDL-{u.MoodleId}";
                }

                est = new Estudiante { Identificacion = identificacion, FechaCreacion = ahora, Activo = true };
                db.Estudiantes.Add(est);
                candidatos.Add(est);
            }
            else if (cedula is not null && est.Identificacion.StartsWith("MDL-") &&
                     !candidatos.Any(e => e != est && e.Identificacion == cedula))
            {
                est.Identificacion = cedula; // Moodle ya tiene la cédula.
            }

            if (correo.Length == 0)
            {
                correo = $"moodle-{u.MoodleId}@sin-correo.local";
                r.Advertencia(null, $"{u.Nombre} no tiene correo visible en Moodle.");
            }

            est.Nombre = Recortar(u.Nombre.Trim(), 150);
            est.Correo = Recortar(correo, 254);
            est.MoodleUserId = u.MoodleId;
            est.FotoMoodleRuta = u.FotoRuta is { Length: <= 300 } ? u.FotoRuta : null;
            est.Activo = true;
            est.FechaActualizacion = ahora;
            resultado.Add((u.MoodleId, est));
        }

        await db.SaveChangesAsync(ct);
        return resultado.ToDictionary(x => x.MoodleId, x => x.Estudiante.Id);
    }

    private static string Recortar(string s, int max) => s.Length <= max ? s : s[..max];
}
