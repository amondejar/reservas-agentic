# Instrucciones para agentes de código

Esta aplicación reserva salas, puestos y vehículos. La usan personas (el front) y agentes de IA (MCP en `/mcp`).
Lee esto antes de cambiar nada.

## Compilar y probar

```bash
dotnet build      # también comprueba DDD (Tactica), trazabilidad (SpecTrace) y skills de agente (SkillLock)
dotnet test
dotnet run --project src/Reservas.Api   # http://localhost:5090 · MCP en /mcp · paneles en /preview
node tests/e2e/agente.mjs http://localhost:5090   # con la aplicación en marcha: la usa como un agente
```

El build tiene que terminar **sin avisos**. Un aviso de SpecTrace (ST0003, ST0004, ST0005) significa que un requisito
no tiene código o test: arréglalo, no lo suprimas. Los errores de Tactica (TAC0xx) dicen qué escribir en su lugar.
Para muchos a la vez: `dotnet format analyzers --diagnostics TAC001 TAC002 TAC005 TAC010 TAC012 TAC013 TAC030 --severity warn`.

## Estructura

| Proyecto | Qué hay |
|---|---|
| `src/Reservas.Domain` | Agregados (`Recurso`, `Reserva`), value objects (`Franja`, `Capacidad`), eventos y puertos (repositorios). Sin dependencias |
| `src/Reservas.Api` | Casos de uso (`Aplicacion/ServicioReservas`), un endpoint por clase (`Features/<Área>/<Operación>/Endpoint.cs`), panel MCP (`Apps`), datos en memoria |
| `src/Reservas.Infrastructure` | Postgres con EF Core, y las claves de Data Protection |
| `src/Reservas.Contracts` | Lo que viaja por HTTP (DTOs), compartido con el front |
| `src/Reservas.Web` | El front, Blazor WebAssembly, servido por la API |
| `tests/Reservas.Tests` | Tests del dominio y de la API (un solo proyecto, por SpecTrace) |

## Cómo se hace un cambio

1. **Primero el requisito.** Están en `specs/reservas.spec.md` (ID, `Status:`, enunciado, criterios `- AC-n:`). Solo se
   implementan los `Status: approved`. Si algo no está claro, añade una pregunta abierta (`> Preguntas abiertas:`) en
   lugar de suponer.
2. **Las reglas, en el dominio.** Un agregado cambia solo con sus métodos; otro agregado se referencia por id; los
   value objects son inmutables; los eventos se publican desde el agregado. Tactica lo comprueba en cada build.
3. **El caso de uso, en `ServicioReservas`**, con `[Implements(Requirements.RS_00x)]`. Carga, pide la operación al
   agregado y guarda con `IUnidadDeTrabajo`.
4. **El endpoint**, copiando uno parecido (`Features/Reservas/Obtener`). Para ofrecerlo a los agentes,
   `ExposeAsTool("...")` con una descripción escrita para el modelo.
   - Lo que cambia datos dice `tool.Destructive` (true si cancela, borra o sobrescribe).
   - Lo irreversible pide confirmación: `RequireConfirmation<TRequest>(r => $"...")`. El mensaje solo puede usar los
     datos de la petición.
   - Listas: `tool.MaxItems` y `tool.FieldSelection = true`.
5. **Un test por criterio de aceptación**, con `[Verifies(Requirements.RS_00x, AcceptanceCriteria.RS_00x.AC_n)]`.
6. **El front**, si aplica, en `src/Reservas.Web`, con etiquetas accesibles (WCAG 2.1 AA).

## Nunca

- Quitar o debilitar `RequireConfirmation`, `Destructive = true` o las reglas del dominio para que algo sea más fácil.
- Cambiar el `Status:` de un requisito, ni editar `spectrace.lock`, `skills.lock.json` o `tactica.baseline`: los revisan personas.
- Suprimir avisos de Tactica o SpecTrace (`#pragma`, `[SuppressMessage]`, `.editorconfig`).
- Poner cadenas de conexión o secretos en el repositorio: van en user-secrets o en variables de entorno.
- Añadir paquetes NuGet sin acordarlo antes.
