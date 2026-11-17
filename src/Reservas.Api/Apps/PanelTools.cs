using System.ComponentModel;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Server;
using Reservas.Api.Aplicacion;
using Reservas.Contracts;

namespace Reservas.Api.Apps;

/// <summary>
/// Herramientas MCP que abren paneles. Un panel es un componente Razor (OcupacionApp.razor) que McpRazor muestra dentro
/// del chat. En el panel decide la persona; el agente solo lo abre y lee el resultado.
/// </summary>
[McpServerToolType]
public sealed class PanelTools(ServicioReservas servicio)
{
    [McpServerTool(Name = "ver_ocupacion", ReadOnly = true, UseStructuredContent = true)]
    [McpAppUi(ResourceUri = "ui://reservas/ocupacion")]
    [Description("Muestra la ocupación de un día en un panel interactivo: cada recurso con sus reservas, donde la persona puede cancelarlas.")]
    public Task<OcupacionDto> VerOcupacion(
        [Description("El día, AAAA-MM-DD. Por defecto, hoy.")] DateOnly? fecha = null,
        CancellationToken ct = default) =>
        servicio.OcupacionAsync(fecha, null, ct);
}
