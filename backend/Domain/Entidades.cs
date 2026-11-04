namespace Backend.Domain;

/// <summary>Nombres canónicos de los roles del sistema (RF-01).</summary>
public static class RolNombres
{
    public const string Docente = "Docente";
    public const string AdministradorTecnico = "AdministradorTecnico";
}

public class Rol
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public int RolId { get; set; }
    public Rol Rol { get; set; } = null!;

    /// <summary>Identificador del usuario en Moodle (se completa en HU-03).</summary>
    public long? MoodleUserId { get; set; }

    public bool Activo { get; set; } = true;
    public int IntentosFallidos { get; set; }
    public DateTime? BloqueadoHasta { get; set; }
    public DateTime? UltimoAcceso { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }

    public ICollection<Curso> Cursos { get; set; } = new List<Curso>();
}

public class Curso
{
    public int Id { get; set; }
    public int DocenteId { get; set; }
    public Usuario Docente { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public decimal EscalaCalificacion { get; set; } = 20.0m;
    public string? Paralelo { get; set; }

    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }

    public ICollection<Matricula> Matriculas { get; set; } = new List<Matricula>();
}

public class Estudiante
{
    public int Id { get; set; }
    public string Identificacion { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }

    public ICollection<Matricula> Matriculas { get; set; } = new List<Matricula>();
}

public static class MatriculaEstados
{
    public const string Activa = "Activa";
    public const string Retirada = "Retirada";
}

public class Matricula
{
    public int Id { get; set; }
    public int CursoId { get; set; }
    public Curso Curso { get; set; } = null!;

    public int EstudianteId { get; set; }
    public Estudiante Estudiante { get; set; } = null!;

    public DateTime FechaMatricula { get; set; }
    public string Estado { get; set; } = MatriculaEstados.Activa;
}

/// <summary>
/// Componentes de evaluación definidos por el instructivo UTA. La ponderación
/// se aplica únicamente al calcular la nota de cada parcial (40 / 30 / 30).
/// </summary>
public static class ComponentesEvaluacion
{
    public const string ContactoDocente = "contacto_docente";
    public const string PracticoExperimental = "practico_experimental";
    public const string Autonomo = "autonomo";

    public static readonly IReadOnlyList<string> Todos =
        [ContactoDocente, PracticoExperimental, Autonomo];

    public static decimal Ponderacion(string componente) => componente switch
    {
        ContactoDocente => 0.40m,
        PracticoExperimental => 0.30m,
        Autonomo => 0.30m,
        _ => 0m
    };

    public static string Etiqueta(string componente) => componente switch
    {
        ContactoDocente => "Aprendizaje en contacto con el docente (40 %)",
        PracticoExperimental => "Aprendizaje práctico-experimental (30 %)",
        Autonomo => "Aprendizaje autónomo (30 %)",
        _ => componente
    };

    public static string EtiquetaCorta(string componente) => componente switch
    {
        ContactoDocente => "Contacto con el docente",
        PracticoExperimental => "Práctico-experimental",
        Autonomo => "Autónomo",
        _ => componente
    };
}

/// <summary>Estados únicos de una actividad (RN-02).</summary>
public static class EstadosActividad
{
    public const string Calificada = "calificada";
    public const string NotaCero = "nota_cero";
    public const string SinEntrega = "sin_entrega";
    public const string Pendiente = "pendiente";
    public const string Completada = "completada";

    public static readonly IReadOnlyList<string> Todos =
        [Calificada, NotaCero, SinEntrega, Pendiente, Completada];

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) &&
        Todos.Contains(estado.Trim().ToLowerInvariant());

    public static bool EsCalificada(string? estado) =>
        string.Equals(estado, Calificada, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(estado, NotaCero, StringComparison.OrdinalIgnoreCase);

    public static bool EsCompletada(string? estado) =>
        EsCalificada(estado) ||
        string.Equals(estado, Completada, StringComparison.OrdinalIgnoreCase);

    public static string Etiqueta(string? estado) => estado switch
    {
        Calificada => "Calificada",
        NotaCero => "Nota cero",
        SinEntrega => "Sin entrega",
        Pendiente => "Pendiente de calificar",
        Completada => "Completada sin nota",
        _ => "Sin estado"
    };

    /// <summary>
    /// Resuelve el estado efectivo de una actividad. Si no existe registro explícito,
    /// una actividad vencida se considera "sin entrega" y una futura "pendiente".
    /// </summary>
    public static string Resolver(string? estadoRegistrado, decimal? nota, DateTime? fechaEntrega, DateTime ahora)
    {
        if (!string.IsNullOrWhiteSpace(estadoRegistrado))
        {
            var estado = estadoRegistrado.Trim().ToLowerInvariant();
            if (!EsValido(estado)) estado = Pendiente;
            if (estado == NotaCero) return NotaCero;
            if (estado == Calificada && nota is null) return Pendiente;
            return estado;
        }

        return fechaEntrega is not null && fechaEntrega.Value <= ahora
            ? SinEntrega
            : Pendiente;
    }
}

/// <summary>Actividad evaluable del módulo académico local (HU-05 / RF-03).</summary>
public class Actividad
{
    public int Id { get; set; }

    public int CursoId { get; set; }
    public Curso Curso { get; set; } = null!;

    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = "Tarea";
    public string Componente { get; set; } = ComponentesEvaluacion.Autonomo;
    public int Parcial { get; set; } = 1;
    public DateTime? FechaEntrega { get; set; }
    public decimal PuntajeMaximo { get; set; } = 10m;

    public DateTime FechaCreacion { get; set; }
    public DateTime FechaActualizacion { get; set; }

    public ICollection<Calificacion> Calificaciones { get; set; } = new List<Calificacion>();
}

/// <summary>Calificación y estado de un estudiante en una actividad (HU-05 / RN-02).</summary>
public class Calificacion
{
    public int Id { get; set; }

    public int ActividadId { get; set; }
    public Actividad Actividad { get; set; } = null!;

    public int EstudianteId { get; set; }
    public Estudiante Estudiante { get; set; } = null!;

    public decimal? Nota { get; set; }
    public string Estado { get; set; } = EstadosActividad.Pendiente;
    public string? Observacion { get; set; }

    public DateTime FechaRegistro { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

/// <summary>Regla de alerta configurable (HU-10 / RN-04 a RN-06).</summary>
public class ReglaAlerta
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string ParametrosJson { get; set; } = "{}";
    public bool Activa { get; set; } = true;
    public DateTime FechaActualizacion { get; set; }
}
