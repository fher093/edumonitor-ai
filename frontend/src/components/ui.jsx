import { useEffect, useRef, useState } from 'react'
import { IconAdvertencia, IconChevronDown, IconClose, IconMas, IconSearch } from './Icons.jsx'
import { claseEstado, etiquetaEstado, fecha, numero } from '../utils/formato.js'
import { apiBlob } from '../api/http.js'
import { useCursoOpcional } from '../cursos/CursoContext.jsx'

/* Componentes de interfaz del espacio de trabajo (DESIGN.md): densos, planos, con pills de 9999px. */

const TONOS_AVATAR = ['violet', 'blue', 'teal', 'amber', 'rose', 'slate']

function iniciales(nombre = '') {
  const partes = nombre.trim().split(/\s+/)
  return ((partes[0]?.[0] ?? '') + (partes[1]?.[0] ?? '')).toUpperCase() || '?'
}

// Fotos ya descargadas en esta sesión: "curso:estudiante" -> Promise<URL local | null>.
const fotosDescargadas = new Map()

function cargarFoto(cursoId, estudianteId) {
  const clave = `${cursoId}:${estudianteId}`
  if (!fotosDescargadas.has(clave)) {
    fotosDescargadas.set(
      clave,
      apiBlob(`/api/cursos/${cursoId}/estudiantes/${estudianteId}/foto`)
        .then(blob => (blob ? URL.createObjectURL(blob) : null))
        .catch(() => null)
    )
  }
  return fotosDescargadas.get(clave)
}

/**
 * Avatar circular. Si se indica `estudianteId` y el estudiante tiene foto en Moodle, la muestra;
 * si no (o mientras carga), muestra sus iniciales.
 */
export function Avatar({ nombre, tamano = 32, estudianteId }) {
  const curso = useCursoOpcional()
  const cursoId = curso?.cursoId
  const tieneFoto = Boolean(estudianteId && cursoId && curso?.conFoto?.has(estudianteId))
  const [foto, setFoto] = useState(null)

  useEffect(() => {
    setFoto(null)
    if (!tieneFoto) return
    let vivo = true
    cargarFoto(cursoId, estudianteId).then(url => vivo && setFoto(url))
    return () => {
      vivo = false
    }
  }, [tieneFoto, cursoId, estudianteId])

  let hash = 0
  for (const c of nombre ?? '') hash = (hash * 31 + c.charCodeAt(0)) >>> 0
  const tono = TONOS_AVATAR[hash % TONOS_AVATAR.length]
  return (
    <span
      className={`ws-avatar ws-avatar-${tono} ${foto ? 'ws-avatar-foto' : ''}`}
      style={{ width: tamano, height: tamano, fontSize: tamano * 0.38 }}
      aria-hidden="true"
    >
      {foto ? <img src={foto} alt="" width={tamano} height={tamano} onError={() => setFoto(null)} /> : iniciales(nombre)}
    </span>
  )
}

/** Tono semántico de una nota según su porcentaje de logro (umbral UTA: 70 % = 14/20). */
export function tonoNota(valor, escala) {
  if (valor === null || valor === undefined) return 'none'
  const p = (Number(valor) / escala) * 100
  if (p >= 70) return 'ok'
  if (p >= 50) return 'warn'
  return 'danger'
}

/** Nota con su escala explícita (RN-01) y una barra de logro. */
export function NotaMeter({ valor, escala, compacto = false, pendiente = 'Pendiente' }) {
  const tono = tonoNota(valor, escala)
  const ancho = valor === null || valor === undefined ? 0 : Math.max(0, Math.min(100, (valor / escala) * 100))
  return (
    <span className={`ws-meter ${compacto ? 'ws-meter-compact' : ''}`}>
      <span className={`ws-meter-value ws-tone-${tono}`}>
        {valor === null || valor === undefined ? (
          <span className="ws-muted">{pendiente}</span>
        ) : (
          <>
            {numero(valor)}
            <small>/{numero(escala, 0)}</small>
          </>
        )}
      </span>
      <span className="ws-meter-track">
        <span className={`ws-meter-fill ws-fill-${tono}`} style={{ width: `${ancho}%` }} />
      </span>
    </span>
  )
}

export function EstadoChip({ estado }) {
  return <span className={claseEstado(estado)}>{etiquetaEstado(estado)}</span>
}

/**
 * Indicador grande (Stat Callout de DESIGN.md) en versión compacta. `icono` identifica el indicador
 * y toma el color suave de su `tono` (ok, warn, danger); sin tono queda en gris.
 */
