using System.ComponentModel.DataAnnotations;

namespace Backend.Dtos;

// ---------- Gestión de cuentas de docentes (HU-02, solo Administrador Técnico) ----------

/// <summary>
/// Cuenta de docente vista por el administrador. <c>CursosAsignados</c> indica cuántos cursos de Moodle
/// se le asignaron al sincronizar (0 suele significar que su correo no coincide con el de Moodle).
/// </summary>
public record DocenteDto(
    int Id,
    string Nombre,
    string Correo,
    bool Activo,
    bool Bloqueado,
    DateTime? BloqueadoHasta,
    DateTime? UltimoAcceso,
    int CursosAsignados,
    DateTime FechaCreacion);

public record CrearDocenteRequest(
    [Required, StringLength(150, MinimumLength = 2)] string Nombre,
    [Required, EmailAddress, StringLength(254)] string Correo,
    [Required, StringLength(128, MinimumLength = 8)] string ClaveTemporal);

public record EditarDocenteRequest(
    [Required, StringLength(150, MinimumLength = 2)] string Nombre,
    [Required, EmailAddress, StringLength(254)] string Correo);

public record CambiarEstadoDocenteRequest(bool Activo);

public record RestablecerClaveRequest(
    [Required, StringLength(128, MinimumLength = 8)] string ClaveTemporal);
