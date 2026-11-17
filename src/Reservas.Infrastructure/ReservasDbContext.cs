using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Reservas.Domain.Recursos;
using Reservas.Domain.Reservas;

namespace Reservas.Infrastructure;

/// <summary>
/// Las tablas recursos y reservas, y las claves de Data Protection (firman las confirmaciones de los agentes: si se
/// perdieran al reiniciar, las aprobaciones pendientes dejarían de valer).
/// </summary>
public sealed class ReservasDbContext(DbContextOptions<ReservasDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Recurso> Recursos => Set<Recurso>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recurso>(recurso =>
        {
            recurso.ToTable("recursos");
            recurso.HasKey(r => r.Id);
            recurso.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever()
                .HasConversion(id => id.Valor, valor => new RecursoId(valor));
            recurso.Property(r => r.Nombre).HasColumnName("nombre").HasMaxLength(100);
            recurso.Property(r => r.Tipo).HasColumnName("tipo").HasConversion<string>().HasMaxLength(20);
            recurso.ComplexProperty(r => r.Capacidad, capacidad => capacidad.Property(c => c.Plazas).HasColumnName("capacidad"));
            recurso.Property(r => r.Ubicacion).HasColumnName("ubicacion").HasMaxLength(200);
            recurso.Property(r => r.Activo).HasColumnName("activo");
            recurso.Ignore(r => r.Eventos);
        });

        modelBuilder.Entity<Reserva>(reserva =>
        {
            reserva.ToTable("reservas");
            reserva.HasKey(r => r.Id);
            reserva.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever()
                .HasConversion(id => id.Valor, valor => new ReservaId(valor));
            // Otro agregado: solo su id, sin navegación ni clave ajena.
            reserva.Property(r => r.RecursoId).HasColumnName("recurso_id")
                .HasConversion(id => id.Valor, valor => new RecursoId(valor));
            reserva.ComplexProperty(r => r.Franja, franja =>
            {
                franja.Property(f => f.Inicio).HasColumnName("inicio");
                franja.Property(f => f.Fin).HasColumnName("fin");
            });
            reserva.Property(r => r.Titular).HasColumnName("titular").HasMaxLength(100);
            reserva.Property(r => r.Asunto).HasColumnName("asunto").HasMaxLength(200);
            reserva.Property(r => r.Asistentes).HasColumnName("asistentes");
            reserva.Property(r => r.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(20);
            reserva.Property(r => r.MotivoCancelacion).HasColumnName("motivo_cancelacion").HasMaxLength(300);
            reserva.Property(r => r.CreadaEn).HasColumnName("creada_en");
            reserva.HasIndex(r => r.RecursoId).HasDatabaseName("ix_reservas_recurso");
            reserva.Ignore(r => r.Eventos);
        });
    }
}
