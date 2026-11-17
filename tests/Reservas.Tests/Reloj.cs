namespace Reservas.Tests;

/// <summary>Un reloj que no avanza solo: los tests deciden qué hora es.</summary>
public sealed class Reloj(DateTimeOffset ahora) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = ahora;

    public override DateTimeOffset GetUtcNow() => Ahora.ToUniversalTime();

    /// <summary>El lunes 16/11/2026 a las 8:00 en Madrid (+01:00).</summary>
    public static readonly DateTimeOffset Lunes8 = new(2026, 11, 16, 8, 0, 0, TimeSpan.FromHours(1));
}
