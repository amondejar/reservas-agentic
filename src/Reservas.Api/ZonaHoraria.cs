namespace Reservas.Api;

/// <summary>
/// La zona horaria de la aplicación (configuración Reservas:ZonaHoraria, Europe/Madrid por defecto): en ella se
/// interpretan los días y se muestran las horas.
/// </summary>
public sealed class ZonaHoraria(TimeZoneInfo zona)
{
    public TimeZoneInfo Zona { get; } = zona;

    public string Nombre => Zona.Id;

    public static ZonaHoraria Desde(IConfiguration configuration, ILogger logger)
    {
        var id = configuration["Reservas:ZonaHoraria"] ?? "Europe/Madrid";
        try
        {
            return new ZonaHoraria(TimeZoneInfo.FindSystemTimeZoneById(id));
        }
        catch (TimeZoneNotFoundException)
        {
            logger.LogWarning("La zona horaria {Zona} no existe en este sistema: se usa UTC.", id);
            return new ZonaHoraria(TimeZoneInfo.Utc);
        }
    }

    /// <summary>El instante en la hora local: 2026-11-18T16:00:00+01:00.</summary>
    public DateTimeOffset Local(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, Zona);

    public DateOnly Hoy(TimeProvider reloj) => DateOnly.FromDateTime(Local(reloj.GetUtcNow()).DateTime);

    /// <summary>[00:00 del día, 00:00 del siguiente), en la hora local.</summary>
    public (DateTimeOffset Desde, DateTimeOffset Hasta) Dia(DateOnly fecha) => (Medianoche(fecha), Medianoche(fecha.AddDays(1)));

    private DateTimeOffset Medianoche(DateOnly fecha)
    {
        var local = fecha.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zona.GetUtcOffset(local));
    }

    /// <summary>Una hora del día en la hora local, como instante.</summary>
    public DateTimeOffset A(DateOnly fecha, int hora, int minuto = 0)
    {
        var local = fecha.ToDateTime(new TimeOnly(hora, minuto), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zona.GetUtcOffset(local));
    }
}
