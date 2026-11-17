using Reservas.Domain.Recursos;
using Reservas.Domain.Reservas;

namespace Reservas.Domain;

/// <summary>Carga y guarda recursos enteros.</summary>
public interface IRecursoRepository
{
    Task<Recurso?> ObtenerAsync(RecursoId id, CancellationToken ct = default);

    /// <summary>Los recursos ordenados por nombre: solo los activos, o también los dados de baja.</summary>
    Task<IReadOnlyList<Recurso>> ListarAsync(bool soloActivos, CancellationToken ct = default);

    Task<bool> HayAlgunoAsync(CancellationToken ct = default);

    void Agregar(Recurso recurso);
}

/// <summary>Carga y guarda reservas enteras.</summary>
public interface IReservaRepository
{
    Task<Reserva?> ObtenerAsync(ReservaId id, CancellationToken ct = default);

    /// <summary>Las reservas confirmadas de un recurso que se solapan con [desde, hasta).</summary>
    Task<IReadOnlyList<Reserva>> ConfirmadasEntreAsync(RecursoId? recurso, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default);

    /// <summary>Las reservas de un titular que aún no han terminado, por hora de inicio.</summary>
    Task<IReadOnlyList<Reserva>> ProximasDeAsync(string? titular, DateTimeOffset ahora, CancellationToken ct = default);

    void Agregar(Reserva reserva);
}

/// <summary>Guarda los cambios de los agregados cargados o agregados, en una transacción.</summary>
public interface IUnidadDeTrabajo
{
    Task GuardarAsync(CancellationToken ct = default);
}
