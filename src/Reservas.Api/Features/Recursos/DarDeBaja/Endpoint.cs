using System.ComponentModel;
using Keel.Endpoints;
using Keel.Endpoints.Agents;
using Keel.Endpoints.Mcp;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Reservas.Api.Aplicacion;
using Reservas.Contracts;

namespace Reservas.Api.Features.Recursos.DarDeBaja;

/// <summary>El id viene de la ruta: se enlaza como parámetros (POST sin cuerpo).</summary>
[BindRequest(RequestBinding.Parameters)]
public sealed record DarDeBajaRecurso([property: Description("El id del recurso (de recursos_listar).")] Guid Id);

/// <summary>POST /api/recursos/{id}/baja</summary>
[Post("recursos/{id}/baja")]
public sealed class Endpoint(ServicioReservas servicio)
    : ResultEndpoint<DarDeBajaRecurso, Results<Ok<RecursoDto>, NotFound<ProblemDetails>, ProblemHttpResult>>
{
    // Dar de baja no se deshace desde la aplicación: si lo pide un agente, la persona confirma antes.
    // El mensaje solo puede usar los datos de la petición (Keel lo construye sin el endpoint).
    public override void Configure(EndpointOptions options) => options
        .ExposeAsTool("Da de baja un recurso: deja de listarse y de poder reservarse. Falla si tiene reservas futuras.", tool => tool.Destructive = true)
        .RequireConfirmation<DarDeBajaRecurso>(request => $"Dar de baja el recurso {request.Id}. No se podrá volver a reservar.");

    public override Task<Results<Ok<RecursoDto>, NotFound<ProblemDetails>, ProblemHttpResult>> HandleAsync(DarDeBajaRecurso request, CancellationToken ct) =>
        Respuestas.Ejecutar(() => servicio.DarDeBajaAsync(request.Id, ct));
}
