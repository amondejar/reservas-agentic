// Hace de agente de IA contra la aplicación en marcha: lista herramientas, consulta, reserva, abre el panel y
// comprueba que las herramientas destructivas nunca se ejecutan sin la persona. Uso:
//   node tests/e2e/agente.mjs http://localhost:5090
const base = process.argv[2] ?? 'http://localhost:5090';
let id = 0, pasadas = 0;
const ok = (cond, que, detalle) => {
  if (!cond) { console.error(`  ✗ ${que}`, detalle ?? ''); process.exit(1); }
  console.log(`  ✓ ${que}`); pasadas++;
};

async function mcp(method, params = {}) {
  const res = await fetch(`${base}/mcp`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', accept: 'application/json, text/event-stream', 'mcp-protocol-version': '2025-11-25' },
    body: JSON.stringify({ jsonrpc: '2.0', id: ++id, method, params }),
  });
  const text = await res.text();
  const json = text.trimStart().startsWith('{') ? text : text.split('\n').filter(l => l.startsWith('data:')).map(l => l.slice(5)).join('');
  return { status: res.status, body: json ? JSON.parse(json) : null, raw: text };
}
const api = async (path, init) => {
  const r = await fetch(`${base}/api${path}`, init);
  return { status: r.status, body: r.headers.get('content-type')?.includes('json') ? await r.json() : await r.text() };
};
const post = (path, body) => api(path, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
const texto = r => r.body?.result?.content?.map(c => c.text ?? '').join('\n') ?? '';

// Un día de la semana que viene, a las 10:00 en Madrid (+01:00 en invierno, +02:00 en verano: se pide a la API).
const dia = new Date(Date.now() + 7 * 86400000).toISOString().slice(0, 10);

console.log('Servidor MCP');
const init = await mcp('initialize', { protocolVersion: '2025-11-25', capabilities: { extensions: { 'io.modelcontextprotocol/ui': { mimeTypes: ['text/html;profile=mcp-app'] } } }, clientInfo: { name: 'e2e', version: '1' } });
ok(init.status === 200 && init.body?.result?.serverInfo, 'initialize', init.raw);

const lista = await mcp('tools/list');
const tools = Object.fromEntries((lista.body?.result?.tools ?? []).map(t => [t.name, t]));
for (const nombre of ['recursos_listar', 'recursos_dar_de_baja', 'ocupacion_consultar', 'reservas_crear', 'reservas_listar', 'reservas_obtener', 'reservas_cancelar', 'ver_ocupacion'])
  ok(tools[nombre], `la herramienta ${nombre} está publicada`, Object.keys(tools));
ok(tools.reservas_cancelar.annotations?.destructiveHint === true, 'reservas_cancelar está marcada como destructiva');
ok(tools.recursos_dar_de_baja.annotations?.destructiveHint === true, 'recursos_dar_de_baja está marcada como destructiva');
ok(tools.reservas_crear.annotations?.destructiveHint === false, 'reservas_crear no es destructiva: solo añade');
ok(tools.recursos_listar.annotations?.readOnlyHint === true, 'recursos_listar es de solo lectura');
ok(tools.ver_ocupacion._meta?.ui?.resourceUri === 'ui://reservas/ocupacion', 'ver_ocupacion abre el panel');

console.log('El agente consulta');
const salas = await mcp('tools/call', { name: 'recursos_listar', arguments: { tipo: 'Sala', capacidadMinima: 10, fields: ['id', 'nombre'] } });
ok(!salas.body?.result?.isError && texto(salas).includes('Sala Mediterráneo') && !texto(salas).includes('Sala Atlántico'), 'recursos_listar filtra por tipo y capacidad, con solo los campos pedidos', texto(salas));
const mediterraneo = JSON.parse(texto(salas))[0].id;

console.log('El agente reserva');
const zona = (await api(`/ocupacion?fecha=${dia}`)).body.zonaHoraria;
const offset = new Intl.DateTimeFormat('en', { timeZone: zona, timeZoneName: 'longOffset' })
  .formatToParts(new Date(`${dia}T10:00:00Z`)).find(p => p.type === 'timeZoneName').value.replace('GMT', '') || '+00:00';
const reserva = { recursoId: mediterraneo, inicio: `${dia}T10:00:00${offset}`, fin: `${dia}T11:00:00${offset}`, titular: 'Agente de pruebas', asunto: 'Prueba e2e', asistentes: 10 };
const creada = await mcp('tools/call', { name: 'reservas_crear', arguments: reserva });
ok(!creada.body?.result?.isError && texto(creada).includes('"estado":"Confirmada"'), 'reservas_crear reserva una franja libre sin pedir confirmación', texto(creada));
const reservaId = JSON.parse(texto(creada)).id;

const solapada = await mcp('tools/call', { name: 'reservas_crear', arguments: { ...reserva, inicio: `${dia}T10:30:00${offset}`, fin: `${dia}T11:30:00${offset}` } });
ok(solapada.body?.result?.isError && texto(solapada).includes('ya está reservado'), 'una franja solapada se rechaza con un motivo que el agente entiende', texto(solapada));

const grande = await mcp('tools/call', { name: 'reservas_crear', arguments: { ...reserva, inicio: `${dia}T12:00:00${offset}`, fin: `${dia}T13:00:00${offset}`, asistentes: 30 } });
ok(grande.body?.result?.isError && texto(grande).includes('admite 14 plazas'), 'si no caben los asistentes, se rechaza', texto(grande));

console.log('Cancelar necesita a la persona');
const cancelar = await mcp('tools/call', { name: 'reservas_cancelar', arguments: { id: reservaId, body: { motivo: 'Ya no hace falta' } } });
ok(cancelar.body?.result?.isError && /approv/i.test(texto(cancelar)), 'el agente no puede cancelar solo: recibe un enlace de aprobación', texto(cancelar));
ok((await api(`/reservas/${reservaId}`)).body.estado === 'Confirmada', 'la reserva sigue confirmada');

console.log('Panel de ocupación');
const panel = await mcp('tools/call', { name: 'ver_ocupacion', arguments: { fecha: dia } });
const vista = panel.body?.result?._meta?.['mcpRazor/view'];
ok(vista?.html?.includes('Ocupación del') && vista.html.includes('Prueba e2e'), 'ver_ocupacion renderiza el panel en el servidor', JSON.stringify(panel.body).slice(0, 300));

console.log('API para el front');
const directa = await post(`/reservas/${reservaId}/cancelar`, { motivo: 'Cancelada desde el front' });
ok(directa.status === 200 && directa.body.estado === 'Cancelada', 'el front (una persona) cancela directamente', directa.body);
const otraVez = await post('/reservas', reserva);
ok(otraVez.status === 201, 'una franja cancelada se puede volver a reservar', otraVez.body);
const mal = await post('/reservas', { ...reserva, fin: reserva.inicio });
ok(mal.status === 400, 'una franja que acaba al empezar es un error 400', mal.body);
const ocupado = await post('/reservas', reserva);
ok(ocupado.status === 409 && ocupado.body.detail?.includes('ya está reservado'), 'el solape es un 409 con el motivo', ocupado.body);

console.log(`${pasadas} comprobaciones superadas`);
