import { useEffect, useRef, useState } from 'react'
import { api } from '../api/http.js'
import { useCursoActivo } from '../cursos/CursoContext.jsx'
import { Panel, Vacio } from '../components/ui.jsx'
import { IconAgente, IconAdvertencia } from '../components/Icons.jsx'
import { fechaHora } from '../utils/formato.js'
import { SinCursos } from './Resumen.jsx'

const SUGERENCIAS = [
  'Resumen del curso',
  '¿Qué estudiantes necesitan apoyo esta semana?',
  '¿Qué actividades debo reforzar?',
  '¿Quién tiene entregas vencidas?',
]

const NOMBRE_HERRAMIENTA = {
  listar_cursos_autorizados: 'Cursos autorizados',
  obtener_calificaciones: 'Calificaciones',
  obtener_progreso: 'Progreso',
  identificar_alertas: 'Alertas',
  generar_resumen: 'Resumen del curso',
}

/** Texto plano de una respuesta del agente, para enviarlo como historial de la conversación. */
function textoRespuesta(r) {
  if (!r) return ''
  if (r.mensaje) return r.mensaje
  return [
    ...(r.respuesta ? [r.respuesta] : []),
    ...r.hechos.map(h => `Hecho: ${h.texto}`),
    ...r.inferencias.map(i => `Inferencia: ${i}`),
    ...r.recomendaciones.map(x => `Recomendación: ${x.accion}`),
  ].join('\n')
}

/**
 * Asistente académico (HU-12 a HU-15). El agente consulta las herramientas con el token del docente
 * y responde separando hechos, inferencias y recomendaciones, con fuentes y fecha de corte (RN-09, RNF-08).
 */