export function Kpi({ etiqueta, valor, detalle, tono, icono }) {
  return (
    <div className={`ws-kpi ${tono ? `ws-kpi-${tono}` : ''}`}>
      <span className="ws-kpi-label">
        {icono && <span className="ws-kpi-icon">{icono}</span>}
        <span className="ws-eyebrow">{etiqueta}</span>
      </span>
      <span className="ws-kpi-value">{valor}</span>
      {detalle && <span className="ws-kpi-detail">{detalle}</span>}
    </div>
  )
}

/** Encabezado compacto de página: título, descripción corta y acciones a la derecha. */
export function PageHeader({ eyebrow, titulo, descripcion, datos, children }) {
  return (
    <header className="ws-page-header">
      <div className="ws-page-heading">
        {eyebrow && <span className="ws-eyebrow">{eyebrow}</span>}
        <h1>{titulo}</h1>
        {descripcion && <p>{descripcion}</p>}
        {datos?.length > 0 && (
          <dl className="ws-facts">
            {datos.map(d => (
              <div key={d.etiqueta}>
                <dt>{d.etiqueta}</dt>
                <dd>{d.valor}</dd>
              </div>
            ))}
          </dl>
        )}
      </div>
      {children && <div className="ws-page-actions">{children}</div>}
    </header>
  )
}

/**
 * Aviso compacto (p. ej. "Datos incompletos"): una etiqueta con icono que despliega la explicación
 * en un recuadro flotante, en lugar de ocupar una línea entera de la página.
 */
export function Aviso({ titulo, mensajes }) {
  const [abierto, setAbierto] = useState(false)
  const ref = useRef(null)

  useEffect(() => {
    if (!abierto) return
    const fuera = e => ref.current && !ref.current.contains(e.target) && setAbierto(false)
    const escape = e => e.key === 'Escape' && setAbierto(false)
    document.addEventListener('mousedown', fuera)
    document.addEventListener('keydown', escape)
    return () => {
      document.removeEventListener('mousedown', fuera)
      document.removeEventListener('keydown', escape)
    }
  }, [abierto])

  if (!mensajes?.length) return null
  return (
    <span className="ws-aviso" ref={ref}>
      <button type="button" className="ws-aviso-btn" onClick={() => setAbierto(a => !a)} aria-expanded={abierto}>
        <IconAdvertencia size={13} />
        {titulo}
        <IconChevronDown size={13} className={abierto ? 'is-open' : ''} />
      </button>
      {abierto && (
        <span className="ws-aviso-pop" role="note">
          <strong>{titulo}</strong>
          {mensajes.map((m, i) => (
            <span key={i}>{m}</span>
          ))}
        </span>
      )}
    </span>
  )
}

/** Botón "+" / "−" que despliega u oculta el detalle de un panel. */
function BotonDesplegar({ abierto, onClick, titulo }) {
  return (
    <button
      type="button"
      className={`ws-expand ${abierto ? 'is-open' : ''}`}
      onClick={onClick}
      aria-expanded={abierto}
      aria-label={`${abierto ? 'Ocultar' : 'Mostrar'} detalle de ${titulo}`}
      title={abierto ? 'Ver menos' : 'Ver más'}
    >
      <IconMas size={15} />
    </button>
  )
}

/**
 * Panel con cabecera fija y cuerpo con scroll propio.
 * - `icono`: icono SVG que identifica el panel.
 * - `expandible`: muestra un botón "+" en la cabecera; `children` puede ser una función
 *   `(abierto) => contenido` para mostrar lo esencial cerrado y el detalle completo abierto.
 */
export function Panel({
  titulo,
  meta,
  icono,
  acciones,
  expandible = false,
  inicialAbierto = false,
  children,
  className = '',
  cuerpoClassName = '',
}) {
  const [abierto, setAbierto] = useState(inicialAbierto)
  const contenido = typeof children === 'function' ? children(abierto, () => setAbierto(true)) : children
  return (
    <section className={`ws-panel ${className}`}>
      {(titulo || acciones) && (
        <div className="ws-panel-head">
          <div className="ws-panel-title">
            {icono && <span className="ws-panel-icon">{icono}</span>}
            <div>
              {titulo && <h2>{titulo}</h2>}
              {meta && <span className="ws-panel-meta">{meta}</span>}
            </div>
          </div>
          {(acciones || expandible) && (
            <div className="ws-panel-actions">
              {acciones}
              {expandible && <BotonDesplegar abierto={abierto} onClick={() => setAbierto(a => !a)} titulo={titulo} />}
            </div>
          )}
        </div>
      )}
      <div className={`ws-panel-body ${cuerpoClassName}`}>{contenido}</div>
    </section>
  )
}

