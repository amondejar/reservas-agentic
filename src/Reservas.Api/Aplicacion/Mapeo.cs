using Reservas.Contracts;
using Reservas.Domain.Recursos;
using Reservas.Domain.Reservas;
using DomainTipo = Reservas.Domain.Recursos.TipoRecurso;
using DomainEstado = Reservas.Domain.Reservas.EstadoReserva;

namespace Reservas.Api.Aplicacion;

/// <summary>Del modelo de dominio a lo que viaja por HTTP. A mano: sin AutoMapper.</summary>
internal static class Mapeo
{
    public static RecursoDto ADto(this Recurso recurso) =>
        new(recurso.Id.Valor, recurso.Nombre, ADto(recurso.Tipo), recurso.Capacidad.Plazas, recurso.Ubicacion);

    public static ReservaDto ADto(this Reserva reserva, string recurso, ZonaHoraria zona) =>
        new(reserva.Id.Valor, reserva.RecursoId.Valor, recurso, zona.Local(reserva.Franja.Inicio), zona.Local(reserva.Franja.Fin),
            reserva.Titular, reserva.Asunto, reserva.Asistentes,
            reserva.Estado == DomainEstado.Confirmada ? Contracts.EstadoReserva.Confirmada : Contracts.EstadoReserva.Cancelada,
            reserva.MotivoCancelacion);

    public static Contracts.TipoRecurso ADto(DomainTipo tipo) => tipo switch
    {
        DomainTipo.Sala => Contracts.TipoRecurso.Sala,
        DomainTipo.Puesto => Contracts.TipoRecurso.Puesto,
        _ => Contracts.TipoRecurso.Vehiculo,
    };

    public static DomainTipo ADominio(Contracts.TipoRecurso tipo) => tipo switch
    {
        Contracts.TipoRecurso.Sala => DomainTipo.Sala,
        Contracts.TipoRecurso.Puesto => DomainTipo.Puesto,
        _ => DomainTipo.Vehiculo,
    };
}
