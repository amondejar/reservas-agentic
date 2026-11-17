using Reservas.Domain.Comun;
using Reservas.Domain.Recursos;
using Reservas.Domain.Reservas;
using Reservas.Tests.Specs;
using SpecTrace;

namespace Reservas.Tests;

/// <summary>Las reglas del dominio, sin API ni base de datos.</summary>
public class DominioTests
{
    private static readonly DateTimeOffset Ahora = Reloj.Lunes8;
    private static readonly Recurso Sala8 = Recurso.Crear("Sala Atlántico", TipoRecurso.Sala, Capacidad.De(8));

    private static DateTimeOffset A(int hora, int minuto = 0) => new(2026, 11, 16, hora, minuto, 0, TimeSpan.FromHours(1));

    private static Reserva Reservar(Recurso recurso, int desde, int hasta, IEnumerable<Reserva>? ocupadas = null, int asistentes = 4) =>
        Reserva.Crear(recurso, Franja.Entre(A(desde), A(hasta)), "Ana García", "Reunión", asistentes, ocupadas ?? [], Ahora);

    private static ReglaIncumplidaException Rechaza(Action accion) => Assert.Throws<ReglaIncumplidaException>(accion);

    [Fact]
    [Verifies(Requirements.RS_003, AcceptanceCriteria.RS_003.AC_1)]
    public void Una_franja_libre_queda_reservada_y_publica_ReservaCreada()
    {
        var reserva = Reservar(Sala8, 10, 11);

        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        var evento = Assert.IsType<ReservaCreada>(Assert.Single(reserva.Eventos));
        Assert.Equal(reserva.Id, evento.Reserva);
        Assert.Equal(Sala8.Id, evento.Recurso);
    }

    [Fact]
    [Verifies(Requirements.RS_003, AcceptanceCriteria.RS_003.AC_2)]
    public void Una_franja_solapada_se_rechaza()
    {
        var existente = Reservar(Sala8, 10, 11);

        var rechazo = Rechaza(() => Reserva.Crear(Sala8, Franja.Entre(A(10, 30), A(11, 30)), "Luis", "Otra", 2, [existente], Ahora));

        Assert.Equal(TipoRechazo.Conflicto, rechazo.Tipo);
        Assert.Contains("ya está reservado", rechazo.Message);
    }

    [Fact]
    [Verifies(Requirements.RS_003, AcceptanceCriteria.RS_003.AC_3)]
    public void Las_franjas_contiguas_no_se_solapan()
    {
        var existente = Reservar(Sala8, 10, 11);

        var siguiente = Reservar(Sala8, 11, 12, [existente]);

        Assert.Equal(EstadoReserva.Confirmada, siguiente.Estado);
    }

    [Fact]
    [Verifies(Requirements.RS_003, AcceptanceCriteria.RS_003.AC_4)]
    public void Si_no_caben_los_asistentes_se_rechaza()
    {
        var rechazo = Rechaza(() => Reservar(Sala8, 10, 11, asistentes: 9));

        Assert.Contains("admite 8 plazas", rechazo.Message);
    }

    [Fact]
    [Verifies(Requirements.RS_003, AcceptanceCriteria.RS_003.AC_5)]
    public void Un_recurso_de_baja_no_se_puede_reservar()
    {
        var recurso = Recurso.Crear("Sala vieja", TipoRecurso.Sala, Capacidad.De(4));
        recurso.DarDeBaja(0, Ahora);

        var rechazo = Rechaza(() => Reservar(recurso, 10, 11, asistentes: 2));

        Assert.Contains("dado de baja", rechazo.Message);
    }

    [Fact]
    [Verifies(Requirements.RS_003, AcceptanceCriteria.RS_003.AC_6)]
    public void Una_franja_que_ya_ha_empezado_se_rechaza()
    {
        var rechazo = Rechaza(() => Reservar(Sala8, 7, 9));

        Assert.Contains("ya ha empezado", rechazo.Message);
    }

