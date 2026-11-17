using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Keel.Endpoints;
using Keel.Endpoints.Mcp;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Reservas.Api.Aplicacion;
using Reservas.Contracts;
using Reservas.Domain.Comun;

namespace Reservas.Api.Features.Reservas.Crear;

/// <summary>El contrato de la petición, desde el cuerpo JSON. Se valida antes del handler.</summary>
/// <remarks>Nombre completo: los proyectos con componentes Razor también reciben un ValidatableType embebido.</remarks>
[Microsoft.Extensions.Validation.ValidatableType]
public sealed record CrearReserva(
    [property: Description("El id del recurso (de recursos_listar u ocupacion_consultar).")] Guid RecursoId,
    [property: Description("Inicio, en ISO 8601 con la zona horaria, por ejemplo 2026-11-18T16:00:00+01:00.")] DateTimeOffset Inicio,
    [property: Description("Fin, en ISO 8601 con la zona horaria. Entre 15 minutos y 12 horas después del inicio.")] DateTimeOffset Fin,
    [property: Required, StringLength(100, MinimumLength = 2), Description("Quién reserva: nombre de la persona.")] string Titular,
    [property: Required, StringLength(200, MinimumLength = 2), Description("Para qué es la reserva, por ejemplo \"Reunión de equipo\".")] string Asunto,
    [property: Range(1, 200), Description("Cuántas personas asistirán.")] int Asistentes);

/// <summary>POST /api/reservas</summary>
[Post("reservas")]
public sealed class Endpoint(ServicioReservas servicio)
    : ResultEndpoint<CrearReserva, Results<Created<ReservaDto>, NotFound<ProblemDetails>, ProblemHttpResult>>
{
    public override void Configure(EndpointOptions options) =>
        // Solo añade una reserva (no cambia ni borra nada): el agente puede hacerlo sin pedir confirmación.
        options.ExposeAsTool("Reserva un recurso para una franja. Falla si la franja está ocupada, ya ha empezado, o si no caben los asistentes.",
            tool => tool.Destructive = false);

    public override async Task<Results<Created<ReservaDto>, NotFound<ProblemDetails>, ProblemHttpResult>> HandleAsync(CrearReserva request, CancellationToken ct)
    {
        try
        {
            var reserva = await servicio.ReservarAsync(
                new NuevaReservaDto(request.RecursoId, request.Inicio, request.Fin, request.Titular, request.Asunto, request.Asistentes), ct);
            return TypedResults.Created($"/api/reservas/{reserva.Id}", reserva);
        }
        catch (KeyNotFoundException e)
        {
            return Respuestas.NoEncontrado(e);
        }
        catch (ReglaIncumplidaException e)
        {
            return Respuestas.Rechazo(e);
        }
    }
}
