using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Reservas.Domain;
using Reservas.Domain.Comun;
using Reservas.Domain.Recursos;
using Reservas.Domain.Reservas;

namespace Reservas.Infrastructure;

internal sealed class RecursosEf(ReservasDbContext db) : IRecursoRepository
{
    public Task<Recurso?> ObtenerAsync(RecursoId id, CancellationToken ct = default) =>
        db.Recursos.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Recurso>> ListarAsync(bool soloActivos, CancellationToken ct = default) =>
        await (soloActivos ? db.Recursos.Where(r => r.Activo) : db.Recursos).OrderBy(r => r.Nombre).ToListAsync(ct);

    public Task<bool> HayAlgunoAsync(CancellationToken ct = default) => db.Recursos.AnyAsync(ct);

    public void Agregar(Recurso recurso) => db.Recursos.Add(recurso);
}

internal sealed class ReservasEf(ReservasDbContext db) : IReservaRepository
{
    public Task<Reserva?> ObtenerAsync(ReservaId id, CancellationToken ct = default) =>
        db.Reservas.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Reserva>> ConfirmadasEntreAsync(RecursoId? recurso, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default)
    {
        var (inicio, fin) = (desde.ToUniversalTime(), hasta.ToUniversalTime());
        var consulta = db.Reservas.Where(r => r.Estado == EstadoReserva.Confirmada && r.Franja.Inicio < fin && inicio < r.Franja.Fin);
        if (recurso is { } id) consulta = consulta.Where(r => r.RecursoId == id);
        return await consulta.OrderBy(r => r.Franja.Inicio).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Reserva>> ProximasDeAsync(string? titular, DateTimeOffset ahora, CancellationToken ct = default)
    {
        var desde = ahora.ToUniversalTime();
        var consulta = db.Reservas.Where(r => r.Franja.Fin > desde);
        if (!string.IsNullOrWhiteSpace(titular))
        {
            var patron = "%" + titular.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            consulta = consulta.Where(r => EF.Functions.ILike(r.Titular, patron));
        }

        return await consulta.OrderBy(r => r.Franja.Inicio).ToListAsync(ct);
    }

    public void Agregar(Reserva reserva) => db.Reservas.Add(reserva);
}

/// <summary>
/// Guarda en una transacción y anota los eventos publicados. Si otra petición ocupa la franja a la vez, la restricción
/// de exclusión de Postgres lo impide, y aquí se convierte en un rechazo que la persona entiende.
/// </summary>
internal sealed partial class UnidadEf(ReservasDbContext db, ILogger<UnidadEf> logger) : IUnidadDeTrabajo
{
    public async Task GuardarAsync(CancellationToken ct = default)
    {
        var agregados = db.ChangeTracker.Entries().Select(e => e.Entity).ToList();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            db.ChangeTracker.Clear();
            throw new ReglaIncumplidaException(TipoRechazo.Conflicto, "Otra reserva acaba de ocupar esa franja. Elige otra hora.");
        }

        foreach (var agregado in agregados)
        {
            switch (agregado)
            {
                case Recurso recurso:
                    foreach (var evento in recurso.Eventos) LogEvento(logger, evento.GetType().Name, evento);
                    recurso.LimpiarEventos();
                    break;
                case Reserva reserva:
                    foreach (var evento in reserva.Eventos) LogEvento(logger, evento.GetType().Name, evento);
                    reserva.LimpiarEventos();
                    break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Evento de dominio {Nombre}: {Evento}")]
    private static partial void LogEvento(ILogger logger, string nombre, IDomainEvent evento);
}
