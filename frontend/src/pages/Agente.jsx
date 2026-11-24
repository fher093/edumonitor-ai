import { useEffect, useRef, useState } from 'react'
import { api } from '../api/http.js'
import { useCursoActivo } from '../cursos/CursoContext.jsx'
import { Panel } from '../components/ui.jsx'
import { IconAgente, IconAdvertencia } from '../components/Icons.jsx'
import { fechaHora } from '../utils/formato.js'
import { SinCursos } from './Resumen.jsx'

// Pocas y variadas (NN/g recomienda 3-5): muestran lo que el asistente sabe hacer, incluido crear material.
const SUGERENCIAS = [
  'Resumen del curso',
  '¿Quién necesita apoyo esta semana?',
  '¿Qué actividades debo reforzar?',
  'Crea un deber de 5 preguntas sobre la actividad con más bajo rendimiento',
]

// Estados con nombre mientras se espera la respuesta (en lugar de un indicador genérico).
const ETAPAS_ESPERA = [
  { desde: 0, texto: 'Revisando las notas y alertas del curso…' },
  { desde: 2500, texto: 'Redactando la respuesta…' },
  { desde: 15000, texto: 'Sigue trabajando: el modelo local puede tardar un poco más…' },
]

const PREFIJO_HERRAMIENTA = /^(listar_cursos_autorizados|obtener_calificaciones|obtener_progreso|identificar_alertas|generar_resumen):/

/**
 * Avisos que sí le sirven al docente, en lenguaje simple. Los avisos internos del agente (verificación de cifras,
 * herramientas pedidas automáticamente) quedan en la auditoría y en la API, no en el chat.
 */
