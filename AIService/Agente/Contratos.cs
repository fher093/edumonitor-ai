using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AIService.Agente;

// ---------------------------------------------------------------- API pública del agente

public record MensajeHistorial(string Rol, string Texto);

public record PeticionChat(
    [Required, StringLength(1000, MinimumLength = 2)] string Mensaje,
    int? CursoId = null,
    List<MensajeHistorial>? Historial = null,
    string? Proveedor = null); // "ollama" (por defecto, local) o "gemini" (demostración, más rápido)

public record HechoDto(string Texto, string? Fuente);

public record RecomendacionAgenteDto(string Accion, string Justificacion);

/// <summary>
/// Material de apoyo generado por el modelo (deber, preguntas, guía…) cuando el docente lo pide. No son datos:
/// se muestra aparte, con una nota para revisarlo antes de usarlo.
/// </summary>
public record MaterialDto(string Titulo, List<string> Items, string Nota);

/// <summary>Una llamada a una herramienta, tal como se muestra en la traza (TT-09).</summary>
public record PasoTrazaDto(
    string Herramienta,
    string Argumentos,
    bool Exito,
    int? CodigoHttp,
    long DuracionMs,
    string? Error,
    string Origen = "modelo"); // "modelo": la pidió el modelo; "orquestador": la agregó el agente (HU-13 / consulta mínima)

/// <summary>
/// Respuesta del agente (HU-14): hechos, inferencias y recomendaciones separados, con fuentes,
/// fecha de corte, advertencias y la traza de herramientas usadas.
/// Estado: ok | sin_acceso | sin_datos | error_herramientas.
/// </summary>
public record RespuestaAgenteDto(
    string Estado,
    string? Mensaje,
    List<HechoDto> Hechos,
    List<string> Inferencias,
    List<RecomendacionAgenteDto> Recomendaciones,
    List<string> Fuentes,
    DateTime? FechaCorte,
    List<string> Advertencias,
    List<PasoTrazaDto> Traza,
    string Modelo,
    string ConsultaId,
    long DuracionMs,
    string? Respuesta = null, // respuesta directa a la pregunta (1-3 frases), verificada como los hechos
    MaterialDto? Material = null); // material de apoyo generado (solo cuando el docente pide crear algo)

// ---------------------------------------------------------------- Modelo de lenguaje

public record LlamadaHerramienta(string Nombre, JsonElement Argumentos);

public record MensajeChat(
    string Rol,
    string Contenido,
    IReadOnlyList<LlamadaHerramienta>? LlamadasHerramienta = null,
    string? NombreHerramienta = null);

public record DefinicionHerramienta(string Nombre, string Descripcion, JsonElement Parametros);

/// <summary>Abstracción del modelo de lenguaje (Ollama en producción, simulado en las pruebas).</summary>
public interface IModeloLenguaje
{
    string NombreModelo { get; }

    /// <param name="herramientas">Herramientas disponibles (function calling); null en la respuesta final.</param>
    /// <param name="formato">Esquema JSON que debe cumplir la respuesta; null para texto libre.</param>
    Task<MensajeChat> ChatAsync(
        IReadOnlyList<MensajeChat> mensajes,
        IReadOnlyList<DefinicionHerramienta>? herramientas,
        JsonElement? formato,
        CancellationToken ct);
}

/// <summary>El modelo de lenguaje no responde (Ollama apagado, sin el modelo o tiempo agotado).</summary>
public sealed class ModeloNoDisponibleException(string mensaje, Exception? interna = null) : Exception(mensaje, interna);

// ---------------------------------------------------------------- Backend académico

/// <summary>Datos de la consulta en curso: el JWT del docente y un id para correlacionar la auditoría.</summary>
public record ContextoConsulta(string Token, string ConsultaId);

public record RespuestaBackend(int CodigoHttp, JsonElement? Cuerpo)
{
    public bool Exito => CodigoHttp is >= 200 and < 300 && Cuerpo is not null;
}

/// <summary>Acceso de solo lectura a la API del backend con el JWT del docente (RN-12).</summary>
public interface IBackendAcademico
{
    Task<RespuestaBackend> GetAsync(string ruta, string herramienta, ContextoConsulta contexto, CancellationToken ct);
}
