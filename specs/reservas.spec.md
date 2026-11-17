---
spec: RS
title: Reservas de salas y recursos
---
# Reservas de salas y recursos

Lo que hace la aplicación con los recursos (salas, puestos, vehículos) y sus reservas. Cada requisito tiene un ID,
un enunciado y criterios de aceptación numerados. El código que lo implementa lleva `[Implements(Requirements.RS_00x)]`
y los tests que lo comprueban `[Verifies(...)]`. El build avisa de los requisitos aprobados sin código o sin tests.

Las horas se expresan en la zona horaria de la aplicación (Europe/Madrid por defecto).

## RS-001: Listar recursos
Status: approved

Cuando una persona o un agente pide los recursos, el sistema devolverá los recursos activos ordenados por nombre,
filtrados por tipo y por capacidad mínima cuando se indiquen.

- AC-1: Dadas una sala de 8 plazas, otra de 14 y un vehículo, cuando se filtra por tipo Sala y capacidad mínima 10, entonces solo se devuelve la de 14.
- AC-2: Dado un recurso dado de baja, cuando se listan los recursos, entonces no aparece.

## RS-002: Consultar la ocupación de un día
Status: approved

Cuando se consulta la ocupación de un día, el sistema devolverá, para cada recurso activo, sus reservas confirmadas
de ese día ordenadas por hora de inicio.

- AC-1: Dadas una reserva confirmada y otra cancelada el mismo día, cuando se consulta ese día, entonces solo aparece la confirmada.
- AC-2: Dadas reservas en dos días distintos, cuando se consulta uno de ellos, entonces solo aparecen las de ese día.

## RS-003: Reservar un recurso
Status: approved

Cuando alguien reserva un recurso activo para una franja libre, para un número de asistentes que cabe en él y con
inicio en el futuro, el sistema registrará la reserva como confirmada y publicará el evento ReservaCreada.

- AC-1: Dada una franja libre, cuando se reserva, entonces la reserva queda confirmada y se publica ReservaCreada.
- AC-2: Dada una reserva confirmada de 10:00 a 11:00, cuando se reserva el mismo recurso de 10:30 a 11:30, entonces se rechaza por solape.
- AC-3: Dada una reserva confirmada de 10:00 a 11:00, cuando se reserva el mismo recurso de 11:00 a 12:00, entonces se acepta: las franjas contiguas no se solapan.
- AC-4: Dada una sala de 8 plazas, cuando se reserva para 9 asistentes, entonces se rechaza por capacidad.
- AC-5: Dado un recurso dado de baja, cuando se reserva, entonces se rechaza.
- AC-6: Dada una franja que ya ha empezado, cuando se reserva, entonces se rechaza.
- AC-7: Dada una franja de menos de 15 minutos o de más de 12 horas, o con el fin antes del inicio, cuando se reserva, entonces se rechaza.

## RS-004: Cancelar una reserva
Status: approved

Mientras una reserva está confirmada, cuando su titular la cancela con un motivo al menos 2 horas antes del inicio,
el sistema la marcará como cancelada, guardará el motivo, liberará la franja y publicará el evento ReservaCancelada.
Un agente solo puede cancelar después de que la persona lo confirme.

- AC-1: Dada una reserva que empieza dentro de 3 horas, cuando se cancela con un motivo, entonces queda cancelada con el motivo y se publica ReservaCancelada.
- AC-2: Dada una reserva que empieza dentro de 1 hora, cuando se cancela, entonces se rechaza.
- AC-3: Dada una reserva ya cancelada, cuando se cancela otra vez, entonces se rechaza.
- AC-4: Dada una reserva cancelada, cuando se reserva otra vez la misma franja, entonces se acepta.
- AC-5: Dado un agente que no puede pedir confirmación a la persona, cuando cancela una reserva, entonces la reserva no cambia y recibe un enlace de aprobación.

## RS-005: Dar de baja un recurso
Status: approved

Cuando un administrador da de baja un recurso sin reservas confirmadas futuras, el sistema lo marcará como inactivo:
deja de listarse y de poder reservarse. Un agente solo puede darlo de baja después de que la persona lo confirme.

- AC-1: Dado un recurso sin reservas futuras, cuando se da de baja, entonces queda inactivo.
- AC-2: Dado un recurso con una reserva confirmada futura, cuando se da de baja, entonces se rechaza indicando cuántas reservas hay que cancelar antes.

## RS-006: Reservas periódicas
Status: proposed

> Preguntas abiertas:
> - ¿Qué periodicidades: semanal, quincenal, mensual?
> - Si una de las fechas está ocupada, ¿se rechaza toda la serie o se reservan las libres?
> - ¿Se puede cancelar una sola fecha de la serie?

Cuando alguien reserva un recurso de forma periódica, el sistema creará una reserva por cada fecha de la serie.

- AC-1: Dada una franja semanal libre durante 4 semanas, cuando se reserva de forma periódica, entonces se crean 4 reservas confirmadas.
