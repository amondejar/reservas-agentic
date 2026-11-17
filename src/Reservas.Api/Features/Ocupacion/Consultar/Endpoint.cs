using System.ComponentModel;
using Keel.Endpoints;
using Keel.Endpoints.Mcp;
using Microsoft.AspNetCore.Http.HttpResults;
using Reservas.Api.Aplicacion;
using Reservas.Contracts;

namespace Reservas.Api.Features.Ocupacion.Consultar;

public sealed record ConsultarOcupacion(
    [property: Description("El día, AAAA-MM-DD. Por defecto, hoy.")] DateOnly? Fecha = null,
    [property: Description("Solo recursos de este tipo: Sala, Puesto o Vehiculo.")] TipoRecurso? Tipo = null);

/// <summary>GET /api/ocupacion?fecha=2026-11-18&amp;tipo=Sala</summary>
[Get("ocupacion")]
public sealed class Endpoint(ServicioReservas servicio) : ResultEndpoint<ConsultarOcupacion, Ok<OcupacionDto>>
{
    public override void Configure(EndpointOptions options) =>
        options.ExposeAsTool("Muestra, para un día, cada recurso activo con sus reservas confirmadas (hora de inicio y fin, asunto, titular). Úsalo para saber qué está libre antes de reservar.");

    public override async Task<Ok<OcupacionDto>> HandleAsync(ConsultarOcupacion request, CancellationToken ct) =>
        TypedResults.Ok(await servicio.OcupacionAsync(request.Fecha, request.Tipo, ct));
}
