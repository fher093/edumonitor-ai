import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/http.js'
import { useAuth } from '../auth/useAuth.js'
import { ROLES } from '../auth/roles.js'
import useCursos from '../hooks/useCursos.js'
import SelectorCurso from '../components/SelectorCurso.jsx'
import { fechaHora, numero, porcentaje } from '../utils/formato.js'

function KpiCard({ etiqueta, valor, detalle, tono = 'neutral' }) {
  return (
    <div className={`kpi-card kpi-${tono}`}>
      <span className="kpi-label">{etiqueta}</span>
      <span className="kpi-value">{valor}</span>
      {detalle && <span className="kpi-detail">{detalle}</span>}
    </div>
  )
}

function EstadoCarga({ texto }) {
  return (
    <div className="state-empty-box">
      <p className="text-muted">{texto}</p>
    </div>
  )
}

export default function Dashboard() {
  const { usuario } = useAuth()
  const cursosHook = useCursos()
  const { cursoId, cursoActivo, cargando: cargandoCursos, error: errorCursos } = cursosHook

  const [dashboard, setDashboard] = useState(null)
  const [cargando, setCargando] = useState(false)
  const [error, setError] = useState('')

  const baseFicha = usuario?.rol === ROLES.ADMIN ? '/admin/ficha' : '/ficha'

  useEffect(() => {
    if (!cursoId) {
      setDashboard(null)
      return
    }
    let vivo = true
    setCargando(true)
    setError('')
    api(`/api/indicadores/curso/${cursoId}`)
      .then(data => {
        if (vivo) setDashboard(data)
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

  return (
    <div className="page-container">
      <section className="product-surface-card">
        <div className="section-head-split">
          <div>
            <div className="brand-dot-pill">
              <span className="dot-violet" />
              <span className="tag-uta-micro">ANALÍTICA DEL CURSO</span>
            </div>
            <h1 className="display-headline">Panel de Rendimiento Académico</h1>
            <p className="lead-subtitle">
              Indicadores calculados de forma determinista a partir de las actividades y
              calificaciones registradas. La nota final solo se muestra cuando ambos parciales están completos.
            </p>
          </div>
          {dashboard && (
            <div className="stat-pill-counter">
              <span className="stat-number">{numero(dashboard.promedioCurso)}</span>
              <span className="stat-label">promedio sobre {numero(dashboard.escalaCalificacion, 0)}</span>
            </div>
          )}
        </div>

        <div className="hairline-divider" />

        <SelectorCurso cursosHook={cursosHook} />

        {errorCursos && <div className="banner-alert banner-alert-error">{errorCursos}</div>}

        {!cargandoCursos && !cursoId && (
          <EstadoCarga texto="Aún no tienes asignaturas asignadas para consultar indicadores." />
        )}

        {cargando && <EstadoCarga texto="Calculando indicadores del curso..." />}
        {error && <div className="banner-alert banner-alert-error">{error}</div>}

        {!cargando && dashboard && (
          <>
            {dashboard.advertencias?.length > 0 && (
              <div className="banner-alert banner-alert-info">
                <strong>Nota sobre los datos:</strong>
                <ul className="inline-warning-list">
                  {dashboard.advertencias.map((a, i) => <li key={i}>{a}</li>)}
                </ul>
              </div>
            )}

            <div className="kpi-grid">
              <KpiCard
                etiqueta="Promedio del curso"
                valor={`${numero(dashboard.promedioCurso)} / ${numero(dashboard.escalaCalificacion, 0)}`}
                detalle={`${dashboard.estudiantesConNota} de ${dashboard.totalMatriculados} estudiantes con nota`}
                tono="violet"
              />
              <KpiCard
                etiqueta="Aprobación proyectada"
                valor={`${dashboard.aprobadosProyectados} estudiantes`}
                detalle="Nota final ≥ 14/20 con parciales completos"
                tono="ok"
              />
              <KpiCard
                etiqueta="Estudiantes en riesgo"
                valor={dashboard.estudiantesEnRiesgo}
                detalle="Con al menos una alerta temprana activa"
                tono="warn"
              />
              <KpiCard
                etiqueta="Actividades evaluadas"
                valor={`${dashboard.actividadesEvaluadas} / ${dashboard.actividadesTotales}`}
                detalle={`Desviación estándar: ${numero(dashboard.desviacionEstandar)}`}
              />
            </div>

            <div className="split-panels">
              <div className="panel-box">
                <div className="panel-head">
                  <h3 className="panel-title">Distribución del rendimiento</h3>
                  <span className="text-muted text-xs">Porcentaje de logro sobre la escala</span>
                </div>
                <div className="distribution-list">
                  {dashboard.distribucion.map(d => (
                    <div key={d.rango} className="distribution-row">
                      <span className="distribution-label">{d.etiqueta}</span>
                      <div className="distribution-track">
                        <div
                          className={`distribution-fill fill-${d.rango.replace('-', '_')}`}
                          style={{ width: `${Math.min(100, d.porcentaje)}%` }}
                        />
                      </div>
                      <span className="distribution-count">
                        {d.cantidad} <span className="text-muted">({porcentaje(d.porcentaje, 0)})</span>
                      </span>
                    </div>
                  ))}
                </div>

                <div className="progress-summary">
                  <div>
                    <span className="text-muted text-xs">Avance general</span>
                    <strong>{porcentaje(dashboard.progreso.porcentajeAvance)}</strong>
                  </div>
                  <div>
                    <span className="text-muted text-xs">Finalización de vencidas</span>
                    <strong>{porcentaje(dashboard.progreso.porcentajeFinalizacionVencidas)}</strong>
                  </div>
                  <div>
                    <span className="text-muted text-xs">Completadas</span>
                    <strong>{dashboard.progreso.completadas} / {dashboard.progreso.totalEsperado}</strong>
                  </div>
                </div>

                <div className="chip-row">
                  <span className="state-chip state-chip-ok">Calificadas: {dashboard.progreso.calificadas}</span>
                  <span className="state-chip state-chip-danger">Nota cero: {dashboard.progreso.notaCero}</span>
                  <span className="state-chip state-chip-warn">Sin entrega: {dashboard.progreso.sinEntrega}</span>
                  <span className="state-chip state-chip-neutral">Pendientes: {dashboard.progreso.pendientes}</span>
                </div>
              </div>

              <div className="panel-box">
                <div className="panel-head">
                  <h3 className="panel-title">Parciales</h3>
                  <span className="text-muted text-xs">Ponderación UTA 40 / 30 / 30</span>
                </div>
                <div className="parciales-grid">
                  {dashboard.parciales.map(p => (
                    <div key={p.parcial} className="parcial-card">
                      <div className="parcial-head">
                        <span className="parcial-num">Parcial {p.parcial}</span>
                        <span className={`status-pill ${p.completo ? 'status-pill-active' : 'status-pill-neutral'}`}>
                          {p.completo ? 'Completo' : 'Incompleto'}
                        </span>
                      </div>
                      <strong className="parcial-value">{numero(p.promedioSobre10)} / 10</strong>
                      <span className="text-muted text-xs">
                        {p.estudiantesConNota} con nota · {p.estudiantesIncompletos} incompletos
                      </span>
                    </div>
                  ))}
                </div>

                <div className="panel-head panel-head-spaced">
                  <h3 className="panel-title">Componentes evaluados</h3>
                </div>
                <div className="table-scroll-container">
                  <table className="product-data-table compact-table">
                    <thead>
                      <tr>
                        <th>Componente</th>
                        <th>Parcial</th>
                        <th>Peso</th>
                        <th style={{ textAlign: 'right' }}>Promedio</th>
                      </tr>
                    </thead>
                    <tbody>
                      {dashboard.componentes.map(c => (
                        <tr key={`${c.parcial}-${c.componente}`}>
                          <td>{c.etiquetaCorta}</td>
                          <td>P{c.parcial}</td>
                          <td>{Math.round(c.ponderacion * 100)} %</td>
                          <td style={{ textAlign: 'right' }}>
                            {numero(c.promedioSobre10)} / 10
                            <span className="text-muted text-xs"> ({c.calificadas}/{c.actividades})</span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            <div className="panel-box panel-box-spaced">
              <div className="panel-head">
                <h3 className="panel-title">Seguimiento por estudiante</h3>
                <span className="text-muted text-xs">
                  Corte: {fechaHora(dashboard.fechaCalculo)} · Curso {dashboard.cursoCodigo}
                </span>
              </div>

              <div className="table-scroll-container">
                <table className="product-data-table">
                  <thead>
                    <tr>
                      <th>Estudiante</th>
                      <th>Promedio</th>
                      <th>Parcial 1</th>
                      <th>Parcial 2</th>
                      <th>Nota final</th>
                      <th>Actividades</th>
                      <th>Alertas</th>
                      <th style={{ textAlign: 'right' }}>Ficha</th>
                    </tr>
                  </thead>
                  <tbody>
                    {dashboard.estudiantes.length === 0 ? (
                      <tr>
                        <td colSpan={8} className="cell-empty-message">
                          No hay estudiantes matriculados en este curso.
                        </td>
                      </tr>
                    ) : (
                      dashboard.estudiantes.map(est => (
                        <tr key={est.estudianteId}>
                          <td>
                            <div className="cell-student-name">{est.nombre}</div>
                            <div className="text-muted text-xs mono-id-badge-inline">{est.identificacion}</div>
                          </td>
                          <td>{numero(est.promedioActividades)}</td>
                          <td>{numero(est.notaParcial1)}</td>
                          <td>{numero(est.notaParcial2)}</td>
                          <td>
                            {est.notaFinal === null ? (
                              <span className="text-muted text-xs">Pendiente</span>
                            ) : (
                              <span className={est.notaFinal >= 14 ? 'nota-aprobada' : 'nota-reprobada'}>
                                {numero(est.notaFinal)}
                              </span>
                            )}
                          </td>
                          <td>
                            <div className="chip-row chip-row-tight">
                              {est.calificadas > 0 && <span className="mini-chip">{est.calificadas} calif.</span>}
                              {est.notaCero > 0 && <span className="mini-chip mini-chip-danger">{est.notaCero} cero</span>}
                              {est.sinEntrega > 0 && <span className="mini-chip mini-chip-warn">{est.sinEntrega} sin entrega</span>}
                              {est.pendientes > 0 && <span className="mini-chip">{est.pendientes} pend.</span>}
                              {est.completadasSinNota > 0 && <span className="mini-chip mini-chip-info">{est.completadasSinNota} compl.</span>}
                            </div>
                          </td>
                          <td>
                            {est.alertas > 0 ? (
                              <span className="state-chip state-chip-warn">{est.alertas}</span>
                            ) : (
                              <span className="text-muted text-xs">—</span>
                            )}
                          </td>
                          <td style={{ textAlign: 'right' }}>
                            <Link
                              to={`${baseFicha}/${dashboard.cursoId}/${est.estudianteId}`}
                              className="btn-pill btn-pill-ghost btn-sm btn-pill-link"
                            >
                              Ver ficha
                            </Link>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </>
        )}

        {cursoActivo && !cargando && !dashboard && !error && (
          <EstadoCarga texto="Selecciona una asignatura para consultar sus indicadores." />
        )}
      </section>
    </div>
  )
}
