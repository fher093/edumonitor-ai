import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/http.js'
import { useAuth } from '../auth/useAuth.js'
import { ROLES } from '../auth/roles.js'
import useCursos from '../hooks/useCursos.js'
import SelectorCurso from '../components/SelectorCurso.jsx'
import { fechaHora } from '../utils/formato.js'

const TIPOS = {
  descenso: 'Descenso de rendimiento',
  atraso: 'Atraso en entregas',
  baja_finalizacion: 'Baja finalización',
}

function EtiquetaParametros({ json }) {
  let datos
  try {
    datos = JSON.parse(json)
  } catch {
    return <code className="param-code">{json}</code>
  }
  return (
    <span className="param-list">
      {Object.entries(datos).map(([clave, valor]) => (
        <span key={clave} className="param-chip">
          {clave} = {String(valor)}
        </span>
      ))}
    </span>
  )
}

export default function Alertas() {
  const { usuario } = useAuth()
  const cursosHook = useCursos()
  const { cursoId, cursoActivo, cargando: cargandoCursos } = cursosHook

  const [datos, setDatos] = useState(null)
  const [cargando, setCargando] = useState(false)
  const [error, setError] = useState('')
  const [tipo, setTipo] = useState('')
  const [busqueda, setBusqueda] = useState('')

  const esAdmin = usuario?.rol === ROLES.ADMIN
  const rutaFicha = esAdmin ? '/admin/ficha' : '/ficha'

  useEffect(() => {
    if (!cursoId) {
      setDatos(null)
      return
    }
    let vivo = true
    setCargando(true)
    setError('')
    api(`/api/alertas/curso/${cursoId}`)
      .then(data => {
        if (vivo) setDatos(data)
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
  }, [cursoId])

  const alertasFiltradas = useMemo(() => {
    if (!datos) return []
    const q = busqueda.trim().toLowerCase()
    return datos.alertas.filter(a => {
      if (tipo && a.tipo !== tipo) return false
      if (!q) return true
      return (
        a.nombre.toLowerCase().includes(q) ||
        a.identificacion.toLowerCase().includes(q) ||
        a.evidencia.toLowerCase().includes(q)
      )
    })
  }, [datos, tipo, busqueda])

  return (
    <div className="page-container">
      <section className="product-surface-card">
        <div className="section-head-split">
          <div>
            <div className="brand-dot-pill">
              <span className="dot-violet" />
              <span className="tag-uta-micro">ALERTAS TEMPRANAS</span>
            </div>
            <h1 className="display-headline">Seguimiento Oportuno</h1>
            <p className="lead-subtitle">
              Cada alerta muestra la regla aplicada y la evidencia numérica que la activó.
              Son señales para el acompañamiento docente, no etiquetas definitivas del estudiante.
            </p>
          </div>
          {datos && (
            <div className="stat-pill-counter">
              <span className="stat-number">{datos.resumen.total}</span>
              <span className="stat-label">alertas activas</span>
            </div>
          )}
        </div>

        <div className="hairline-divider" />

        <SelectorCurso cursosHook={cursosHook} />

        {!cargandoCursos && !cursoId && (
          <div className="state-empty-box">
            <p className="text-muted">No hay asignaturas disponibles para evaluar alertas.</p>
          </div>
        )}

        {cargando && (
          <div className="state-empty-box">
            <p className="text-muted">Evaluando reglas de alerta del curso...</p>
          </div>
        )}
        {error && <div className="banner-alert banner-alert-error">{error}</div>}

        {!cargando && datos && (
          <>
            {datos.advertencias?.length > 0 && (
              <div className="banner-alert banner-alert-info">
                <ul className="inline-warning-list">
                  {datos.advertencias.map((a, i) => <li key={i}>{a}</li>)}
                </ul>
              </div>
            )}

            <div className="kpi-grid">
              <button
                type="button"
                className={`kpi-card kpi-button ${tipo === '' ? 'kpi-selected' : ''}`}
                onClick={() => setTipo('')}
              >
                <span className="kpi-label">Total de alertas</span>
                <span className="kpi-value">{datos.resumen.total}</span>
                <span className="kpi-detail">{datos.resumen.estudiantesAfectados} estudiantes afectados</span>
              </button>
              <button
                type="button"
                className={`kpi-card kpi-button kpi-warn ${tipo === 'descenso' ? 'kpi-selected' : ''}`}
                onClick={() => setTipo('descenso')}
              >
                <span className="kpi-label">Descenso de rendimiento</span>
                <span className="kpi-value">{datos.resumen.descenso}</span>
                <span className="kpi-detail">Caída sostenida en las últimas evaluaciones</span>
              </button>
              <button
                type="button"
                className={`kpi-card kpi-button ${tipo === 'atraso' ? 'kpi-selected' : ''}`}
                onClick={() => setTipo('atraso')}
              >
                <span className="kpi-label">Atraso en entregas</span>
                <span className="kpi-value">{datos.resumen.atraso}</span>
                <span className="kpi-detail">Actividades vencidas sin entrega</span>
              </button>
              <button
                type="button"
                className={`kpi-card kpi-button kpi-warn ${tipo === 'baja_finalizacion' ? 'kpi-selected' : ''}`}
                onClick={() => setTipo('baja_finalizacion')}
              >
                <span className="kpi-label">Baja finalización</span>
                <span className="kpi-value">{datos.resumen.bajaFinalizacion}</span>
                <span className="kpi-detail">Porcentaje de entrega bajo el umbral</span>
              </button>
            </div>

            <div className="panel-box panel-box-spaced">
              <div className="panel-head">
                <h3 className="panel-title">Alertas del curso</h3>
                <span className="text-muted text-xs">
                  {cursoActivo?.nombre} · corte {fechaHora(datos.fechaCalculo)}
                </span>
              </div>

              <div className="product-toolbar-row toolbar-compact">
                <label className="mini-select-field">
                  <span>Tipo</span>
                  <select value={tipo} onChange={e => setTipo(e.target.value)}>
                    <option value="">Todos los tipos</option>
                    {Object.entries(TIPOS).map(([valor, etiqueta]) => (
                      <option key={valor} value={valor}>{etiqueta}</option>
                    ))}
                  </select>
                </label>
                <div className="toolbar-search-field selector-search-field">
                  <input
                    type="text"
                    className="input-text-clean input-sin-icono"
                    placeholder="Buscar estudiante o evidencia..."
                    value={busqueda}
                    onChange={e => setBusqueda(e.target.value)}
                    aria-label="Buscar alerta"
                  />
                </div>
              </div>

              {alertasFiltradas.length === 0 ? (
                <div className="state-empty-box">
                  <p className="text-muted">
                    No hay alertas que coincidan con los filtros seleccionados.
                  </p>
                </div>
              ) : (
                <ul className="alert-card-list">
                  {alertasFiltradas.map((alerta, i) => (
                    <li key={`${alerta.tipo}-${alerta.estudianteId}-${i}`} className="alert-card">
                      <div className="alert-card-head">
                        <div>
                          <div className="alert-card-title">
                            <span className={`state-chip ${alerta.severidad === 'alta' ? 'state-chip-danger' : 'state-chip-warn'}`}>
                              {alerta.tipoEtiqueta}
                            </span>
                            <strong>{alerta.nombre}</strong>
                            <span className="text-muted text-xs">{alerta.identificacion}</span>
                          </div>
                          <p className="alert-evidence-text">{alerta.evidencia}</p>
                        </div>
                        <Link
                          to={`${rutaFicha}/${datos.cursoId}/${alerta.estudianteId}`}
                          className="btn-pill btn-pill-ghost btn-sm btn-pill-link"
                        >
                          Ver ficha
                        </Link>
                      </div>
                      <div className="alert-card-foot">
                        <span className="text-muted text-xs">{alerta.regla}</span>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <div className="panel-box panel-box-spaced">
              <div className="panel-head">
                <h3 className="panel-title">Reglas de alerta aplicadas</h3>
                <span className="text-muted text-xs">
                  {esAdmin ? (
                    <Link to="/admin/reglas-alertas" className="text-link">Configurar reglas →</Link>
                  ) : (
                    'Configuradas por el Administrador Técnico'
                  )}
                </span>
              </div>
              <div className="table-scroll-container">
                <table className="product-data-table compact-table">
                  <thead>
                    <tr>
                      <th>Regla</th>
                      <th>Tipo</th>
                      <th>Parámetros</th>
                      <th>Estado</th>
                      <th>Última actualización</th>
                    </tr>
                  </thead>
                  <tbody>
                    {datos.reglas.map(regla => (
                      <tr key={regla.id}>
                        <td>{regla.nombre}</td>
                        <td>{TIPOS[regla.tipo] ?? regla.tipo}</td>
                        <td><EtiquetaParametros json={regla.parametrosJson} /></td>
                        <td>
                          <span className={`status-pill ${regla.activa ? 'status-pill-active' : 'status-pill-neutral'}`}>
                            {regla.activa ? 'Activa' : 'Inactiva'}
                          </span>
                        </td>
                        <td className="text-muted text-xs">{fechaHora(regla.fechaActualizacion)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </>
        )}
      </section>
    </div>
  )
}
