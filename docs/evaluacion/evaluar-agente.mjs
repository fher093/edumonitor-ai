// Evaluación del agente con el modelo real (TT-11).
// Uso: node docs/evaluacion/evaluar-agente.mjs <correo> <clave> [cursoId] [salida.json]
// Requiere el backend (5000) y el AIService (5100) en ejecución, y Ollama con el modelo configurado.
import { writeFileSync } from 'node:fs'

const [correo, clave, cursoArg = '1', salida = 'resultado-evaluacion.json'] = process.argv.slice(2)
const BACKEND = process.env.BACKEND_URL ?? 'http://localhost:5000'
const IA = process.env.IA_URL ?? 'http://localhost:5100'
const cursoId = Number(cursoArg)

const ESCENARIOS = [
  { id: 'E1', tipo: 'escenario', pregunta: 'Dame un resumen del curso', esperado: 'ok' },
  { id: 'E2', tipo: 'escenario', pregunta: '¿Qué estudiantes necesitan apoyo esta semana?', esperado: 'ok' },
  { id: 'E3', tipo: 'escenario', pregunta: '¿Qué actividades debo reforzar con el grupo?', esperado: 'ok' },
  { id: 'F1', tipo: 'fallo', pregunta: 'Muéstrame las alertas del curso con id 9999', esperado: 'sin_acceso' },
]

const login = await fetch(`${BACKEND}/api/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ correo, password: clave }),
})
if (!login.ok) throw new Error(`Login falló: ${login.status}`)
const { token } = await login.json()
const estado = await (await fetch(`${IA}/api/ia/estado`, { headers: { Authorization: `Bearer ${token}` } })).json()

const resultados = []
for (const e of ESCENARIOS) {
  const t0 = Date.now()
  const resp = await fetch(`${IA}/api/ia/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ mensaje: e.pregunta, cursoId }),
  })
  const cuerpo = await resp.json().catch(() => null)
  const r = { ...e, http: resp.status, segundos: (Date.now() - t0) / 1000, respuesta: cuerpo }
  if (cuerpo?.traza) {
    r.herramientas = cuerpo.traza.map(t => t.herramienta)
    r.cumple = cuerpo.estado === e.esperado
  }
  resultados.push(r)
  console.log(`${e.id} ${resp.status} ${cuerpo?.estado ?? ''} ${r.segundos}s herramientas=${(r.herramientas ?? []).join(',')} hechos=${cuerpo?.hechos?.length ?? 0} recs=${cuerpo?.recomendaciones?.length ?? 0} advertencias=${cuerpo?.advertencias?.length ?? 0}`)
}

writeFileSync(salida, JSON.stringify({ fecha: new Date().toISOString(), modelo: estado.modelo, cursoId, resultados }, null, 2))
console.log(`Resultados en ${salida}`)
