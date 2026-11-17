using System.Text.Json.Serialization;
using Reservas.Domain.Comun;

namespace Reservas.Domain.Recursos;

public readonly record struct RecursoId(Guid Valor)
{
    public static RecursoId Nuevo() => new(Guid.NewGuid());

    public override string ToString() => Valor.ToString();
}

/// <summary>Lo que se reserva.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TipoRecurso>))]
public enum TipoRecurso
{
    Sala,
    Puesto,
    Vehiculo,
}

/// <summary>Cuántas personas caben en un recurso: de 1 a 200.</summary>
public sealed class Capacidad : ValueObject
{
    public const int Maxima = 200;

    public int Plazas { get; }

    private Capacidad(int plazas) => Plazas = plazas;

    public static Capacidad De(int plazas) =>
        plazas is < 1 or > Maxima
            ? throw new ReglaIncumplidaException(TipoRechazo.Validacion, $"La capacidad debe estar entre 1 y {Maxima} plazas.")
            : new Capacidad(plazas);

    public bool Admite(int asistentes) => asistentes <= Plazas;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Plazas;
    }

    public override string ToString() => Plazas == 1 ? "1 plaza" : $"{Plazas} plazas";
}

/// <summary>Una sala, un puesto o un vehículo que se puede reservar.</summary>
public sealed class Recurso : AggregateRoot<RecursoId>
{
    public string Nombre { get; private set; } = "";
    public TipoRecurso Tipo { get; private set; }
    public Capacidad Capacidad { get; private set; } = null!;
    public string? Ubicacion { get; private set; }
    public bool Activo { get; private set; }

    /// <summary>Para el ORM.</summary>
    private Recurso() { }

    private Recurso(RecursoId id, string nombre, TipoRecurso tipo, Capacidad capacidad, string? ubicacion) : base(id)
    {
        Nombre = nombre;
        Tipo = tipo;
        Capacidad = capacidad;
        Ubicacion = ubicacion;
        Activo = true;
    }

    public static Recurso Crear(string nombre, TipoRecurso tipo, Capacidad capacidad, string? ubicacion = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ReglaIncumplidaException(TipoRechazo.Validacion, "El recurso necesita un nombre.");
        return new Recurso(RecursoId.Nuevo(), nombre.Trim(), tipo, capacidad, string.IsNullOrWhiteSpace(ubicacion) ? null : ubicacion.Trim());
    }

    /// <summary>Deja de listarse y de poder reservarse.</summary>
    /// <param name="reservasFuturas">Cuántas reservas confirmadas tiene que aún no han terminado.</param>
    public void DarDeBaja(int reservasFuturas, DateTimeOffset ahora)
    {
        if (!Activo)
            throw new ReglaIncumplidaException(TipoRechazo.Conflicto, $"{Nombre} ya está dado de baja.");
        if (reservasFuturas > 0)
            throw new ReglaIncumplidaException(TipoRechazo.Conflicto,
                $"{Nombre} tiene {reservasFuturas} {(reservasFuturas == 1 ? "reserva futura" : "reservas futuras")}: cancélalas antes de darlo de baja.");

        Activo = false;
        Raise(new RecursoDadoDeBaja(Id, ahora));
    }
}

public sealed record RecursoDadoDeBaja(RecursoId Recurso, DateTimeOffset OcurridoEn) : IDomainEvent;
