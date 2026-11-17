namespace Reservas.Domain.Comun;

/// <summary>Algo que ha pasado en el dominio y que interesa a otras partes del sistema.</summary>
public interface IDomainEvent
{
    DateTimeOffset OcurridoEn { get; }
}

/// <summary>Un objeto con identidad: dos entidades son la misma si tienen el mismo id.</summary>
public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id) => Id = id;

    /// <summary>Para el ORM.</summary>
    protected Entity() { }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>La raíz de un agregado: el único punto de entrada para cambiarlo, y quien publica sus eventos.</summary>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _eventos = [];

    /// <summary>Los eventos publicados desde que se cargó el agregado, pendientes de despachar.</summary>
    public IReadOnlyList<IDomainEvent> Eventos => _eventos;

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>Para el ORM.</summary>
    protected AggregateRoot() { }

    protected void Raise(IDomainEvent evento) => _eventos.Add(evento);

    public void LimpiarEventos() => _eventos.Clear();
}

/// <summary>Un valor sin identidad: inmutable y comparado por todos sus componentes.</summary>
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj) =>
        obj is ValueObject other && other.GetType() == GetType() && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    public override int GetHashCode() => GetEqualityComponents().Aggregate(0, HashCode.Combine);

    public static bool operator ==(ValueObject? a, ValueObject? b) => Equals(a, b);

    public static bool operator !=(ValueObject? a, ValueObject? b) => !Equals(a, b);
}

/// <summary>Por qué el dominio rechaza una operación. La API la traduce a un error 409 o 400.</summary>
public enum TipoRechazo
{
    /// <summary>Los datos no son válidos (una franja al revés, 0 asistentes…): 400.</summary>
    Validacion,

    /// <summary>El estado actual no lo permite (solape, plazo, recurso de baja…): 409.</summary>
    Conflicto,
}

/// <summary>Una regla de negocio incumplida. El mensaje es para la persona o el agente que hizo la petición.</summary>
public sealed class ReglaIncumplidaException(TipoRechazo tipo, string message) : Exception(message)
{
    public TipoRechazo Tipo { get; } = tipo;
}
