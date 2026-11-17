using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Keel.Endpoints;
using Keel.Endpoints.Agents;
using Keel.Endpoints.Mcp;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Reservas.Api.Aplicacion;
using Reservas.Contracts;

namespace Reservas.Api.Features.Reservas.Cancelar;

/// <summary>El motivo, desde el cuerpo JSON.</summary>
public sealed record MotivoCancelacion(
    [property: Required, StringLength(300, MinimumLength = 3), Description("Por qué se cancela. Lo verá el titular.")] string Motivo);

/// <summary>El id desde la ruta y el motivo desde el cuerpo. Se valida antes del handler.</summary>
[BindRequest(RequestBinding.Parameters), Microsoft.Extensions.Validation.ValidatableType]
public sealed record CancelarReserva(
    [property: Description("El id de la reserva.")] Guid Id,
    [property: FromBody] MotivoCancelacion Body);

/// <summary>POST /api/reservas/{id}/cancelar</summary>
[Post("reservas/{id}/cancelar")]
public sealed class Endpoint(ServicioReservas servicio)
    : ResultEndpoint<CancelarReserva, Results<Ok<ReservaDto>, NotFound<Microsoft.AspNetCore.Mvc.ProblemDetails>, ProblemHttpResult>>
{
    // Cancelar libera la franja para otros: si lo pide un agente, la persona confirma antes, viendo el motivo.
    public override void Configure(EndpointOptions options) => options
        .ExposeAsTool("Cancela una reserva confirmada, con un motivo. Solo con al menos 2 horas de antelación.", tool => tool.Destructive = true)
        .RequireConfirmation<CancelarReserva>(request => $"Cancelar la reserva {request.Id}: \"{request.Body.Motivo}\"");

    public override Task<Results<Ok<ReservaDto>, NotFound<Microsoft.AspNetCore.Mvc.ProblemDetails>, ProblemHttpResult>> HandleAsync(CancelarReserva request, CancellationToken ct) =>
        Respuestas.Ejecutar(() => servicio.CancelarAsync(request.Id, request.Body.Motivo, ct));
}
