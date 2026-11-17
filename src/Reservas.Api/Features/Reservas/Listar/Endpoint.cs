using System.ComponentModel;
using Keel.Endpoints;
using Keel.Endpoints.Mcp;
using Microsoft.AspNetCore.Http.HttpResults;
using Reservas.Api.Aplicacion;
using Reservas.Contracts;

namespace Reservas.Api.Features.Reservas.Listar;

public sealed record ListarReservas(
    [property: Description("Solo las de este titular (o parte del nombre). Por defecto, todas.")] string? Titular = null);

/// <summary>GET /api/reservas?titular=Ana</summary>
[Get("reservas")]
public sealed class Endpoint(ServicioReservas servicio) : ResultEndpoint<ListarReservas, Ok<IReadOnlyList<ReservaDto>>>
{
    public override void Configure(EndpointOptions options) =>
        options.ExposeAsTool("Lista las reservas que aún no han terminado (confirmadas y canceladas), por hora de inicio, de un titular o de todos.", tool =>
        {
            tool.MaxItems = 20;
            tool.FieldSelection = true;
        });

    public override async Task<Ok<IReadOnlyList<ReservaDto>>> HandleAsync(ListarReservas request, CancellationToken ct) =>
        TypedResults.Ok(await servicio.ProximasAsync(request.Titular, ct));
}