/** Tarjeta dentro de un detalle, con el mismo patrón de icono y "+" que `Panel`. */
export function Tarjeta({ titulo, meta, icono, expandible = false, className = '', children }) {
  const [abierto, setAbierto] = useState(false)
  const contenido = typeof children === 'function' ? children(abierto) : children
  return (
    <div className={`ws-card ${className}`}>
      <div className="ws-card-head">
        <div className="ws-panel-title">
          {icono && <span className="ws-panel-icon ws-panel-icon-sm">{icono}</span>}
          <div>
            <h3>{titulo}</h3>
            {meta && <span className="ws-muted ws-small">{meta}</span>}
          </div>
        </div>
        {expandible && <BotonDesplegar abierto={abierto} onClick={() => setAbierto(a => !a)} titulo={titulo} />}
      </div>
      {contenido}
    </div>
  )
}

/** Pie "Mostrar N más" para listas recortadas en un panel cerrado. */
export function VerMas({ restantes, onClick }) {
  if (!(restantes > 0)) return null
  return (
    <button type="button" className="ws-more" onClick={onClick}>
      <IconMas size={13} /> Mostrar {restantes} más
    </button>
  )
}

export function Buscador({ valor, onCambio, placeholder, etiqueta }) {
  return (
    <label className="ws-search">
      <IconSearch size={15} />
      <input
        type="search"
        value={valor}
        onChange={e => onCambio(e.target.value)}
        placeholder={placeholder}
        aria-label={etiqueta ?? placeholder}
      />
    </label>
  )
}

/** Grupo de filtros tipo pestañas (Feature Tag Pill). `opciones`: [{ valor, etiqueta, cantidad? }]. */
export function FiltroChips({ opciones, valor, onCambio, etiqueta }) {
  return (
    <div className="ws-chips" role="tablist" aria-label={etiqueta}>
      {opciones.map(o => (
        <button
          key={o.valor}
          type="button"
          role="tab"
          aria-selected={valor === o.valor}
          className={`ws-chip ${valor === o.valor ? 'is-active' : ''}`}
          onClick={() => onCambio(o.valor)}
        >
          {o.etiqueta}
          {o.cantidad !== undefined && <span className="ws-chip-count">{o.cantidad}</span>}
        </button>
      ))}
    </div>
  )
}

export function Vacio({ titulo, children, icono }) {
  return (
    <div className="ws-empty">
      {icono && <span className="ws-empty-icon">{icono}</span>}
      <strong>{titulo}</strong>
      {children && <p>{children}</p>}
    </div>
  )
}

export function Cargando({ texto = 'Cargando...' }) {
  return (
    <div className="ws-loading" role="status">
      <span className="ws-spinner" />
      {texto}
    </div>
  )
}

/** Mide el ancho real de un elemento para dibujar SVG sin deformar círculos ni texto. */
function useAncho(inicial = 600) {
  const ref = useRef(null)
  const [ancho, setAncho] = useState(inicial)
  useEffect(() => {
    if (!ref.current) return
    const obs = new ResizeObserver(([e]) => setAncho(Math.max(240, Math.round(e.contentRect.width))))
    obs.observe(ref.current)
    return () => obs.disconnect()
  }, [])
  return [ref, ancho]
}

const COLOR_ESTADO_SIN_NOTA = {
  sin_entrega: 'warn',
  pendiente: 'neutral',
  completada: 'info',
}

/** Nombre corto para el eje X: "Lección 1: Fundamentos" -> "Lección 1". */
function etiquetaCorta(nombre) {
  return (nombre ?? '').split(':')[0].trim()
}

/**
 * Evolución de notas de un estudiante (HU-08) con ejes rotulados.
 * Eje Y: nota normalizada sobre 10. Eje X: actividades en orden de entrega.
 * Puntos llenos = actividades con nota (oscuros si alcanzan el umbral, rojo suave si no);
 * círculos vacíos sobre el eje = actividades sin nota, con el color de su estado.
 * `actividades`: [{ id, nombre, nota, puntajeMaximo, estado, fechaEntrega }]
 */