    [Theory]
    [Verifies(Requirements.RS_003, AcceptanceCriteria.RS_003.AC_7)]
    [InlineData(10, 0, 10, 10)]    // 10 minutos
    [InlineData(9, 0, 21, 30)]     // 12 horas y media
    [InlineData(11, 0, 10, 0)]     // el fin antes del inicio
    public void Una_franja_de_duracion_imposible_no_es_valida(int h1, int m1, int h2, int m2)
    {
        var rechazo = Rechaza(() => Franja.Entre(A(h1, m1), A(h2, m2)));

        Assert.Equal(TipoRechazo.Validacion, rechazo.Tipo);
    }

    [Fact]
    [Verifies(Requirements.RS_004, AcceptanceCriteria.RS_004.AC_1)]
    public void Con_mas_de_2_horas_de_antelacion_se_cancela_con_el_motivo()
    {
        var reserva = Reservar(Sala8, 11, 12);
        reserva.LimpiarEventos();

        reserva.Cancelar("Se aplaza la reunión", Ahora);

        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
        Assert.Equal("Se aplaza la reunión", reserva.MotivoCancelacion);
        Assert.IsType<ReservaCancelada>(Assert.Single(reserva.Eventos));
    }

    [Fact]
    [Verifies(Requirements.RS_004, AcceptanceCriteria.RS_004.AC_2)]
    public void Con_menos_de_2_horas_no_se_cancela()
    {
        var reserva = Reservar(Sala8, 9, 10);

        var rechazo = Rechaza(() => reserva.Cancelar("Al final no", Ahora));

        Assert.Contains("2 horas", rechazo.Message);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    [Verifies(Requirements.RS_004, AcceptanceCriteria.RS_004.AC_3)]
    public void Una_reserva_cancelada_no_se_cancela_otra_vez()
    {
        var reserva = Reservar(Sala8, 11, 12);
        reserva.Cancelar("Primera vez", Ahora);

        var rechazo = Rechaza(() => reserva.Cancelar("Segunda vez", Ahora));

        Assert.Contains("ya está cancelada", rechazo.Message);
        Assert.Equal("Primera vez", reserva.MotivoCancelacion);
    }

    [Fact]
    [Verifies(Requirements.RS_004, AcceptanceCriteria.RS_004.AC_4)]
    public void Una_franja_cancelada_se_puede_volver_a_reservar()
    {
        var cancelada = Reservar(Sala8, 11, 12);
        cancelada.Cancelar("Se aplaza", Ahora);

        var nueva = Reservar(Sala8, 11, 12, [cancelada]);

        Assert.Equal(EstadoReserva.Confirmada, nueva.Estado);
    }

    [Fact]
    [Verifies(Requirements.RS_005, AcceptanceCriteria.RS_005.AC_1)]
    public void Un_recurso_sin_reservas_futuras_se_da_de_baja()
    {
        var recurso = Recurso.Crear("Puesto 3", TipoRecurso.Puesto, Capacidad.De(1));

        recurso.DarDeBaja(reservasFuturas: 0, Ahora);

        Assert.False(recurso.Activo);
        Assert.IsType<RecursoDadoDeBaja>(Assert.Single(recurso.Eventos));
    }

    [Fact]
    [Verifies(Requirements.RS_005, AcceptanceCriteria.RS_005.AC_2)]
    public void Un_recurso_con_reservas_futuras_no_se_da_de_baja()
    {
        var recurso = Recurso.Crear("Sala Norte", TipoRecurso.Sala, Capacidad.De(6));

        var rechazo = Rechaza(() => recurso.DarDeBaja(reservasFuturas: 1, Ahora));

        Assert.Contains("1 reserva futura", rechazo.Message);
        Assert.True(recurso.Activo);
    }

    [Fact]
    public void Dos_franjas_iguales_en_distinta_zona_horaria_son_la_misma()
    {
        var madrid = Franja.Entre(A(10), A(11));
        var utc = Franja.Entre(A(10).ToUniversalTime(), A(11).ToUniversalTime());

        Assert.Equal(madrid, utc);
    }
}
