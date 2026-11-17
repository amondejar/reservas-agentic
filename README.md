# Reservas

Una aplicación de ejemplo para reservar salas, puestos y vehículos, hecha con los paquetes
[agénticos para .NET](https://github.com/amondejar). La usan **personas**, desde el navegador, y **agentes de IA**
(Claude, Copilot…), desde el chat, por MCP. Las dos ven los mismos datos y cumplen las mismas reglas.

| | Qué aporta aquí | Dónde |
|---|---|---|
| **[Keel.Endpoints](https://github.com/amondejar/keel-endpoints)** | Un endpoint por clase. Cada uno es también una herramienta MCP; cancelar y dar de baja solo se ejecutan si la persona confirma | `src/Reservas.Api/Features` |
| **[McpRazor](https://github.com/amondejar/mcp-razor)** | El panel *Ocupación del día* dentro del chat, escrito como componente Razor | `src/Reservas.Api/Apps` |
| **[Tactica](https://github.com/amondejar/tactica)** | Comprueba en cada build el diseño DDD del dominio: agregados, value objects, eventos | `src/Reservas.Domain` |
| **[SpecTrace](https://github.com/amondejar/spec-trace)** | Los requisitos en Markdown, enlazados con el código y los tests. El build avisa de lo que falta | `specs/` |
| **[SkillLock](https://github.com/amondejar/skill-lock)** | Revisa las skills de agente que traen los paquetes | `Directory.Build.props` |

## Arrancar en local

```bash
dotnet run --project src/Reservas.Api
```

- Front: http://localhost:5090
- API: http://localhost:5090/api/recursos
- Servidor MCP para agentes: http://localhost:5090/mcp
- Paneles en el navegador, sin cliente de IA: http://localhost:5090/preview

Sin configurar nada, los datos están **en memoria**, con unos recursos y reservas de ejemplo, y se pierden al parar.
Para usar Postgres (por ejemplo, Neon), guarda la cadena de conexión en user-secrets, nunca en el repositorio:

```bash
dotnet user-secrets --project src/Reservas.Api set ConnectionStrings:Reservas "postgresql://usuario:clave@host/reservas?sslmode=require"
```

Vale tal cual la copia Neon. Al arrancar se crean las tablas y la restricción que impide dos reservas solapadas.

## Conectar un agente

- **Claude Code**: `.mcp.json` ya está aquí. Arranca `claude` en esta carpeta y aprueba el servidor `reservas`.
- **VS Code con GitHub Copilot**: `.vscode/mcp.json` ya está aquí. Arranca el servidor desde la vista MCP y activa
  `chat.mcp.apps.enabled` para ver los paneles.
- **Claude Desktop o claude.ai**: necesitan una URL pública con HTTPS, la de Render (abajo) o un túnel:
  `devtunnel host -p 5090 --allow-anonymous`.

Y prueba:

- *"¿Qué salas para 10 personas hay libres mañana a las 10?"* → `recursos_listar` y `ocupacion_consultar`.
- *"Resérvame la Mediterráneo mañana de 10 a 11 para la revisión del sprint, somos 9"* → `reservas_crear`.
- *"Enséñame la ocupación de mañana"* → `ver_ocupacion` abre el panel, donde tú cancelas con un clic.
- *"Cancela mi reserva de las 10, se ha aplazado"* → `reservas_cancelar`, y **tú** confirmas. Si tu cliente no puede
  mostrar la confirmación, el agente te da un enlace para aprobarla.

## Publicar en Render (gratis)

1. En [Render](https://dashboard.render.com), **New → Blueprint** y elige este repositorio: `render.yaml` crea el
   servicio desde el `Dockerfile`, en el plan gratuito.
2. Cuando lo pida, pon en `ConnectionStrings__Reservas` la cadena de conexión de Neon. Sin ella, la aplicación arranca
   con datos en memoria, que se pierden cada vez que Render la reinicia.
3. Cada push a `main` despliega de nuevo.

Una sola imagen sirve el front, la API y el servidor MCP en la misma URL. En el plan gratuito el servicio se duerme
tras 15 minutos sin tráfico, y la primera visita tarda un minuto en despertarlo.

## Cómo está hecho

- **Dominio** (`src/Reservas.Domain`): dos agregados, `Recurso` y `Reserva`. Una reserva guarda el id del recurso,
  no el recurso. `Franja` y `Capacidad` son value objects inmutables. Las reglas viven ahí: sin solapes, sin pasar de
  la capacidad, solo franjas futuras, cancelar con 2 horas de antelación, no dar de baja un recurso con reservas.
- **Casos de uso** (`Aplicacion/ServicioReservas`): cargan, piden la operación al agregado y guardan. Los usan los
  endpoints, las herramientas MCP y el panel.
- **Persistencia**: en memoria o Postgres (EF Core). En Postgres, una restricción de exclusión impide dos reservas
  confirmadas solapadas aunque lleguen a la vez, y las claves de Data Protection (que firman las confirmaciones de
  los agentes) se guardan en la base de datos para sobrevivir a los reinicios.
- **Front** (`src/Reservas.Web`): Blazor WebAssembly. Ocupación del día, reservar, próximas reservas y recursos.

Antes de usarlo de verdad: añade autenticación (hoy todos los endpoints son anónimos, ver `Features/Api.cs`) y
cambia `EnsureCreated` por migraciones de EF Core.

## Comandos

```bash
dotnet test                                  # dominio y API
node tests/e2e/agente.mjs http://localhost:5090   # con la aplicación en marcha: la usa como un agente
dotnet tool restore
dotnet spectrace report                      # requisito → código → test
dotnet skilllock scan                        # qué piden a los agentes las skills de los paquetes
```

## Licencia

MIT
