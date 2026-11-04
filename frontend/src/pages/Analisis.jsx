import { useEffect, useState } from 'react'
import { api } from '../api/http.js'
import useCursos from '../hooks/useCursos.js'
import SelectorCurso from '../components/SelectorCurso.jsx'
import { fechaHora, numero, porcentaje } from '../utils/formato.js'

const ESTILO_TENDENCIA = {
  mejora: 'state-chip state-chip-ok',
  descenso: 'state-chip state-chip-danger',
  estable: 'state-chip state-chip-neutral',
  'datos insuficientes': 'state-chip state-chip-neutral',
}

export default function Analisis() {
  const cursosHook = useCursos()
  const { cursoId, cursoActivo, cargando: cargandoCursos } = cursosHook

  const [analisis, setAnalisis] = useState(null)
  const [cargando, setCargando] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!cursoId) {
      setAnalisis(null)
      return
    }
    let vivo = true
    setCargando(true)
    setError('')
    api(`/api/analisis/curso/${cursoId}`)
      .then(data => {
        if (vivo) setAnalisis(data)
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
              <span className="tag-uta-micro">ANÁLISIS GRUPAL</span>
            </div>
            <h1 className="display-headline">Diagnóstico por Actividad y Tendencias</h1>
            <p className="lead-subtitle">
              Detecta dificultades estructurales del grupo comparando el rendimiento por actividad,
              componente y evolución temporal del curso.
            </p>
          </div>
          {analisis && (
            <span className={ESTILO_TENDENCIA[analisis.tendenciaGeneral] ?? 'state-chip state-chip-neutral'}>
              Tendencia: {analisis.tendenciaGeneral}
            </span>
          )}
        </div>

        <div className="hairline-divider" />

        <SelectorCurso cursosHook={cursosHook} />

        {!cargandoCursos && !cursoId && (
          <div className="state-empty-box">
            <p className="text-muted">No hay asignaturas disponibles para analizar.</p>
          </div>
        )}

        {cargando && (
          <div className="state-empty-box">
            <p className="text-muted">Analizando el desempeño del grupo...</p>
          </div>
        )}
        {error && <div className="banner-alert banner-alert-error">{error}</div>}

        {!cargando && analisis && (
          <>
            {analisis.advertencias?.length > 0 && (
              <div className="banner-alert banner-alert-info">
                <ul className="inline-warning-list">
                  {analisis.advertencias.map((a, i) => <li key={i}>{a}</li>)}
                </ul>
              </div>
            )}

            <div className="kpi-grid">
              <div className="kpi-card kpi-violet">
                <span className="kpi-label">Promedio del curso</span>
                <span className="kpi-value">{numero(analisis.promedioCurso)} / {numero(analisis.escalaCalificacion, 0)}</span>
                <span className="kpi-detail">{analisis.totalEstudiantes} estudiantes</span>
              </div>
              <div className="kpi-card">
                <span className="kpi-label">Desviación estándar</span>
                <span className="kpi-value">{numero(analisis.desviacionEstandar)}</span>
                <span className="kpi-detail">Dispersión del rendimiento del grupo</span>
              </div>
              <div className="kpi-card kpi-warn">
                <span className="kpi-label">Actividades con bajo resultado</span>
                <span className="kpi-value">{analisis.actividadesBajas}</span>
                <span className="kpi-detail">Promedio inferior al 60 % de logro</span>
              </div>
              <div className="kpi-card">
                <span className="kpi-label">Estudiantes en riesgo</span>
                <span className="kpi-value">{analisis.estudiantesEnRiesgo}</span>
                <span className="kpi-detail">Con alertas activas</span>
              </div>
            </div>

            <div className="split-panels">
              <div className="panel-box">
                <div className="panel-head">
                  <h3 className="panel-title">Hallazgos del análisis</h3>
                  <span className="text-muted text-xs">Corte {fechaHora(analisis.fechaCalculo)}</span>
                </div>
                {analisis.hallazgos.length === 0 ? (
                  <p className="text-muted text-sm">No hay hallazgos con los datos actuales.</p>
                ) : (
                  <ul className="hallazgo-list">
                    {analisis.hallazgos.map((h, i) => (
                      <li key={i} className="hallazgo-item">
                        <span className="hallazgo-bullet" aria-hidden="true">◆</span>
                        <span>{h}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              <div className="panel-box">
                <div className="panel-head">
                  <h3 className="panel-title">Evolución por actividad</h3>
                  <span className="text-muted text-xs">
                    Variación general: {analisis.variacionTendencia === null ? '—' : `${numero(analisis.variacionTendencia)} pts`}
                  </span>
                </div>
                {analisis.tendencias.length === 0 ? (
                  <p className="text-muted text-sm">Sin actividades para graficar.</p>
                ) : (
                  <div className="evolution-list">
                    {analisis.tendencias.map(t => (
                      <div key={t.actividadId} className="evolution-row">
                        <span className="evolution-label" title={t.etiqueta}>{t.etiqueta}</span>
                        <div className="distribution-track">
                          <div
                            className={`distribution-fill ${(t.promedioPorcentaje ?? 0) < 60 ? 'fill-0_59' : (t.promedioPorcentaje ?? 0) < 80 ? 'fill-70_79' : 'fill-90_100'}`}
                            style={{ width: `${Math.min(100, t.promedioPorcentaje ?? 0)}%` }}
                          />
                        </div>
                        <span className="evolution-value">{porcentaje(t.promedioPorcentaje, 0)}</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>

            <div className="panel-box panel-box-spaced">
              <div className="panel-head">
                <h3 className="panel-title">Detalle por actividad</h3>
                <span className="text-muted text-xs">{cursoActivo?.nombre}</span>
              </div>
              <div className="table-scroll-container">
                <table className="product-data-table">
                  <thead>
                    <tr>
                      <th>Actividad</th>
                      <th>Componente</th>
                      <th>Parcial</th>
                      <th>Estudiantes</th>
                      <th>Calificados</th>
                      <th>Promedio</th>
                      <th>Logro</th>
                      <th>Finalización</th>
                      <th>Resultado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {analisis.detalleActividades.length === 0 ? (
                      <tr>
                        <td colSpan={9} className="cell-empty-message">
                          El curso no tiene actividades registradas.
                        </td>
                      </tr>
                    ) : (
                      analisis.detalleActividades.map(a => (
                        <tr key={a.actividadId}>
                          <td className="cell-student-name">{a.nombre}</td>
                          <td>{a.componenteEtiquetaCorta}</td>
                          <td>P{a.parcial}</td>
                          <td>{a.estudiantes}</td>
                          <td>{a.calificados}</td>
                          <td>{numero(a.promedioNota)} / {numero(a.puntajeMaximo)}</td>
                          <td>{porcentaje(a.promedioPorcentaje)}</td>
                          <td>{porcentaje(a.porcentajeFinalizacion)}</td>
                          <td>
                            <span className={`state-chip ${a.resultadoBajo ? 'state-chip-danger' : 'state-chip-ok'}`}>
                              {a.resultadoBajo ? 'Bajo el umbral' : 'Adecuado'}
                            </span>
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
      </section>
    </div>
  )
}
