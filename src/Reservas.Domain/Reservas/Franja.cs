using Reservas.Domain.Comun;

namespace Reservas.Domain.Reservas;

/// <summary>El intervalo de tiempo de una reserva: [Inicio, Fin), de 15 minutos a 12 horas.</summary>
public sealed class Franja : ValueObject
{
    public static readonly TimeSpan DuracionMinima = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan DuracionMaxima = TimeSpan.FromHours(12);

    public DateTimeOffset Inicio { get; }
    public DateTimeOffset Fin { get; }

    public TimeSpan Duracion => Fin - Inicio;

    private Franja(DateTimeOffset inicio, DateTimeOffset fin) => (Inicio, Fin) = (inicio, fin);
    public static Franja Entre(DateTimeOffset inicio, DateTimeOffset fin)
    {
        if (fin <= inicio)
            throw new ReglaIncumplidaException(TipoRechazo.Validacion, "La franja debe terminar después de empezar.");
        var duracion = fin - inicio;
        if (duracion < DuracionMinima || duracion > DuracionMaxima)
            throw new ReglaIncumplidaException(TipoRechazo.Validacion, "Una reserva dura entre 15 minutos y 12 horas.");

        // Se guarda en UTC: dos franjas iguales en distinta zona horaria son la misma franja.
        return new Franja(inicio.ToUniversalTime(), fin.ToUniversalTime());
    }

    /// <summary>Si comparten algún instante. Las contiguas (una acaba cuando empieza la otra) no se solapan.</summary>
    public bool SeSolapaCon(Franja otra) => Inicio < otra.Fin && otra.Inicio < Fin;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Inicio;
        yield return Fin;
    }

    public override string ToString() => $"{Inicio:yyyy-MM-dd HH:mm}–{Fin:HH:mm} UTC";
}
