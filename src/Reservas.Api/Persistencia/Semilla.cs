using Reservas.Domain;
using Reservas.Domain.Recursos;
using Reservas.Domain.Reservas;

namespace Reservas.Api.Persistencia;

/// <summary>
/// Unos recursos y reservas de ejemplo, si la base de datos está vacía, para probar el front, la API y las
/// herramientas del agente nada más arrancar. Se desactiva con Reservas:Semilla=false.
/// </summary>
public static class Semilla
{
    public static async Task CargarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var recursos = provider.GetRequiredService<IRecursoRepository>();
        if (await recursos.HayAlgunoAsync(ct)) return;

        var reservas = provider.GetRequiredService<IReservaRepository>();
        var unidad = provider.GetRequiredService<IUnidadDeTrabajo>();
        var zona = provider.GetRequiredService<ZonaHoraria>();
        var reloj = provider.GetRequiredService<TimeProvider>();
        var ahora = reloj.GetUtcNow();

        var atlantico = Recurso.Crear("Sala Atlántico", TipoRecurso.Sala, Capacidad.De(8), "Planta 1");
        var mediterraneo = Recurso.Crear("Sala Mediterráneo", TipoRecurso.Sala, Capacidad.De(14), "Planta 2");
        var cantabrico = Recurso.Crear("Sala Cantábrico", TipoRecurso.Sala, Capacidad.De(4), "Planta 1");
        var puesto = Recurso.Crear("Puesto flexible 12", TipoRecurso.Puesto, Capacidad.De(1), "Planta 2, zona abierta");
        var furgoneta = Recurso.Crear("Furgoneta 1234-KLM", TipoRecurso.Vehiculo, Capacidad.De(3), "Parking -1");
        foreach (var recurso in new[] { atlantico, mediterraneo, cantabrico, puesto, furgoneta }) recursos.Agregar(recurso);

        // Mañana y pasado: siempre en el futuro, sea cual sea la hora de arranque.
        var manana = zona.Hoy(reloj).AddDays(1);
        var ejemplos = new (Recurso Recurso, DateOnly Dia, int Desde, int Hasta, string Titular, string Asunto, int Asistentes)[]
        {
            (atlantico, manana, 9, 10, "Ana García", "Daily del equipo", 6),
            (atlantico, manana, 12, 13, "Luis Martín", "Entrevista", 3),
            (mediterraneo, manana, 10, 12, "Marta Ruiz", "Revisión trimestral", 12),
            (cantabrico, manana, 16, 17, "Ana García", "Uno a uno", 2),
            (furgoneta, manana, 8, 14, "Pedro Sanz", "Visita a cliente en Valencia", 2),
            (mediterraneo, manana.AddDays(1), 9, 11, "Luis Martín", "Formación de producto", 10),
        };

        foreach (var (recurso, dia, desde, hasta, titular, asunto, asistentes) in ejemplos)
        {
            var franja = Franja.Entre(zona.A(dia, desde), zona.A(dia, hasta));
            reservas.Agregar(Reserva.Crear(recurso, franja, titular, asunto, asistentes, [], ahora));
        }

        await unidad.GuardarAsync(ct);
    }
}
