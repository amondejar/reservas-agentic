using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Reservas.Contracts;
using Reservas.Domain;
using Reservas.Domain.Recursos;
using Reservas.Tests.Specs;
using SpecTrace;
using DomainTipo = Reservas.Domain.Recursos.TipoRecurso;

namespace Reservas.Tests;

/// <summary>La API por HTTP y el servidor MCP al que se conectan los agentes. Cada test, con su propia aplicación en memoria.</summary>
public sealed class ApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly WebApplicationFactory<Program> _app;
    private readonly Reloj _reloj = new(Reloj.Lunes8);

    public ApiTests()
    {
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Reservas:Semilla", "false");
            builder.UseSetting("ConnectionStrings:Reservas", "");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_reloj);
            });
        });
    }

    public void Dispose() => _app.Dispose();

    private async Task<Guid> Recurso(string nombre, DomainTipo tipo, int plazas, bool deBaja = false)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var recurso = Domain.Recursos.Recurso.Crear(nombre, tipo, Capacidad.De(plazas));
        if (deBaja) recurso.DarDeBaja(0, _reloj.Ahora);
        scope.ServiceProvider.GetRequiredService<IRecursoRepository>().Agregar(recurso);
        await scope.ServiceProvider.GetRequiredService<IUnidadDeTrabajo>().GuardarAsync();
        return recurso.Id.Valor;
    }

    private async Task<ReservaDto> Reservar(Guid recurso, int dia, int desde, int hasta)
    {
        var respuesta = await _app.CreateClient().PostAsJsonAsync("/api/reservas", new NuevaReservaDto(
            recurso, Hora(dia, desde), Hora(dia, hasta), "Ana García", "Reunión", 2), Json);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ReservaDto>(Json))!;
    }

    private static DateTimeOffset Hora(int dia, int hora) => new(2026, 11, dia, hora, 0, 0, TimeSpan.FromHours(1));

    [Fact]
    [Verifies(Requirements.RS_001, AcceptanceCriteria.RS_001.AC_1)]
    public async Task Lista_los_recursos_por_tipo_y_capacidad()
    {
        await Recurso("Sala 8", DomainTipo.Sala, 8);
        await Recurso("Sala 14", DomainTipo.Sala, 14);
        await Recurso("Furgoneta", DomainTipo.Vehiculo, 20);

        var recursos = await _app.CreateClient().GetFromJsonAsync<List<RecursoDto>>("/api/recursos?tipo=Sala&capacidadMinima=10", Json);

        Assert.Equal(new[] { "Sala 14" }, recursos!.Select(r => r.Nombre));
    }

    [Fact]
    [Verifies(Requirements.RS_001, AcceptanceCriteria.RS_001.AC_2)]
    public async Task Los_recursos_de_baja_no_se_listan()
    {
        await Recurso("Sala activa", DomainTipo.Sala, 8);
        await Recurso("Sala de baja", DomainTipo.Sala, 8, deBaja: true);

        var recursos = await _app.CreateClient().GetFromJsonAsync<List<RecursoDto>>("/api/recursos", Json);

        Assert.Equal(new[] { "Sala activa" }, recursos!.Select(r => r.Nombre));
    }

    [Fact]
    [Verifies(Requirements.RS_002, AcceptanceCriteria.RS_002.AC_1)]
    public async Task La_ocupacion_no_muestra_las_reservas_canceladas()
    {
        var sala = await Recurso("Sala", DomainTipo.Sala, 8);
        await Reservar(sala, 17, 10, 11);
        var cancelada = await Reservar(sala, 17, 12, 13);
        var cliente = _app.CreateClient();
        (await cliente.PostAsJsonAsync($"/api/reservas/{cancelada.Id}/cancelar", new CancelacionDto("Ya no hace falta"), Json)).EnsureSuccessStatusCode();

        var ocupacion = await cliente.GetFromJsonAsync<OcupacionDto>("/api/ocupacion?fecha=2026-11-17", Json);

        var reservas = Assert.Single(ocupacion!.Recursos).Reservas;
        Assert.Equal(new[] { Hora(17, 10) }, reservas.Select(r => r.Inicio));
    }

    [Fact]
    [Verifies(Requirements.RS_002, AcceptanceCriteria.RS_002.AC_2)]
    public async Task La_ocupacion_muestra_solo_el_dia_pedido()
    {
        var sala = await Recurso("Sala", DomainTipo.Sala, 8);
        await Reservar(sala, 17, 10, 11);
        await Reservar(sala, 18, 10, 11);

        var ocupacion = await _app.CreateClient().GetFromJsonAsync<OcupacionDto>("/api/ocupacion?fecha=2026-11-18", Json);

        var reserva = Assert.Single(Assert.Single(ocupacion!.Recursos).Reservas);
        Assert.Equal(Hora(18, 10), reserva.Inicio);
        Assert.Equal("Europe/Madrid", ocupacion.ZonaHoraria);
    }

    [Fact]
    public async Task Un_solape_es_un_409_con_el_motivo()
    {
        var sala = await Recurso("Sala", DomainTipo.Sala, 8);
        await Reservar(sala, 17, 10, 11);

        var respuesta = await _app.CreateClient().PostAsJsonAsync("/api/reservas", new NuevaReservaDto(
            sala, Hora(17, 10), Hora(17, 12), "Luis", "Otra", 2), Json);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("ya está reservado", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    [Verifies(Requirements.RS_004, AcceptanceCriteria.RS_004.AC_5)]
    public async Task Un_agente_no_puede_cancelar_sin_la_persona()
    {
        var sala = await Recurso("Sala", DomainTipo.Sala, 8);
        var reserva = await Reservar(sala, 17, 10, 11);

        // Este cliente no puede preguntar a la persona: el servidor responde con un enlace de aprobación y no hace nada.
        using var resultado = await Mcp("tools/call", new { name = "reservas_cancelar", arguments = new { id = reserva.Id, body = new { motivo = "Ya no hace falta" } } });

        Assert.True(resultado.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        var ahora = await _app.CreateClient().GetFromJsonAsync<ReservaDto>($"/api/reservas/{reserva.Id}", Json);
        Assert.Equal(EstadoReserva.Confirmada, ahora!.Estado);
    }

    [Fact]
    public async Task Los_agentes_ven_las_herramientas_y_el_panel()
    {
        using var resultado = await Mcp("tools/list", new { });
        var tools = resultado.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .ToDictionary(tool => tool.GetProperty("name").GetString()!);

        Assert.Contains("reservas_crear", tools.Keys);
        Assert.True(tools["reservas_cancelar"].GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
        Assert.True(tools["recursos_dar_de_baja"].GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
        Assert.Equal("ui://reservas/ocupacion", tools["ver_ocupacion"].GetProperty("_meta").GetProperty("ui").GetProperty("resourceUri").GetString());
    }

    private async Task<JsonDocument> Mcp(string method, object parameters)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method, @params = parameters }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");

        var response = await _app.CreateClient().SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        // Streamable HTTP puede responder como server-sent event: se toma la línea data.
        var json = body.TrimStart().StartsWith('{') ? body : string.Join("", body.Split('\n').Where(l => l.StartsWith("data:")).Select(l => l[5..]));
        return JsonDocument.Parse(json);
    }
}
