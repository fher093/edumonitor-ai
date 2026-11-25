namespace AIService.Agente;

public sealed class AgenteOptions
{
    public const string Section = "Agente";

    /// <summary>
    /// Ruta rápida: en preguntas sobre un curso, el orquestador ejecuta las herramientas del análisis completo
    /// sin pedirle al modelo que las elija, y usa el modelo una sola vez para redactar (ver EVALUACION_AGENTE.md).
    /// En false, el modelo elige las herramientas en cada pregunta (más lento con modelos locales).
    /// </summary>
    public bool RutaRapida { get; set; } = true;
}
