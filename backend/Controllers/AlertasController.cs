using System.Text.Json;
using Backend.Data;
using Backend.Domain;
using Backend.Dtos;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/alertas")]
[Authorize(Roles = $"{RolNombres.Docente},{RolNombres.AdministradorTecnico}")]
public class AlertasController(
    AppDbContext db,
    AnaliticaProvider analitica,
    AnaliticaService servicio) : ControladorAcademicoBase(db, analitica)
{
    /// <summary>Alertas académicas con evidencia y reglas aplicadas (HU-10 / RF-07).</summary>
    [HttpGet("curso/{cursoId:int}")]
    [Authorize(Roles = RolNombres.Docente)]
    [ProducesResponseType<AlertasCursoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertasCursoDto>> PorCurso(int cursoId, CancellationToken ct)
    {
        var error = await VerificarAccesoCursoAsync(cursoId, ct);
        if (error is not null) return error;

        var contexto = await Analitica.CargarAsync(cursoId, ct);
        if (contexto is null) return NotFound(new ErrorResponse("Curso no encontrado."));

        return Ok(servicio.CalcularAlertasCurso(contexto));
    }

    /// <summary>Lista las reglas de alerta configuradas (RN-04 a RN-06).</summary>
    [HttpGet("reglas")]
    [ProducesResponseType<List<ReglaAlertaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ReglaAlertaDto>>> ListarReglas(CancellationToken ct)
    {
        var reglas = await Db.ReglasAlerta.AsNoTracking()
            .OrderBy(r => r.Tipo)
            .Select(r => new ReglaAlertaDto(
                r.Id, r.Tipo, r.Nombre, r.ParametrosJson, r.Activa, r.FechaActualizacion))
            .ToListAsync(ct);

        return Ok(reglas);
    }

    /// <summary>
    /// Actualiza los parámetros o el estado de una regla (solo Administrador Técnico).
    /// </summary>
    [HttpPut("reglas/{id:int}")]
    [Authorize(Roles = RolNombres.AdministradorTecnico)]
    [ProducesResponseType<ReglaAlertaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReglaAlertaDto>> EditarRegla(
        int id, [FromBody] EditarReglaAlertaRequest request, CancellationToken ct)
    {
        var regla = await Db.ReglasAlerta.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (regla is null) return NotFound(new ErrorResponse("Regla de alerta no encontrada."));

        if (!ValidarParametros(regla.Tipo, request.ParametrosJson, out var mensaje))
            return BadRequest(new ErrorResponse(mensaje));

        regla.ParametrosJson = request.ParametrosJson.Trim();
        regla.Activa = request.Activa;
        regla.FechaActualizacion = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);

        return Ok(new ReglaAlertaDto(
            regla.Id, regla.Tipo, regla.Nombre, regla.ParametrosJson, regla.Activa,
            regla.FechaActualizacion));
    }

    private static bool ValidarParametros(string tipo, string json, out string mensaje)
    {
        mensaje = string.Empty;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            mensaje = "Los parámetros deben ser un objeto JSON válido.";
            return false;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                mensaje = "Los parámetros deben ser un objeto JSON.";
                return false;
            }

            bool TieneDecimal(string nombre, out decimal valor)
            {
                valor = 0;
                return doc.RootElement.TryGetProperty(nombre, out var p) && p.TryGetDecimal(out valor);
            }

            switch (tipo)
            {
                case "descenso":
                    if (!TieneDecimal("k", out var k) || k < 1)
                    {
                        mensaje = "La regla de descenso requiere k (entero mayor que 0).";
                        return false;
                    }
                    if (!TieneDecimal("delta", out var delta) || delta < 0)
                    {
                        mensaje = "La regla de descenso requiere delta (número mayor o igual que 0).";
                        return false;
                    }
                    return true;

                case "atraso":
                    if (!TieneDecimal("n", out var n) || n < 1)
                    {
                        mensaje = "La regla de atraso requiere n (entero mayor que 0).";
                        return false;
                    }
                    return true;

                case "baja_finalizacion":
                    if (!TieneDecimal("p", out var p) || p <= 0 || p > 100)
                    {
                        mensaje = "La regla de baja finalización requiere p (porcentaje entre 1 y 100).";
                        return false;
                    }
                    return true;

                default:
                    mensaje = "Tipo de regla no reconocido.";
                    return false;
            }
        }
    }
}
