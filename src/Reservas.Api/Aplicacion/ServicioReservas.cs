using Reservas.Api.Specs;
using Reservas.Contracts;
using Reservas.Domain;
using Reservas.Domain.Recursos;
using Reservas.Domain.Reservas;
using SpecTrace;

namespace Reservas.Api.Aplicacion;

/// <summary>
/// Los casos de uso: cargan los agregados, les piden la operación y guardan. Las reglas están en el dominio
/// (Recurso, Reserva, Franja); aquí solo se orquesta. Lo usan los endpoints (API y herramientas MCP) y el panel.
/// </summary>
/// <exception cref="KeyNotFoundException">El recurso o la reserva no existen.</exception>
/// <exception cref="Domain.Comun.ReglaIncumplidaException">El dominio rechaza la operación.</exception>
public sealed class ServicioReservas(
    IRecursoRepository recursos, IReservaRepository reservas, IUnidadDeTrabajo unidad, TimeProvider reloj, ZonaHoraria zona)
{
    [Implements(Requirements.RS_001)]
    public async Task<IReadOnlyList<RecursoDto>> ListarRecursosAsync(Contracts.TipoRecurso? tipo, int capacidadMinima, CancellationToken ct = default) =>
        (await recursos.ListarAsync(soloActivos: true, ct))
            .Where(r => (tipo is null || r.Tipo == Mapeo.ADominio(tipo.Value)) && r.Capacidad.Plazas >= capacidadMinima)
            .Select(r => r.ADto())
            .ToList();

    [Implements(Requirements.RS_002)]
    public async Task<OcupacionDto> OcupacionAsync(DateOnly? fecha, Contracts.TipoRecurso? tipo, CancellationToken ct = default)
    {
        var dia = fecha ?? zona.Hoy(reloj);
        var (desde, hasta) = zona.Dia(dia);
        var activos = (await recursos.ListarAsync(soloActivos: true, ct))
            .Where(r => tipo is null || r.Tipo == Mapeo.ADominio(tipo.Value))
            .ToList();
        var delDia = await reservas.ConfirmadasEntreAsync(null, desde, hasta, ct);

        var porRecurso = activos.Select(recurso => new OcupacionRecursoDto(
                recurso.ADto(),
                delDia.Where(r => r.RecursoId == recurso.Id)
                    .OrderBy(r => r.Franja.Inicio)
                    .Select(r => r.ADto(recurso.Nombre, zona))
                    .ToList()))
            .ToList();
        return new OcupacionDto(dia, zona.Nombre, porRecurso);
    }

    [Implements(Requirements.RS_003)]
    public async Task<ReservaDto> ReservarAsync(NuevaReservaDto nueva, CancellationToken ct = default)
    {
        var recurso = await recursos.ObtenerAsync(new RecursoId(nueva.RecursoId), ct)
                      ?? throw new KeyNotFoundException($"No existe el recurso {nueva.RecursoId}.");
        var franja = Franja.Entre(nueva.Inicio, nueva.Fin);
        var ocupadas = await reservas.ConfirmadasEntreAsync(recurso.Id, franja.Inicio, franja.Fin, ct);

        var reserva = Reserva.Crear(recurso, franja, nueva.Titular, nueva.Asunto, nueva.Asistentes, ocupadas, reloj.GetUtcNow());
        reservas.Agregar(reserva);
        await unidad.GuardarAsync(ct);
        return reserva.ADto(recurso.Nombre, zona);
    }

    [Implements(Requirements.RS_004)]
    public async Task<ReservaDto> CancelarAsync(Guid id, string motivo, CancellationToken ct = default)
    {
        var reserva = await reservas.ObtenerAsync(new ReservaId(id), ct) ?? throw new KeyNotFoundException($"No existe la reserva {id}.");
        reserva.Cancelar(motivo, reloj.GetUtcNow());
        await unidad.GuardarAsync(ct);
        return reserva.ADto(await NombreAsync(reserva.RecursoId, ct), zona);
    }

    [Implements(Requirements.RS_005)]
    public async Task<RecursoDto> DarDeBajaAsync(Guid id, CancellationToken ct = default)
    {
        var recurso = await recursos.ObtenerAsync(new RecursoId(id), ct) ?? throw new KeyNotFoundException($"No existe el recurso {id}.");
        var ahora = reloj.GetUtcNow();
        var futuras = await reservas.ConfirmadasEntreAsync(recurso.Id, ahora, ahora.AddYears(100), ct);
        recurso.DarDeBaja(futuras.Count, ahora);
        await unidad.GuardarAsync(ct);
        return recurso.ADto();
    }

    public async Task<ReservaDto?> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        await reservas.ObtenerAsync(new ReservaId(id), ct) is { } reserva
            ? reserva.ADto(await NombreAsync(reserva.RecursoId, ct), zona)
            : null;

    /// <summary>Las reservas que aún no han terminado, de un titular o de todos.</summary>
    public async Task<IReadOnlyList<ReservaDto>> ProximasAsync(string? titular, CancellationToken ct = default)
    {
        var proximas = await reservas.ProximasDeAsync(titular, reloj.GetUtcNow(), ct);
        var nombres = (await recursos.ListarAsync(soloActivos: false, ct)).ToDictionary(r => r.Id, r => r.Nombre);
        return proximas.Select(r => r.ADto(nombres.GetValueOrDefault(r.RecursoId, "?"), zona)).ToList();
    }

    private async Task<string> NombreAsync(RecursoId id, CancellationToken ct) =>
        (await recursos.ObtenerAsync(id, ct))?.Nombre ?? "?";
}
