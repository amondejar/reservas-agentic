using System.ComponentModel;
using Keel.Endpoints;
using Keel.Endpoints.Mcp;
using Microsoft.AspNetCore.Http.HttpResults;
using Reservas.Api.Aplicacion;
using Reservas.Contracts;

namespace Reservas.Api.Features.Reservas.Obtener;

public sealed record ObtenerReserva([property: Description("El id de la reserva.")] Guid Id);

/// <summary>GET /api/reservas/{id}</summary>
[Get("reservas/{id}")]
public sealed class Endpoint(ServicioReservas servicio) : ResultEndpoint<ObtenerReserva, Results<Ok<ReservaDto>, NotFound>>
{
    public override void Configure(EndpointOptions options) =>
        options.ExposeAsTool("Obtiene una reserva por su id.", tool => tool.FieldSelection = true);

    public override async Task<Results<Ok<ReservaDto>, NotFound>> HandleAsync(ObtenerReserva request, CancellationToken ct) =>
        await servicio.ObtenerAsync(request.Id, ct) is { } reserva ? TypedResults.Ok(reserva) : TypedResults.NotFound();
}
