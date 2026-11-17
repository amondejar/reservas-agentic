using System.Text.Json.Serialization;

namespace Reservas.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<TipoRecurso>))]
public enum TipoRecurso
{
    Sala,
    Puesto,
    Vehiculo,
}

[JsonConverter(typeof(JsonStringEnumConverter<EstadoReserva>))]
public enum EstadoReserva
{
    Confirmada,
    Cancelada,
}

public sealed record RecursoDto(Guid Id, string Nombre, TipoRecurso Tipo, int Capacidad, string? Ubicacion);

/// <summary>Una reserva. Inicio y Fin, en la zona horaria de la aplicación.</summary>
public sealed record ReservaDto(
    Guid Id,
    Guid RecursoId,
    string Recurso,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string Titular,
    string Asunto,
    int Asistentes,
    EstadoReserva Estado,
    string? MotivoCancelacion = null);

/// <summary>Las reservas confirmadas de un día, por recurso.</summary>
public sealed record OcupacionDto(DateOnly Fecha, string ZonaHoraria, IReadOnlyList<OcupacionRecursoDto> Recursos);

public sealed record OcupacionRecursoDto(RecursoDto Recurso, IReadOnlyList<ReservaDto> Reservas);

/// <summary>El cuerpo de POST /api/reservas.</summary>
public sealed record NuevaReservaDto(Guid RecursoId, DateTimeOffset Inicio, DateTimeOffset Fin, string Titular, string Asunto, int Asistentes);

/// <summary>El cuerpo de POST /api/reservas/{id}/cancelar.</summary>
public sealed record CancelacionDto(string Motivo);
