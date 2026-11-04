using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Curso> Cursos => Set<Curso>();
    public DbSet<Estudiante> Estudiantes => Set<Estudiante>();
    public DbSet<Matricula> Matriculas => Set<Matricula>();
    public DbSet<Actividad> Actividades => Set<Actividad>();
    public DbSet<Calificacion> Calificaciones => Set<Calificacion>();
    public DbSet<ReglaAlerta> ReglasAlerta => Set<ReglaAlerta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Rol>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Nombre).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Nombre).IsUnique();
            e.HasData(
                new Rol { Id = 1, Nombre = RolNombres.Docente },
                new Rol { Id = 2, Nombre = RolNombres.AdministradorTecnico });
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(254).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            e.HasIndex(x => x.Correo).IsUnique();
            e.HasOne(x => x.Rol).WithMany().HasForeignKey(x => x.RolId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Cursos).WithOne(x => x.Docente).HasForeignKey(x => x.DocenteId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Curso>(e =>
        {
            e.ToTable("Cursos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Codigo).HasMaxLength(50).IsRequired();
            e.Property(x => x.Periodo).HasMaxLength(50).IsRequired();
            e.Property(x => x.EscalaCalificacion).HasPrecision(5, 2);
            e.Property(x => x.Paralelo).HasMaxLength(100);
            e.HasIndex(x => new { x.DocenteId, x.Codigo, x.Periodo }).IsUnique();
        });

        modelBuilder.Entity<Estudiante>(e =>
        {
            e.ToTable("Estudiantes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Identificacion).HasMaxLength(20).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(254).IsRequired();
            e.HasIndex(x => x.Identificacion).IsUnique();
            e.HasIndex(x => x.Correo);
        });

        modelBuilder.Entity<Matricula>(e =>
        {
            e.ToTable("Matriculas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Curso).WithMany(c => c.Matriculas).HasForeignKey(x => x.CursoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Estudiante).WithMany(s => s.Matriculas).HasForeignKey(x => x.EstudianteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CursoId, x.EstudianteId }).IsUnique();
        });

        modelBuilder.Entity<Actividad>(e =>
        {
            e.ToTable("Actividades");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.Tipo).HasMaxLength(40).IsRequired();
            e.Property(x => x.Componente).HasMaxLength(40).IsRequired();
            e.Property(x => x.PuntajeMaximo).HasPrecision(5, 2);
            e.HasOne(x => x.Curso).WithMany().HasForeignKey(x => x.CursoId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CursoId, x.Parcial });
            e.HasIndex(x => new { x.CursoId, x.FechaEntrega });
        });

        modelBuilder.Entity<Calificacion>(e =>
        {
            e.ToTable("Calificaciones");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nota).HasPrecision(5, 2);
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            e.Property(x => x.Observacion).HasMaxLength(500);
            e.HasOne(x => x.Actividad).WithMany(a => a.Calificaciones).HasForeignKey(x => x.ActividadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Estudiante).WithMany().HasForeignKey(x => x.EstudianteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ActividadId, x.EstudianteId }).IsUnique();
            e.HasIndex(x => x.EstudianteId);
        });

        modelBuilder.Entity<ReglaAlerta>(e =>
        {
            e.ToTable("ReglasAlerta");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tipo).HasMaxLength(40).IsRequired();
            e.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
            e.Property(x => x.ParametrosJson).HasMaxLength(400).IsRequired();
            e.HasIndex(x => x.Tipo).IsUnique();
        });
    }
}