function avisosParaElDocente(advertencias) {
  return advertencias.flatMap(a => {
    if (a.startsWith('Gemini no respondió') || a.startsWith('Gemini no está configurado'))
      return ['Gemini no estaba disponible: respondió el modelo local.']
    if (a.startsWith('El modelo no devolvió una respuesta estructurada')) return ['Se muestran los datos del curso sin redactar.']
    if (PREFIJO_HERRAMIENTA.test(a)) {
      const [herramienta, ...resto] = a.split(':')
      return [`No se pudo consultar ${(NOMBRE_HERRAMIENTA[herramienta] ?? herramienta).toLowerCase()}: ${resto.join(':').trim()}`]
    }
    return []
  })
}

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
    ...(r.material ? [`Material: ${r.material.titulo}`] : []),
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
  const [proveedor, setProveedorEstado] = useState(() => {
    try {
      return localStorage.getItem(CLAVE_PROVEEDOR) === 'gemini' ? 'gemini' : 'ollama'
    } catch {
      return 'ollama'
    }
  })
  const finRef = useRef(null)

  function setProveedor(valor) {
    setProveedorEstado(valor)
    try {
      localStorage.setItem(CLAVE_PROVEEDOR, valor)
    } catch {
      /* Sin almacenamiento: la elección dura mientras la página esté abierta. */
    }
  }

  // Si Gemini no está configurado en el servidor, se usa el modelo local.
  const geminiDisponible = Boolean(estadoIA?.gemini?.configurado)
  const proveedorEfectivo = proveedor === 'gemini' && geminiDisponible ? 'gemini' : 'ollama'

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
      const respuesta = await api('/api/ia/chat', { method: 'POST', body: { mensaje: limpia, cursoId, historial, proveedor: proveedorEfectivo } })
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
        meta={`${curso} · responde con los datos de tu curso`}
        acciones={
          <>
            <SelectorModelo
              estado={estadoIA}
              valor={proveedorEfectivo}
              onCambio={setProveedor}
              geminiDisponible={geminiDisponible}
              deshabilitado={enviando}
            />
            <AvisoDisponibilidad estado={estadoIA} proveedor={proveedorEfectivo} />
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
            <div className="ag-bienvenida">
              <span className="ag-bienvenida-icono">
                <IconAgente size={20} />
              </span>
              <strong>¿En qué te ayudo con {cursoActivo?.nombre ?? 'este curso'}?</strong>
              <ul>
                <li>Resumir cómo va el curso y quién necesita apoyo, y por qué.</li>
                <li>Revisar a un estudiante: sus notas más bajas y qué trabajar en una tutoría.</li>
                <li>Sugerir qué actividades reforzar con el grupo.</li>
                <li>Crear material de práctica: deberes, preguntas o ejercicios.</li>
              </ul>
              <span className="ag-bienvenida-nota">Usa solo los datos de tus cursos. Escribe con tus palabras o elige una sugerencia.</span>
            </div>
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

          {enviando && <Esperando />}
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

const CLAVE_PROVEEDOR = 'edumonitor_ia_proveedor'

/** Selector del modelo: local (Ollama, privado) o Gemini (más rápido, nombres seudonimizados). */
function SelectorModelo({ valor, onCambio, geminiDisponible, deshabilitado }) {
  return (
    <div className="ag-selector" role="radiogroup" aria-label="Modelo de IA">
      <button
        type="button"
        role="radio"
        aria-checked={valor === 'ollama'}
        className={valor === 'ollama' ? 'is-active' : ''}
        onClick={() => onCambio('ollama')}
        disabled={deshabilitado}
        title="Modelo local en este equipo: los datos no salen de la universidad"
      >
        Local · privado
      </button>
      <button
        type="button"
        role="radio"
        aria-checked={valor === 'gemini'}
        className={valor === 'gemini' ? 'is-active' : ''}
        onClick={() => onCambio('gemini')}
        disabled={deshabilitado || !geminiDisponible}
        title={
          geminiDisponible
            ? 'Gemini (Google): más rápido. Los nombres de los estudiantes se reemplazan por seudónimos antes de enviarse.'
            : 'Gemini no está configurado (falta GEMINI_API_KEY en el servidor)'
        }
      >
        Gemini · rápido
      </button>
    </div>
  )
}

/** Solo avisa cuando la opción elegida no está disponible; el nombre técnico del modelo no se muestra. */
function AvisoDisponibilidad({ estado, proveedor }) {
  if (!estado || proveedor === 'gemini' || estado.estado === 'operativo') return null
  return (
    <span className="ws-pill ws-pill-warn" title="Abre Ollama en este equipo o elige Gemini">
      IA local no disponible
    </span>
  )
}

function Esperando() {
  const [ms, setMs] = useState(0)
  useEffect(() => {
    const inicio = Date.now()
    const t = setInterval(() => setMs(Date.now() - inicio), 500)
    return () => clearInterval(t)
  }, [])
  const etapa = [...ETAPAS_ESPERA].reverse().find(e => ms >= e.desde)
  return (
    <div className="ws-chat-msg ws-chat-bot ag-pensando" role="status">
      <span className="ws-spinner" /> {etapa.texto}
    </div>
  )
}

/** Sección de la respuesta; si no tiene contenido no se muestra (menos ruido). */
function Seccion({ etiqueta, clase, children, cantidad }) {
  if (cantidad === 0) return null
  return (
    <section>
      <span className={`ws-chat-tag ${clase}`}>{etiqueta}</span>
      <ul>{children}</ul>
    </section>
  )
}

/** Material de apoyo generado por la IA (deber, preguntas…): aparte de los datos verificados y con botón para copiarlo. */
function Material({ m }) {
  const [copiado, setCopiado] = useState(false)
  async function copiar() {
    const texto = [m.titulo, '', ...m.items.map((x, i) => `${i + 1}. ${x}`)].join('\n')
    try {
      await navigator.clipboard.writeText(texto)
      setCopiado(true)
      setTimeout(() => setCopiado(false), 2000)
    } catch {
      /* Sin permiso de portapapeles: el docente puede seleccionar el texto a mano. */
    }
  }
  return (
    <section className="ag-material">
      <div className="ag-material-head">
        <span className="ws-chat-tag ag-material-tag">Material sugerido por IA</span>
        <button type="button" className="ws-btn ws-btn-ghost ws-btn-sm" onClick={copiar}>
          {copiado ? 'Copiado' : 'Copiar'}
        </button>
      </div>
      <strong>{m.titulo}</strong>
      <ol>
        {m.items.map((x, i) => (
          <li key={i}>{x}</li>
        ))}
      </ol>
      <p className="ag-material-nota">{m.nota}</p>
    </section>
  )
}

function RespuestaAgente({ r }) {
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
        <Pie r={r} />
      </div>
    )
  }

  return (
    <div className="ws-chat-msg ws-chat-bot">
      {r.respuesta && <p className="ag-respuesta">{r.respuesta}</p>}
      {r.material && <Material m={r.material} />}
      <Seccion etiqueta="Hechos" clase="ws-chat-tag-fact" cantidad={r.hechos.length}>
        {r.hechos.map((h, i) => (
          <li key={i}>{h.texto}</li>
        ))}
      </Seccion>
      <Seccion etiqueta="Inferencias" clase="ws-chat-tag-inference" cantidad={r.inferencias.length}>
        {r.inferencias.map((x, i) => (
          <li key={i}>{x}</li>
        ))}
      </Seccion>
      <Seccion etiqueta="Recomendaciones" clase="ws-chat-tag-reco" cantidad={r.recomendaciones.length}>
        {r.recomendaciones.map((x, i) => (
          <li key={i}>
            <strong>{x.accion}</strong>
            {x.justificacion && <span className="ag-justificacion">Por qué: {x.justificacion}</span>}
          </li>
        ))}
      </Seccion>
      <Pie r={r} />
    </div>
  )
}

/** Pie: avisos útiles y de dónde salen los datos y a qué fecha (RNF-08). Sin detalles técnicos. */
function Pie({ r }) {
  const avisos = avisosParaElDocente(r.advertencias)
  const fuentes = r.fuentes.map(f => NOMBRE_HERRAMIENTA[f] ?? f)
  return (
    <>
      {avisos.length > 0 && (
        <ul className="ag-advertencias">
          {avisos.map((a, i) => (
            <li key={i}>{a}</li>
          ))}
        </ul>
      )}
      {(fuentes.length > 0 || r.fechaCorte) && (
        <footer>
          {fuentes.length > 0 && <>Basado en: {fuentes.join(', ').toLowerCase()}</>}
          {r.fechaCorte && <> · datos al {fechaHora(r.fechaCorte)}</>}
        </footer>
      )}
    </>
  )
}
