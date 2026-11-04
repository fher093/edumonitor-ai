using Backend.Data;
using Backend.Domain;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>Carga, en una sola consulta coherente, el contexto que usa la analítica.</summary>
public sealed class AnaliticaProvider(AppDbContext db, TimeProvider reloj)
{
    public async Task<ContextoAnalitica?> CargarAsync(int cursoId, CancellationToken ct)
    {
        var curso = await db.Cursos.AsNoTracking()
            .Where(c => c.Id == cursoId)
            .Select(c => new CursoAnalitica(
                c.Id, c.Nombre, c.Codigo, c.Periodo, c.EscalaCalificacion))
            .FirstOrDefaultAsync(ct);

        if (curso is null) return null;

        var estudiantes = await db.Matriculas.AsNoTracking()
            .Where(m => m.CursoId == cursoId && m.Estado == MatriculaEstados.Activa)
            .OrderBy(m => m.Estudiante.Nombre)
            .Select(m => new EstudianteAnalitica(
                m.Estudiante.Id,
                m.Estudiante.Identificacion,
                m.Estudiante.Nombre,
                m.Estudiante.Correo))
            .ToListAsync(ct);

        var actividades = await db.Actividades.AsNoTracking()
            .Where(a => a.CursoId == cursoId)
            .OrderBy(a => a.FechaEntrega ?? DateTime.MaxValue)
            .ThenBy(a => a.Id)
            .Select(a => new ActividadAnalitica(
                a.Id, a.Nombre, a.Tipo, a.Componente, a.Parcial, a.FechaEntrega, a.PuntajeMaximo))
            .ToListAsync(ct);

        var calificaciones = await db.Calificaciones.AsNoTracking()
            .Where(c => c.Actividad.CursoId == cursoId)
            .Select(c => new CalificacionAnalitica(
                c.ActividadId, c.EstudianteId, c.Nota, c.Estado, c.Observacion))
            .ToListAsync(ct);

        var reglas = await db.ReglasAlerta.AsNoTracking()
            .OrderBy(r => r.Tipo)
            .ToListAsync(ct);

        return new ContextoAnalitica(
            curso,
            estudiantes,
            actividades,
            calificaciones,
            reglas,
            reloj.GetUtcNow().UtcDateTime);
    }
}