export function GraficoEvolucion({ actividades, umbral = 7, alto = 230 }) {
  const [ref, ancho] = useAncho()
  const m = { izq: 44, der: 64, sup: 14, inf: 52 }
  const w = ancho - m.izq - m.der
  const h = alto - m.sup - m.inf
  const n = actividades.length

  if (n === 0) return <p className="ws-muted ws-small">Este estudiante aún no tiene actividades.</p>

  const x = i => m.izq + (n === 1 ? w / 2 : (i * w) / (n - 1))
  const y = v => m.sup + h - (v / 10) * h
  const puntos = actividades.map((a, i) => {
    const sobre10 = a.nota === null || a.nota === undefined || !a.puntajeMaximo ? null : (a.nota / a.puntajeMaximo) * 10
    return { ...a, i, sobre10 }
  })

  // La línea se corta donde falta nota: no se inventa una tendencia sobre huecos.
  let d = ''
  let previaConNota = false
  puntos.forEach(p => {
    if (p.sobre10 === null) {
      previaConNota = false
      return
    }
    d += `${previaConNota ? 'L' : 'M'}${x(p.i).toFixed(1)},${y(p.sobre10).toFixed(1)} `
    previaConNota = true
  })

  const espacio = n === 1 ? w : w / (n - 1)
  const maxCaracteres = Math.max(4, Math.floor(espacio / 6.2))
  const recortar = t => (t.length > maxCaracteres ? `${t.slice(0, maxCaracteres - 1)}…` : t)

  return (
    <div ref={ref} className="ws-chart">
      <svg width={ancho} height={alto} role="img" aria-label={`Evolución de notas sobre 10 en ${n} actividades; umbral ${umbral} sobre 10`}>
        {/* Rejilla y eje Y */}
        {[0, 2, 4, 6, 8, 10].map(v => (
          <g key={v}>
            <line x1={m.izq} x2={m.izq + w} y1={y(v)} y2={y(v)} className={v === 0 ? 'ws-chart-axis' : 'ws-chart-grid'} />
            <text x={m.izq - 8} y={y(v)} className="ws-chart-tick" textAnchor="end" dominantBaseline="middle">
              {v}
            </text>
          </g>
        ))}
        <text className="ws-chart-title" transform={`translate(12 ${m.sup + h / 2}) rotate(-90)`} textAnchor="middle">
          Nota (sobre 10)
        </text>

        {/* Umbral */}
        <line x1={m.izq} x2={m.izq + w} y1={y(umbral)} y2={y(umbral)} className="ws-chart-umbral" />
        <text x={m.izq + w + 6} y={y(umbral)} className="ws-chart-umbral-text" dominantBaseline="middle">
          Umbral {umbral}
        </text>

        {/* Serie */}
        <path d={d} className="ws-chart-line" />
        {puntos.map(p => {
          const titulo = `${p.nombre} — ${p.sobre10 === null ? 'sin nota' : `${p.sobre10.toFixed(2)}/10`} · ${etiquetaEstado(p.estado)} · ${fecha(p.fechaEntrega)}`
          return p.sobre10 === null ? (
            <circle key={p.id} cx={x(p.i)} cy={y(0)} r="4.5" className={`ws-chart-hueco ws-chart-${COLOR_ESTADO_SIN_NOTA[p.estado] ?? 'neutral'}`}>
              <title>{titulo}</title>
            </circle>
          ) : (
            <circle key={p.id} cx={x(p.i)} cy={y(p.sobre10)} r="4.5" className={`ws-chart-punto ${p.sobre10 < umbral ? 'is-bajo' : ''}`}>
              <title>{titulo}</title>
            </circle>
          )
        })}

        {/* Eje X */}
        {puntos.map(p => (
          <text key={`x-${p.id}`} x={x(p.i)} y={m.sup + h + 18} className="ws-chart-tick" textAnchor="middle">
            <title>{p.nombre}</title>
            {recortar(etiquetaCorta(p.nombre))}
          </text>
        ))}
        <text x={m.izq + w / 2} y={alto - 6} className="ws-chart-title" textAnchor="middle">
          Actividades en orden de entrega
        </text>
      </svg>
    </div>
  )
}

/** Barra horizontal de porcentaje, para distribución, logro y finalización. */
export function Barra({ porcentaje, tono = 'brand' }) {
  const ancho = Math.max(0, Math.min(100, Number(porcentaje) || 0))
  return (
    <span className="ws-bar">
      <span className={`ws-bar-fill ws-fill-${tono}`} style={{ width: `${ancho}%` }} />
    </span>
  )
}

/** Ventana modal accesible: cierra con Escape o clic fuera. */
export function Modal({ abierto, onCerrar, titulo, eyebrow, ancho = 560, children, pie }) {
  useEffect(() => {
    if (!abierto) return
    const cerrar = e => e.key === 'Escape' && onCerrar()
    window.addEventListener('keydown', cerrar)
    return () => window.removeEventListener('keydown', cerrar)
  }, [abierto, onCerrar])

  if (!abierto) return null
  return (
    <div className="ws-modal-backdrop" onClick={e => e.target === e.currentTarget && onCerrar()}>
      <div className="ws-modal" role="dialog" aria-modal="true" aria-label={titulo} style={{ maxWidth: ancho }}>
        <div className="ws-modal-head">
          <div>
            {eyebrow && <span className="ws-eyebrow">{eyebrow}</span>}
            <h2>{titulo}</h2>
          </div>
          <button type="button" className="ws-icon-btn" onClick={onCerrar} aria-label="Cerrar">
            <IconClose size={16} />
          </button>
        </div>
        <div className="ws-modal-body">{children}</div>
        {pie && <div className="ws-modal-foot">{pie}</div>}
      </div>
    </div>
  )
}
