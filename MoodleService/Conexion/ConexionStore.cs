using Microsoft.EntityFrameworkCore;
using MoodleService.Seguridad;

namespace MoodleService.Conexion;

/// <summary>Configuración persistida de la conexión con Moodle (una sola fila).</summary>
public class ConexionMoodle
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string TokenCifrado { get; set; } = string.Empty;
    public string? ActualizadoPor { get; set; }
    public DateTime FechaActualizacion { get; set; }
}

public class MoodleDbContext(DbContextOptions<MoodleDbContext> options) : DbContext(options)
{
    public DbSet<ConexionMoodle> Conexiones => Set<ConexionMoodle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConexionMoodle>(e =>
        {
            e.ToTable("ConexionMoodle");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Url).HasMaxLength(300).IsRequired();
            e.Property(x => x.TokenCifrado).HasMaxLength(500).IsRequired();
            e.Property(x => x.ActualizadoPor).HasMaxLength(254);
        });
    }

    /// <summary>Crea la tabla si no existe (la base de datos es compartida con el backend).</summary>
    public async Task AsegurarEsquemaAsync(CancellationToken ct)
    {
        if (!Database.IsRelational())
        {
            await Database.EnsureCreatedAsync(ct);
            return;
        }

        await Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'dbo.ConexionMoodle', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ConexionMoodle (
        Id                 INT            NOT NULL CONSTRAINT PK_ConexionMoodle PRIMARY KEY,
        Url                NVARCHAR(300)  NOT NULL,
        TokenCifrado       NVARCHAR(500)  NOT NULL,
        ActualizadoPor     NVARCHAR(254)  NULL,
        FechaActualizacion DATETIME2      NOT NULL CONSTRAINT DF_ConexionMoodle_FActual DEFAULT (SYSUTCDATETIME())
    );
END;", ct);
    }
}

/// <summary>Credenciales efectivas: las guardadas por el administrador o, si no hay, las de entorno.</summary>
public record CredencialesMoodle(string Url, string Token, string Origen, DateTime? FechaActualizacion, string? ActualizadoPor);

public class ConexionStore(MoodleDbContext db, CifradoToken cifrado, IConfiguration config)
{
    public const int IdUnico = 1;

    public async Task<CredencialesMoodle?> ObtenerAsync(CancellationToken ct)
    {
        var fila = await db.Conexiones.AsNoTracking().FirstOrDefaultAsync(c => c.Id == IdUnico, ct);
        if (fila is not null)
            return new CredencialesMoodle(fila.Url, cifrado.Descifrar(fila.TokenCifrado), "configuracion",
                fila.FechaActualizacion, fila.ActualizadoPor);

        var url = config["Moodle:Url"];
        var token = config["Moodle:Token"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token)) return null;
        return new CredencialesMoodle(url.Trim().TrimEnd('/'), token.Trim(), "entorno", null, null);
    }

    public async Task GuardarAsync(string url, string? tokenNuevo, string? usuario, CancellationToken ct)
    {
        var fila = await db.Conexiones.FirstOrDefaultAsync(c => c.Id == IdUnico, ct);
        string token;
        if (!string.IsNullOrWhiteSpace(tokenNuevo))
        {
            token = tokenNuevo.Trim();
        }
        else
        {
            // Sin token nuevo se conserva el vigente (guardado o de entorno).
            var actual = await ObtenerAsync(ct)
                ?? throw new InvalidOperationException("Debes ingresar el token del servicio web de Moodle.");
            token = actual.Token;
        }

        if (fila is null)
        {
            fila = new ConexionMoodle { Id = IdUnico };
            db.Conexiones.Add(fila);
        }

        fila.Url = url.Trim().TrimEnd('/');
        fila.TokenCifrado = cifrado.Cifrar(token);
        fila.ActualizadoPor = usuario;
        fila.FechaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