export default function Agente() {
  const { cursos, cursoId, cursoActivo, cargando: cargandoCursos } = useCursoActivo()
  const [mensajes, setMensajes] = useState([])
  const [texto, setTexto] = useState('')
  const [enviando, setEnviando] = useState(false)
  const [estadoIA, setEstadoIA] = useState(null)
  const finRef = useRef(null)

  useEffect(() => {
    let vivo = true
    api('/api/ia/estado')
      .then(d => vivo && setEstadoIA(d))
      .catch(() => vivo && setEstadoIA({ estado: 'caido' }))
    return () => {
      vivo = false
    }
  }, [])

  // Cada curso tiene su propia conversación.
  useEffect(() => setMensajes([]), [cursoId])

  useEffect(() => {
    finRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [mensajes, enviando])

  if (!cargandoCursos && cursos.length === 0) return <SinCursos />

  async function preguntar(pregunta) {
    const limpia = pregunta.trim()
    if (limpia.length < 2 || enviando || !cursoId) return
    const historial = mensajes
      .filter(m => m.rol !== 'error')
      .slice(-6)
      .map(m => ({ rol: m.rol === 'docente' ? 'docente' : 'agente', texto: m.rol === 'docente' ? m.texto : textoRespuesta(m.respuesta) }))

    setMensajes(prev => [...prev, { rol: 'docente', texto: limpia }])
    setTexto('')
    setEnviando(true)
    try {
      const respuesta = await api('/api/ia/chat', { method: 'POST', body: { mensaje: limpia, cursoId, historial } })
      setMensajes(prev => [...prev, { rol: 'agente', respuesta }])
    } catch (err) {
      setMensajes(prev => [...prev, { rol: 'error', texto: err.message }])
    } finally {
      setEnviando(false)
    }
  }

  const curso = cursoActivo ? `${cursoActivo.nombre} (${cursoActivo.codigo})` : 'tu curso'
  const iaLista = estadoIA?.estado === 'operativo'

  return (
    <div className="ws-page">
      <Panel
        className="ag-panel"
        titulo="Asistente académico"
        icono={<IconAgente size={16} />}
        meta={`${curso} · responde solo con datos de tus herramientas`}
        acciones={
          <>
            <EstadoModelo estado={estadoIA} />
            <button
              type="button"
              className="ws-btn ws-btn-ghost ws-btn-sm"
              onClick={() => setMensajes([])}
              disabled={mensajes.length === 0 || enviando}
            >
              Nueva conversación
            </button>
          </>
        }
        cuerpoClassName="ag-body"
      >
        <div className="ag-messages" aria-live="polite">
          {mensajes.length === 0 && (
            <Vacio titulo="¿En qué te ayudo con este curso?" icono={<IconAgente size={22} />}>
              Pregunta en lenguaje natural. Cada respuesta separa <strong>hechos</strong> (cifras de tus datos),{' '}
              <strong>inferencias</strong> y <strong>recomendaciones</strong>, con las fuentes al pie.
            </Vacio>
          )}

          {mensajes.map((m, i) =>
            m.rol === 'docente' ? (
              <div key={i} className="ws-chat-msg ws-chat-user">{m.texto}</div>
            ) : m.rol === 'error' ? (
              <div key={i} className="ws-chat-msg ag-error" role="alert">
                <IconAdvertencia size={14} /> {m.texto}
              </div>
            ) : (
              <RespuestaAgente key={i} r={m.respuesta} />
            )
          )}

          {enviando && (
            <div className="ws-chat-msg ws-chat-bot ag-pensando" role="status">
              <span className="ws-spinner" /> Consultando las herramientas y redactando la respuesta…
            </div>
          )}
          <div ref={finRef} />
        </div>

        <div className="ag-composer">
          <div className="ws-chat-suggest">
            {SUGERENCIAS.map(s => (
              <button key={s} type="button" className="ag-suggest" onClick={() => preguntar(s)} disabled={enviando || !cursoId}>
                {s}
              </button>
            ))}
          </div>
          <form
            className="ag-input"
            onSubmit={e => {
              e.preventDefault()
              preguntar(texto)
            }}
          >
            <input
              type="text"
              value={texto}
              onChange={e => setTexto(e.target.value)}
              placeholder={iaLista || !estadoIA ? 'Escribe tu pregunta…' : 'El asistente no está disponible, pero puedes intentarlo…'}
              maxLength={1000}
              aria-label="Pregunta para el asistente"
              disabled={enviando}
            />
            <button type="submit" className="ws-btn ws-btn-dark ws-btn-sm" disabled={enviando || texto.trim().length < 2}>
              Enviar
            </button>
          </form>
          <p className="ag-disclaimer">
            Las alertas y recomendaciones son señales de seguimiento, no diagnósticos. El asistente no envía mensajes a los
            estudiantes: la decisión es tuya.
          </p>
        </div>
      </Panel>
    </div>
  )
}

function EstadoModelo({ estado }) {
  if (!estado) return null
  const ok = estado.estado === 'operativo'
  const titulo = ok
    ? `Modelo local ${estado.modelo} disponible`
    : estado.ollamaDisponible
      ? `Ollama responde, pero falta descargar el modelo ${estado.modelo}`
      : 'El modelo de lenguaje local no responde'
  return (
    <span className={`ws-pill ${ok ? 'ws-pill-ok' : 'ws-pill-warn'}`} title={titulo}>
      <span className={`ag-dot ${ok ? 'is-ok' : ''}`} aria-hidden="true" />
      {ok ? estado.modelo : 'IA no disponible'}
    </span>
  )
}

function Seccion({ etiqueta, clase, vacio, children, cantidad }) {
  return (
    <section>
      <span className={`ws-chat-tag ${clase}`}>{etiqueta}</span>
      {cantidad === 0 ? <p className="ag-vacio">{vacio}</p> : <ul>{children}</ul>}
    </section>
  )
}

function RespuestaAgente({ r }) {
  const [verTraza, setVerTraza] = useState(false)

  if (r.estado !== 'ok') {
    return (
      <div className="ws-chat-msg ws-chat-bot">
        <p className="ag-estado">
          <IconAdvertencia size={14} /> {r.mensaje}
        </p>
        {r.estado === 'sin_datos' && r.hechos.length > 0 && (
          <ul>
            {r.hechos.map((h, i) => (
              <li key={i}>{h.texto}</li>
            ))}
          </ul>
        )}
        <Pie r={r} verTraza={verTraza} setVerTraza={setVerTraza} />
      </div>
    )
  }

  return (
    <div className="ws-chat-msg ws-chat-bot">
      {r.respuesta && <p className="ag-respuesta">{r.respuesta}</p>}
      <Seccion etiqueta="Hechos" clase="ws-chat-tag-fact" cantidad={r.hechos.length} vacio="Sin hechos verificables con los datos actuales.">
        {r.hechos.map((h, i) => (
          <li key={i}>
            {h.texto}
            {h.fuente && <span className="ag-fuente">{NOMBRE_HERRAMIENTA[h.fuente] ?? h.fuente}</span>}
          </li>
        ))}
      </Seccion>
      <Seccion etiqueta="Inferencias" clase="ws-chat-tag-inference" cantidad={r.inferencias.length} vacio="Sin inferencias adicionales.">
        {r.inferencias.map((x, i) => (
          <li key={i}>{x}</li>
        ))}
      </Seccion>
      <Seccion etiqueta="Recomendaciones" clase="ws-chat-tag-reco" cantidad={r.recomendaciones.length} vacio="Sin recomendaciones con los datos actuales.">
        {r.recomendaciones.map((x, i) => (
          <li key={i}>
            <strong>{x.accion}</strong>
            {x.justificacion && <span className="ag-justificacion">Por qué: {x.justificacion}</span>}
          </li>
        ))}
      </Seccion>
      <Pie r={r} verTraza={verTraza} setVerTraza={setVerTraza} />
    </div>
  )
}

function Pie({ r, verTraza, setVerTraza }) {
  return (
    <>
      {r.advertencias.length > 0 && (
        <ul className="ag-advertencias">
          {r.advertencias.map((a, i) => (
            <li key={i}>{a}</li>
          ))}
        </ul>
      )}
      <footer>
        {r.fuentes.length > 0 && <>Fuentes: {r.fuentes.map(f => NOMBRE_HERRAMIENTA[f] ?? f).join(' · ')} · </>}
        {r.fechaCorte && <>corte {fechaHora(r.fechaCorte)} · </>}
        {r.modelo} · {(r.duracionMs / 1000).toFixed(1)} s ·{' '}
        <button type="button" className="ag-link" onClick={() => setVerTraza(v => !v)} aria-expanded={verTraza}>
          {verTraza ? 'ocultar traza' : `ver traza (${r.traza.length})`}
        </button>
      </footer>
      {verTraza && (
        <table className="ag-traza">
          <thead>
            <tr>
              <th>Herramienta</th>
              <th>Argumentos</th>
              <th>Resultado</th>
              <th>Tiempo</th>
            </tr>
          </thead>
          <tbody>
            {r.traza.map((t, i) => (
              <tr key={i}>
                <td>
                  <code>{t.herramienta}</code>
                </td>
                <td>
                  <code>{t.argumentos}</code>
                </td>
                <td className={t.exito ? 'ag-ok' : 'ag-fallo'}>{t.exito ? `OK (${t.codigoHttp})` : t.error}</td>
                <td>{t.duracionMs} ms</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  )
}
