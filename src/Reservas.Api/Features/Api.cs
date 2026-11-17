using Keel.Endpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Reservas.Domain.Comun;

namespace Reservas.Api.Features;

/// <summary>
/// Se aplica a todos los endpoints de Features. Es una demo sin autenticación: con usuarios reales, cambia
/// AllowAnonymous por una política, por ejemplo options.RequireAuthorization("empleados").
/// </summary>
public sealed class ApiGroup : EndpointGroup
{
    public override void Configure(EndpointOptions options) => options.AllowAnonymous();
}

/// <summary>Traduce lo que rechaza el dominio a respuestas HTTP: 404, 400 (datos) o 409 (estado).</summary>
internal static class Respuestas
{
    public static async Task<Results<Ok<T>, NotFound<ProblemDetails>, ProblemHttpResult>> Ejecutar<T>(Func<Task<T>> accion)
    {
        try
        {
            return TypedResults.Ok(await accion());
        }
        catch (KeyNotFoundException e)
        {
            return NoEncontrado(e);
        }
        catch (ReglaIncumplidaException e)
        {
            return Rechazo(e);
        }
    }

    public static NotFound<ProblemDetails> NoEncontrado(KeyNotFoundException e) =>
        TypedResults.NotFound(new ProblemDetails { Title = "No encontrado", Detail = e.Message, Status = StatusCodes.Status404NotFound });

    public static ProblemHttpResult Rechazo(ReglaIncumplidaException e) => e.Tipo == TipoRechazo.Validacion
        ? TypedResults.Problem(e.Message, title: "Datos no válidos", statusCode: StatusCodes.Status400BadRequest)
        : TypedResults.Problem(e.Message, title: "No se puede hacer ahora", statusCode: StatusCodes.Status409Conflict);
}
