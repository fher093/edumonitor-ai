using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/estudiantes")]
[Authorize]
public class EstudiantesController(AppDbContext db) : ControllerBase
{
    /// <summary>Busca estudiantes por nombre o identificación (HU-03 / RF-03).</summary>
    [HttpGet]
    [ProducesResponseType<List<EstudianteDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EstudianteDto>>> Buscar([FromQuery] string? q, CancellationToken ct)
    {
        var query = db.Estudiantes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var filtro = q.Trim().ToLower();
            query = query.Where(e => e.Identificacion.ToLower().Contains(filtro) || e.Nombre.ToLower().Contains(filtro));
        }

        var estudiantes = await query
            .OrderBy(e => e.Nombre)
            .Take(50)
            .Select(e => new EstudianteDto(e.Id, e.Identificacion, e.Nombre, e.Correo, e.Activo))
            .ToListAsync(ct);

        return Ok(estudiantes);
    }

    /// <summary>Obtiene un estudiante por su Id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<EstudianteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstudianteDto>> ObtenerPorId(int id, CancellationToken ct)
    {
        var estudiante = await db.Estudiantes.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (estudiante is null) return NotFound(new ErrorResponse("Estudiante no encontrado."));

        return Ok(new EstudianteDto(estudiante.Id, estudiante.Identificacion, estudiante.Nombre, estudiante.Correo, estudiante.Activo));
    }

    /// <summary>Registra un nuevo estudiante validando la identificación única (HU-03 / RF-03).</summary>
    [HttpPost]
    [ProducesResponseType<EstudianteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EstudianteDto>> Registrar([FromBody] CrearEstudianteRequest request, CancellationToken ct)
    {
        var identificacion = request.Identificacion.Trim();
        var nombre = request.Nombre.Trim();
        var correo = request.Correo.Trim().ToLowerInvariant();

        var existeId = await db.Estudiantes.AnyAsync(e => e.Identificacion == identificacion, ct);
        if (existeId)
        {
            return BadRequest(new ErrorResponse($"Ya existe un estudiante con la identificación '{identificacion}'."));
        }

        var ahora = DateTime.UtcNow;
        var estudiante = new Estudiante
        {
            Identificacion = identificacion,
            Nombre = nombre,
            Correo = correo,
            Activo = true,
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };

        db.Estudiantes.Add(estudiante);
        await db.SaveChangesAsync(ct);

        var dto = new EstudianteDto(estudiante.Id, estudiante.Identificacion, estudiante.Nombre, estudiante.Correo, estudiante.Activo);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = estudiante.Id }, dto);
    }
}
