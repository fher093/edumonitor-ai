import { useEffect, useState } from 'react'
import { api } from '../api/http.js'
import { fechaHora } from '../utils/formato.js'

const CAMPOS = {
  descenso: [
    { clave: 'k', etiqueta: 'Ventana de evaluaciones (k)', tipo: 'number', min: 1, step: 1 },
    { clave: 'delta', etiqueta: 'Caída mínima en puntos (Δ)', tipo: 'number', min: 0, step: 0.1 },
  ],
  atraso: [
    { clave: 'n', etiqueta: 'Entregas vencidas mínimas (n)', tipo: 'number', min: 1, step: 1 },
  ],
  baja_finalizacion: [
    { clave: 'p', etiqueta: 'Finalización mínima (%) (p)', tipo: 'number', min: 1, max: 100, step: 1 },
  ],
}

const DESCRIPCION = {
  descenso: 'Activa una alerta cuando el promedio de las últimas k evaluaciones cae más de Δ puntos sobre 10 respecto a las anteriores (RN-04).',
  atraso: 'Activa una alerta cuando el estudiante acumula n o más actividades vencidas sin entrega (RN-05).',
  baja_finalizacion: 'Activa una alerta cuando el porcentaje de actividades vencidas completadas es menor que p (RN-06).',
}

function leerParametros(json) {
  try {
    const datos = JSON.parse(json)
    return typeof datos === 'object' && datos !== null ? datos : {}
  } catch {
    return {}
  }
}

export default function AdminReglas() {
  const [reglas, setReglas] = useState([])
  const [formularios, setFormularios] = useState({})
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [mensajes, setMensajes] = useState({})
  const [guardandoId, setGuardandoId] = useState(null)

  useEffect(() => {
    let vivo = true
    setCargando(true)
    api('/api/alertas/reglas')
      .then(data => {
        if (!vivo) return
        setReglas(data)
        const inicial = {}
        data.forEach(r => {
          const parametros = leerParametros(r.parametrosJson)
          inicial[r.id] = { activa: r.activa, parametros }
        })
        setFormularios(inicial)
      })
      .catch(err => {
        if (vivo) setError(err.message)
      })
      .finally(() => {
        if (vivo) setCargando(false)
      })
    return () => {
      vivo = false
    }
  }, [])

  function actualizarCampo(id, campo, valor) {
    setMensajes(prev => ({ ...prev, [id]: { tipo: '', texto: '' } }))
    setFormularios(prev => ({
      ...prev,
      [id]: {
        ...prev[id],
        parametros: { ...prev[id].parametros, [campo]: valor },
      },
    }))
  }

  function alternarActiva(id) {
    setMensajes(prev => ({ ...prev, [id]: { tipo: '', texto: '' } }))
    setFormularios(prev => ({
      ...prev,
      [id]: { ...prev[id], activa: !prev[id].activa },
    }))
  }

  async function guardar(regla) {
    const form = formularios[regla.id]
    if (!form) return
    setGuardandoId(regla.id)
    setMensajes(prev => ({ ...prev, [regla.id]: { tipo: '', texto: '' } }))
    try {
      const parametros = {}
      Object.entries(form.parametros).forEach(([clave, valor]) => {
        parametros[clave] = Number(valor)
      })
      const actualizada = await api(`/api/alertas/reglas/${regla.id}`, {
        method: 'PUT',
        body: {
          activa: form.activa,
          parametrosJson: JSON.stringify(parametros),
        },
      })
      setReglas(prev => prev.map(r => (r.id === actualizada.id ? actualizada : r)))
      setMensajes(prev => ({
        ...prev,
        [regla.id]: { tipo: 'ok', texto: 'Regla actualizada correctamente.' },
      }))
    } catch (err) {
      setMensajes(prev => ({
        ...prev,
        [regla.id]: { tipo: 'error', texto: err.message },
      }))
    } finally {
      setGuardandoId(null)
    }
  }

  return (
    <div className="page-container">
      <section className="product-surface-card">
        <div className="section-head-split">
          <div>
            <div className="brand-dot-pill">
              <span className="dot-violet" />
              <span className="tag-uta-micro">ADMINISTRACIÓN TÉCNICA</span>
            </div>
            <h1 className="display-headline">Reglas de Alertas Académicas</h1>
            <p className="lead-subtitle">
              Ajusta los umbrales deterministas de las alertas de descenso, atraso y baja
              finalización. Los cambios aplican a todos los cursos y quedan auditados con fecha.
            </p>
          </div>
          <div className="stat-pill-counter">
            <span className="stat-number">{reglas.length}</span>
            <span className="stat-label">reglas configuradas</span>
          </div>
        </div>

        <div className="hairline-divider" />

        {cargando && (
          <div className="state-empty-box">
            <p className="text-muted">Cargando reglas de alerta...</p>
          </div>
        )}
        {error && <div className="banner-alert banner-alert-error">{error}</div>}

        {!cargando && reglas.length === 0 && (
          <div className="state-empty-box">
            <p className="text-muted">No hay reglas de alerta configuradas en el sistema.</p>
          </div>
        )}

        <div className="reglas-grid">
          {reglas.map(regla => {
            const form = formularios[regla.id]
            if (!form) return null
            const mensaje = mensajes[regla.id]
            const campos = CAMPOS[regla.tipo] ?? []
            return (
              <article key={regla.id} className="regla-card">
                <div className="regla-head">
                  <div>
                    <span className={`status-pill ${form.activa ? 'status-pill-active' : 'status-pill-neutral'}`}>
                      {form.activa ? 'Activa' : 'Inactiva'}
                    </span>
                    <h3 className="regla-title">{regla.nombre}</h3>
                    <p className="text-muted text-sm">{DESCRIPCION[regla.tipo] ?? regla.tipo}</p>
                  </div>
                </div>

                <div className="regla-body">
                  {campos.map(campo => (
                    <div key={campo.clave} className="form-control-item">
                      <label htmlFor={`regla-${regla.id}-${campo.clave}`}>{campo.etiqueta}</label>
                      <input
                        id={`regla-${regla.id}-${campo.clave}`}
                        type={campo.tipo}
                        min={campo.min}
                        max={campo.max}
                        step={campo.step}
                        value={form.parametros[campo.clave] ?? ''}
                        onChange={e => actualizarCampo(regla.id, campo.clave, e.target.value)}
                      />
                    </div>
                  ))}

                  <label className="toggle-line">
                    <input
                      type="checkbox"
                      checked={form.activa}
                      onChange={() => alternarActiva(regla.id)}
                    />
                    <span>Habilitar esta regla para el cálculo de alertas</span>
                  </label>

                  {mensaje?.texto && (
                    <div className={`banner-alert ${mensaje.tipo === 'error' ? 'banner-alert-error' : 'banner-alert-ok'}`}>
                      {mensaje.texto}
                    </div>
                  )}
                </div>

                <div className="regla-foot">
                  <span className="text-muted text-xs">
                    Última actualización: {fechaHora(regla.fechaActualizacion)}
                  </span>
                  <button
                    type="button"
                    className="btn-pill btn-pill-dark btn-sm"
                    onClick={() => guardar(regla)}
                    disabled={guardandoId === regla.id}
                  >
                    {guardandoId === regla.id ? 'Guardando...' : 'Guardar regla'}
                  </button>
                </div>
              </article>
            )
          })}
        </div>

        <div className="banner-alert banner-alert-info panel-box-spaced">
          Las alertas son señales de seguimiento con evidencia verificable. No reemplazan el juicio
          docente ni deben usarse como etiquetas definitivas del estudiante.
        </div>
      </section>
    </div>
  )
}
