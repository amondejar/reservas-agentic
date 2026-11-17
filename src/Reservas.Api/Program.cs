using Keel.Endpoints;
using Keel.Endpoints.Agents;
using Keel.Endpoints.Mcp;
using McpRazor;
using Reservas.Api;
using Reservas.Api.Aplicacion;
using Reservas.Api.Apps;
using Reservas.Api.Persistencia;
#if POSTGRES
using Reservas.Infrastructure;
#endif

var builder = WebApplication.CreateBuilder(args);

// Render (y otros) dicen el puerto en la variable PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddKeelEndpoints();   // cerrado por defecto: cada endpoint declara quién puede llamarlo (ver ApiGroup)
builder.Services.AddValidation();      // validación de .NET 10 para los contratos de las peticiones
builder.Services.AddProblemDetails();  // errores RFC 9457
builder.Services.AddHealthChecks();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => ZonaHoraria.Desde(builder.Configuration, sp.GetRequiredService<ILogger<ZonaHoraria>>()));
builder.Services.AddScoped<ServicioReservas>();

// Con cadena de conexión (ConnectionStrings:Reservas, o la variable ConnectionStrings__Reservas), Postgres; sin ella,
// los datos en memoria, que se pierden al reiniciar.
var conexion = builder.Configuration.GetConnectionString("Reservas");
#if POSTGRES
if (!string.IsNullOrWhiteSpace(conexion))
    builder.Services.AddPersistenciaPostgres(conexion);   // también guarda las claves de Data Protection
else
#endif
    builder.Services.AddPersistenciaEnMemoria();

// Agentes. Los endpoints con ExposeAsTool son herramientas MCP en /mcp, y ver_ocupacion abre un panel (un
// componente Razor) dentro de Claude, Copilot y otros clientes MCP Apps.
builder.Services.AddKeelMcp()
    .WithTools<PanelTools>()
    .WithRazorApps(apps => apps.AddAppsFromAssembly(typeof(PanelTools).Assembly));

// Las herramientas destructivas (cancelar, dar de baja) se ejecutan solo después de que la persona confirme. A los
// clientes que no pueden pedir confirmación se les da un enlace de aprobación (/approvals/{id}).
builder.Services.AddKeelAgents(options => options.WhenClientCannotConfirm = ConfirmationFallback.Approval);

var app = builder.Build();

#if POSTGRES
if (!string.IsNullOrWhiteSpace(conexion))
    await app.Services.PrepararPostgresAsync();
#endif
if (app.Configuration.GetValue("Reservas:Semilla", true))
    await Semilla.CargarAsync(app.Services);

if (app.Environment.IsDevelopment())
    app.MapMcpRazorPreview("/preview", mcpPath: "/mcp");   // los paneles en un navegador, sin cliente de IA

#if WEB
// El front: Blazor WebAssembly, servido desde la misma aplicación (una URL, sin CORS).
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
#endif

app.MapKeelEndpoints("/api");
app.MapKeelMcp("/mcp");        // Claude Code: claude mcp add --transport http reservas http://localhost:5090/mcp
app.MapKeelApprovals();        // las páginas de aprobación
app.MapHealthChecks("/health");

#if WEB
app.MapFallbackToFile("index.html");
#endif

app.Run();

/// <summary>Visible para el WebApplicationFactory de los tests.</summary>
public partial class Program;
