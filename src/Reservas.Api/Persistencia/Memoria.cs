using Reservas.Domain;
using Reservas.Domain.Comun;
using Reservas.Domain.Recursos;
using Reservas.Domain.Reservas;

namespace Reservas.Api.Persistencia;

/// <summary>
/// Los datos en memoria, para probar la aplicación sin base de datos (sin cadena de conexión ConnectionStrings:Reservas).
/// Se pierden al reiniciar.
/// </summary>
public sealed class AlmacenEnMemoria
{
    internal readonly Lock Cerrojo = new();
    internal readonly Dictionary<RecursoId, Recurso> Recursos = [];
    internal readonly Dictionary<ReservaId, Reserva> Reservas = [];
}

/// <summary>Los agregados cargados o creados en esta petición, para guardarlos juntos.</summary>
internal sealed class SeguimientoEnMemoria
{
    public readonly List<Recurso> RecursosNuevos = [];
    public readonly List<Reserva> ReservasNuevas = [];
    public readonly HashSet<object> Cargados = new(ReferenceEqualityComparer.Instance);
}

internal sealed class RecursosEnMemoria(AlmacenEnMemoria almacen, SeguimientoEnMemoria seguimiento) : IRecursoRepository
{
    public Task<Recurso?> ObtenerAsync(RecursoId id, CancellationToken ct = default)
    {
        lock (almacen.Cerrojo)
        {
            var recurso = almacen.Recursos.GetValueOrDefault(id);
            if (recurso is not null) seguimiento.Cargados.Add(recurso);
            return Task.FromResult(recurso);
        }
    }

    public Task<IReadOnlyList<Recurso>> ListarAsync(bool soloActivos, CancellationToken ct = default)
    {
        lock (almacen.Cerrojo)
        {
            IReadOnlyList<Recurso> lista = almacen.Recursos.Values
                .Where(r => !soloActivos || r.Activo)
                .OrderBy(r => r.Nombre, StringComparer.CurrentCulture)
                .ToList();
            return Task.FromResult(lista);
        }
    }

    public Task<bool> HayAlgunoAsync(CancellationToken ct = default)
    {
        lock (almacen.Cerrojo) return Task.FromResult(almacen.Recursos.Count > 0);
    }

    public void Agregar(Recurso recurso) => seguimiento.RecursosNuevos.Add(recurso);
}

internal sealed class ReservasEnMemoria(AlmacenEnMemoria almacen, SeguimientoEnMemoria seguimiento) : IReservaRepository
{
    public Task<Reserva?> ObtenerAsync(ReservaId id, CancellationToken ct = default)
    {
        lock (almacen.Cerrojo)
        {
            var reserva = almacen.Reservas.GetValueOrDefault(id);
            if (reserva is not null) seguimiento.Cargados.Add(reserva);
            return Task.FromResult(reserva);
        }
    }

    public Task<IReadOnlyList<Reserva>> ConfirmadasEntreAsync(RecursoId? recurso, DateTimeOffset desde, DateTimeOffset hasta, CancellationToken ct = default)
    {
        lock (almacen.Cerrojo)
        {
            IReadOnlyList<Reserva> lista = almacen.Reservas.Values
                .Where(r => r.Estado == EstadoReserva.Confirmada && (recurso is null || r.RecursoId == recurso) &&
                            r.Franja.Inicio < hasta && desde < r.Franja.Fin)
                .OrderBy(r => r.Franja.Inicio)
                .ToList();
            return Task.FromResult(lista);
        }
    }

    public Task<IReadOnlyList<Reserva>> ProximasDeAsync(string? titular, DateTimeOffset ahora, CancellationToken ct = default)
    {
        lock (almacen.Cerrojo)
        {
            IReadOnlyList<Reserva> lista = almacen.Reservas.Values
                .Where(r => r.Franja.Fin > ahora &&
                            (string.IsNullOrWhiteSpace(titular) || r.Titular.Contains(titular.Trim(), StringComparison.CurrentCultureIgnoreCase)))
                .OrderBy(r => r.Franja.Inicio)
                .ToList();
            return Task.FromResult(lista);
        }
    }

    public void Agregar(Reserva reserva) => seguimiento.ReservasNuevas.Add(reserva);
}

/// <summary>
/// Guarda lo nuevo y anota los eventos publicados. Comprueba otra vez los solapes al guardar, con el cerrojo, para
/// que dos peticiones a la vez no reserven la misma franja (en Postgres lo hace una restricción de exclusión).
/// </summary>
internal sealed class UnidadEnMemoria(AlmacenEnMemoria almacen, SeguimientoEnMemoria seguimiento, ILogger<UnidadEnMemoria> logger) : IUnidadDeTrabajo
{
    public Task GuardarAsync(CancellationToken ct = default)
    {
        lock (almacen.Cerrojo)
        {
            foreach (var nueva in seguimiento.ReservasNuevas)
            {
                if (almacen.Reservas.Values.Any(r => r.RecursoId == nueva.RecursoId && r.Estado == EstadoReserva.Confirmada && r.Franja.SeSolapaCon(nueva.Franja)))
                    throw new ReglaIncumplidaException(TipoRechazo.Conflicto, "Otra reserva acaba de ocupar esa franja. Elige otra hora.");
            }

            foreach (var recurso in seguimiento.RecursosNuevos) almacen.Recursos[recurso.Id] = recurso;
            foreach (var reserva in seguimiento.ReservasNuevas) almacen.Reservas[reserva.Id] = reserva;

            var agregados = seguimiento.Cargados
                .Concat(seguimiento.RecursosNuevos)
                .Concat(seguimiento.ReservasNuevas)
                .Distinct(ReferenceEqualityComparer.Instance);
            Eventos.Despachar(agregados, logger);

            seguimiento.RecursosNuevos.Clear();
            seguimiento.ReservasNuevas.Clear();
        }

        return Task.CompletedTask;
    }
}

/// <summary>Los eventos de dominio de los agregados guardados. Hoy solo se registran en el log.</summary>
public static partial class Eventos
{
    public static void Despachar(IEnumerable<object?> agregados, ILogger logger)
    {
        foreach (var agregado in agregados)
        {
            var (eventos, limpiar) = agregado switch
            {
                Recurso recurso => (recurso.Eventos, (Action)recurso.LimpiarEventos),
                Reserva reserva => (reserva.Eventos, reserva.LimpiarEventos),
                _ => ((IReadOnlyList<IDomainEvent>)[], () => { }),
            };

            foreach (var evento in eventos) LogEvento(logger, evento.GetType().Name, evento);
            limpiar();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Evento de dominio {Nombre}: {Evento}")]
    private static partial void LogEvento(ILogger logger, string nombre, IDomainEvent evento);
}

public static class PersistenciaEnMemoria
{
    public static IServiceCollection AddPersistenciaEnMemoria(this IServiceCollection services)
    {
        services.AddSingleton<AlmacenEnMemoria>();
        services.AddScoped<SeguimientoEnMemoria>();
        services.AddScoped<IRecursoRepository, RecursosEnMemoria>();
        services.AddScoped<IReservaRepository, ReservasEnMemoria>();
        services.AddScoped<IUnidadDeTrabajo, UnidadEnMemoria>();
        return services;
    }
}
