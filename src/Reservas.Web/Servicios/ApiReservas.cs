using System.Net.Http.Json;
using System.Text.Json;
using Reservas.Contracts;

namespace Reservas.Web.Servicios;

/// <summary>Un error de la API, con el mensaje para la persona (el "detail" de ProblemDetails).</summary>
public sealed class ErrorApi(string message) : Exception(message);

/// <summary>Las llamadas a /api. Los errores llegan como ProblemDetails y se convierten en ErrorApi.</summary>
public sealed class ApiReservas(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<OcupacionDto> OcupacionAsync(DateOnly fecha, TipoRecurso? tipo) =>
        (await http.GetFromJsonAsync<OcupacionDto>($"api/ocupacion?fecha={fecha:yyyy-MM-dd}{(tipo is null ? "" : $"&tipo={tipo}")}", Json))!;

    public async Task<IReadOnlyList<RecursoDto>> RecursosAsync() =>
        (await http.GetFromJsonAsync<List<RecursoDto>>("api/recursos", Json))!;

    public async Task<IReadOnlyList<ReservaDto>> ReservasAsync(string? titular) =>
        (await http.GetFromJsonAsync<List<ReservaDto>>($"api/reservas?titular={Uri.EscapeDataString(titular ?? "")}", Json))!;

    public Task<ReservaDto> ReservarAsync(NuevaReservaDto nueva) => Enviar<ReservaDto>("api/reservas", nueva);

    public Task<ReservaDto> CancelarAsync(Guid id, string motivo) => Enviar<ReservaDto>($"api/reservas/{id}/cancelar", new CancelacionDto(motivo));

    public Task<RecursoDto> DarDeBajaAsync(Guid id) => Enviar<RecursoDto>($"api/recursos/{id}/baja", null);

    private async Task<T> Enviar<T>(string ruta, object? cuerpo)
    {
        using var respuesta = cuerpo is null ? await http.PostAsync(ruta, null) : await http.PostAsJsonAsync(ruta, cuerpo, Json);
        if (respuesta.IsSuccessStatusCode) return (await respuesta.Content.ReadFromJsonAsync<T>(Json))!;
        throw new ErrorApi(await Mensaje(respuesta));
    }

    private static async Task<string> Mensaje(HttpResponseMessage respuesta)
    {
        try
        {
            var problema = await respuesta.Content.ReadFromJsonAsync<JsonElement>(Json);
            if (problema.TryGetProperty("detail", out var detalle) && detalle.GetString() is { Length: > 0 } texto) return texto;
            // Errores de validación: { "errors": { "Titular": ["..."] } }
            if (problema.TryGetProperty("errors", out var errores))
                return string.Join(" ", errores.EnumerateObject().SelectMany(e => e.Value.EnumerateArray().Select(m => m.GetString())));
            if (problema.TryGetProperty("title", out var titulo)) return titulo.GetString() ?? "Error";
        }
        catch (JsonException)
        {
        }

        return $"La API respondió {(int)respuesta.StatusCode}.";
    }
}

/// <summary>Lo que la persona ya ha escrito (su nombre), para no pedírselo en cada reserva.</summary>
public sealed class Preferencias
{
    public string? Titular { get; set; }
}

/// <summary>Horas en la zona horaria de la aplicación, sea cual sea la del navegador.</summary>
public static class Horas
{
    public static TimeZoneInfo Zona(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    /// <summary>La fecha y hora local de la zona, como instante con su desfase (+01:00 en invierno en Madrid).</summary>
    public static DateTimeOffset En(TimeZoneInfo zona, DateOnly fecha, TimeOnly hora)
    {
        var local = fecha.ToDateTime(hora, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zona.GetUtcOffset(local));
    }

    public static DateOnly Hoy(TimeZoneInfo zona) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zona).DateTime);
}

/// <summary>Textos que se repiten en las páginas.</summary>
public static class Formato
{
    public static string Tipo(TipoRecurso tipo) => tipo switch
    {
        TipoRecurso.Sala => "Sala",
        TipoRecurso.Puesto => "Puesto",
        _ => "Vehículo",
    };

    public static string Plazas(int n) => n == 1 ? "1 plaza" : $"{n} plazas";

    public static string Personas(int n) => n == 1 ? "1 persona" : $"{n} personas";
}
