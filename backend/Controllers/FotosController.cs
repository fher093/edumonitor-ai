using Backend.Data;
using Backend.Domain;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Backend.Controllers;

/// <summary>
/// Fotos de perfil de los estudiantes, sincronizadas desde Moodle. La foto es un dato personal:
/// solo la ve el docente propietario del curso donde el estudiante está matriculado (RN-11, RN-12).
/// La imagen se pide a MoodleService (que usa el token técnico) y se guarda un rato en memoria.
/// </summary>
[ApiController]
[Route("api/cursos/{cursoId:int}")]
[Authorize(Roles = RolNombres.Docente)]
public class FotosController(
    AppDbContext db,
    AnaliticaProvider analitica,
    IMoodleServiceClient moodle,
    ITokenService tokens,
    IMemoryCache cache) : ControladorAcademicoBase(db, analitica)
{
    private static readonly TimeSpan DuracionCache = TimeSpan.FromHours(1);

    /// <summary>Ids de los estudiantes del curso que tienen foto (para no pedir fotos inexistentes).</summary>
    [HttpGet("fotos")]
    [ProducesResponseType<List<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<int>>> ConFoto(int cursoId, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        return Ok(await Db.Matriculas.AsNoTracking()
            .Where(m => m.CursoId == cursoId && m.Estudiante.FotoMoodleRuta != null)
            .Select(m => m.EstudianteId)
            .ToListAsync(ct));
    }

    /// <summary>Foto de perfil de un estudiante del curso.</summary>
    [HttpGet("estudiantes/{estudianteId:int}/foto")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Foto(int cursoId, int estudianteId, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        var ruta = await Db.Matriculas.AsNoTracking()
            .Where(m => m.CursoId == cursoId && m.EstudianteId == estudianteId)
            .Select(m => m.Estudiante.FotoMoodleRuta)
            .FirstOrDefaultAsync(ct);
        if (ruta is null) return NotFound();

        // La ruta incluye ?rev=N: si el estudiante cambia la foto en Moodle, cambia la clave de caché.
        var clave = $"foto:{ruta}";
        if (!cache.TryGetValue(clave, out (byte[] Datos, string Tipo) foto))
        {
            var obtenida = await moodle.ObtenerFotoAsync(tokens.CrearTokenSincronizacion().Token, ruta, ct);
            if (obtenida is null) return NotFound();
            foto = obtenida.Value;
            cache.Set(clave, foto, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DuracionCache,
                Size = foto.Datos.Length
            });
        }

        // "private": el navegador puede guardarla, pero no proxies ni cachés compartidas.
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(foto.Datos, foto.Tipo);
    }
}
