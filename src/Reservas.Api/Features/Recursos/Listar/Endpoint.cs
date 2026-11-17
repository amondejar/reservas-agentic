using System.ComponentModel;
using Keel.Endpoints;
using Keel.Endpoints.Mcp;
using Microsoft.AspNetCore.Http.HttpResults;
using Reservas.Api.Aplicacion;
using Reservas.Contracts;

namespace Reservas.Api.Features.Recursos.Listar;

/// <summary>El contrato de la petición, desde la query string (GET).</summary>
public sealed record ListarRecursos(
    [property: Description("Solo recursos de este tipo: Sala, Puesto o Vehiculo.")] TipoRecurso? Tipo = null,
    [property: Description("Solo recursos con al menos estas plazas.")] int? CapacidadMinima = null);

/// <summary>GET /api/recursos?tipo=Sala&amp;capacidadMinima=10</summary>
[Get("recursos")]
public sealed class Endpoint(ServicioReservas servicio) : ResultEndpoint<ListarRecursos, Ok<IReadOnlyList<RecursoDto>>>
{
    public override void Configure(EndpointOptions options) =>
        options.ExposeAsTool("Lista los recursos que se pueden reservar (salas, puestos, vehículos) con su id, tipo y capacidad, ordenados por nombre.", tool =>
        {
            tool.MaxItems = 50;
            tool.FieldSelection = true;
        });

    public override async Task<Ok<IReadOnlyList<RecursoDto>>> HandleAsync(ListarRecursos request, CancellationToken ct) =>
        TypedResults.Ok(await servicio.ListarRecursosAsync(request.Tipo, request.CapacidadMinima ?? 0, ct));
}
