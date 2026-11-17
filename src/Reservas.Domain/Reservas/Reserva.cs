using System.Text.Json.Serialization;
using Reservas.Domain.Comun;
using Reservas.Domain.Recursos;

namespace Reservas.Domain.Reservas;

public readonly record struct ReservaId(Guid Valor)
{
    public static ReservaId Nueva() => new(Guid.NewGuid());

    public override string ToString() => Valor.ToString();
}

[JsonConverter(typeof(JsonStringEnumConverter<EstadoReserva>))]
public enum EstadoReserva
{
    Confirmada,
    Cancelada,
}

/// <summary>
/// La reserva de un recurso para una franja. Referencia el recurso por su id: Recurso y Reserva son agregados
/// distintos, y cada operación cambia solo uno de ellos.
/// </summary>
public sealed class Reserva : AggregateRoot<ReservaId>
{
    /// <summary>Antelación mínima para cancelar.</summary>
    public static readonly TimeSpan PlazoCancelacion = TimeSpan.FromHours(2);

    public RecursoId RecursoId { get; private set; }
    public Franja Franja { get; private set; } = null!;
    public string Titular { get; private set; } = "";
    public string Asunto { get; private set; } = "";
    public int Asistentes { get; private set; }
    public EstadoReserva Estado { get; private set; }
    public string? MotivoCancelacion { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>Para el ORM.</summary>
    private Reserva() { }

    private Reserva(ReservaId id, RecursoId recurso, Franja franja, string titular, string asunto, int asistentes, DateTimeOffset ahora)
        : base(id)
    {
        RecursoId = recurso;
        Franja = franja;
        Titular = titular;
        Asunto = asunto;
        Asistentes = asistentes;
        Estado = EstadoReserva.Confirmada;
        CreadaEn = ahora;
    }

    /// <summary>Reserva el recurso si está activo, cabe la gente, la franja es futura y nadie la ocupa.</summary>
    /// <param name="ocupadas">Las reservas confirmadas del recurso que se solapan con la franja.</param>
    public static Reserva Crear(
        Recurso recurso, Franja franja, string titular, string asunto, int asistentes,
        IEnumerable<Reserva> ocupadas, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(titular))
            throw new ReglaIncumplidaException(TipoRechazo.Validacion, "La reserva necesita un titular.");
        if (asistentes < 1)
            throw new ReglaIncumplidaException(TipoRechazo.Validacion, "Una reserva es al menos para 1 asistente.");
        if (!recurso.Activo)
            throw new ReglaIncumplidaException(TipoRechazo.Conflicto, $"{recurso.Nombre} está dado de baja y no se puede reservar.");
        if (!recurso.Capacidad.Admite(asistentes))
            throw new ReglaIncumplidaException(TipoRechazo.Conflicto,
                $"{recurso.Nombre} admite {recurso.Capacidad}; la reserva es para {asistentes}.");
        if (franja.Inicio <= ahora)
            throw new ReglaIncumplidaException(TipoRechazo.Conflicto, "La franja ya ha empezado: solo se reservan franjas futuras.");

        var choque = ocupadas.FirstOrDefault(o => o.RecursoId == recurso.Id && o.Estado == EstadoReserva.Confirmada && o.Franja.SeSolapaCon(franja));
        if (choque is not null)
            throw new ReglaIncumplidaException(TipoRechazo.Conflicto,
                $"{recurso.Nombre} ya está reservado en esa franja por \"{choque.Asunto}\". Consulta la ocupación del día para ver qué hay libre.");

        var reserva = new Reserva(ReservaId.Nueva(), recurso.Id, franja, titular.Trim(), asunto.Trim(), asistentes, ahora);
        reserva.Raise(new ReservaCreada(reserva.Id, recurso.Id, franja.Inicio, franja.Fin, ahora));
        return reserva;
    }
    public void Cancelar(string motivo, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ReglaIncumplidaException(TipoRechazo.Validacion, "Cancelar una reserva necesita un motivo.");
        if (Estado == EstadoReserva.Cancelada)
            throw new ReglaIncumplidaException(TipoRechazo.Conflicto, "La reserva ya está cancelada.");
        if (Franja.Inicio - ahora < PlazoCancelacion)
            throw new ReglaIncumplidaException(TipoRechazo.Conflicto,
                "Las reservas se cancelan con al menos 2 horas de antelación, y a esta le queda menos.");

        Estado = EstadoReserva.Cancelada;
        MotivoCancelacion = motivo.Trim();
        Raise(new ReservaCancelada(Id, RecursoId, MotivoCancelacion, ahora));
    }
}

public sealed record ReservaCreada(ReservaId Reserva, RecursoId Recurso, DateTimeOffset Inicio, DateTimeOffset Fin, DateTimeOffset OcurridoEn) : IDomainEvent;

public sealed record ReservaCancelada(ReservaId Reserva, RecursoId Recurso, string Motivo, DateTimeOffset OcurridoEn) : IDomainEvent;
