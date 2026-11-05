import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/http.js'
import { useAuth } from '../auth/useAuth.js'
import { ROLES } from '../auth/roles.js'
import {
  claseEstado,
  fecha,
  fechaHora,
  numero,
  porcentaje,
} from '../utils/formato.js'

export default function FichaEstudiante() {
  const { cursoId, estudianteId } = useParams()
  const { usuario } = useAuth()
  const [ficha, setFicha] = useState(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  const esAdmin = usuario?.rol === ROLES.ADMIN
  const rutaDashboard = esAdmin ? '/admin/dashboard' : '/dashboard'

  useEffect(() => {
    let vivo = true
    setCargando(true)
    setError('')
    api(`/api/indicadores/curso/${cursoId}/estudiantes/${estudianteId}`)
      .then(data => {
        if (vivo) setFicha(data)
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
  }, [cursoId, estudianteId])

  return (
    <div className="page-container">
      <section className="product-surface-card">
        <div className="section-head-split">
          <div>
            <div className="brand-dot-pill">
              <span className="dot-violet" />
              <span className="tag-uta-micro">SEGUIMIENTO INDIVIDUAL</span>
            </div>
            <h1 className="display-headline">
              {ficha ? ficha.nombre : 'Ficha del estudiante'}
            </h1>
            <p className="lead-subtitle">
              {ficha
                ? `${ficha.cursoNombre} (${ficha.cursoCodigo}) · ${ficha.periodo} · ${ficha.identificacion}`
                : 'Trazabilidad académica del estudiante en el curso seleccionado.'}
            </p>
          </div>
          <Link to={rutaDashboard} className="btn-pill btn-pill-ghost btn-pill-link">
            ← Volver al dashboard
          </Link>
        </div>

        <div className="hairline-divider" />

        {cargando && (
          <div className="state-empty-box">
            <p className="text-muted">Cargando la ficha del estudiante...</p>
          </div>
        )}
        {error && <div className="banner-alert banner-alert-error">{error}</div>}

        {!cargando && ficha && (
          <>
            <div className="kpi-grid">
              <div className="kpi-card kpi-violet">
                <span className="kpi-label">Promedio de actividades</span>
                <span className="kpi-value">
                  {numero(ficha.promedioActividades)} / {numero(ficha.escalaCalificacion, 0)}
                </span>
                <span className="kpi-detail">{porcentaje(ficha.porcentajeFinalizacion)} de finalización</span>
              </div>
              <div className="kpi-card">
                <span className="kpi-label">Parcial 1</span>
                <span className="kpi-value">{numero(ficha.notaParcial1)} / 10</span>
                <span className="kpi-detail">{ficha.parcial1Completo ? 'Componentes completos' : 'Componentes incompletos'}</span>
              </div>
              <div className="kpi-card">
                <span className="kpi-label">Parcial 2</span>
                <span className="kpi-value">{numero(ficha.notaParcial2)} / 10</span>
                <span className="kpi-detail">{ficha.parcial2Completo ? 'Componentes completos' : 'Componentes incompletos'}</span>
              </div>
              <div className={`kpi-card ${ficha.notaFinal === null ? '' : ficha.notaFinal >= 14 ? 'kpi-ok' : 'kpi-warn'}`}>
                <span className="kpi-label">Nota final</span>
                <span className="kpi-value">
                  {ficha.notaFinal === null ? 'Pendiente' : `${numero(ficha.notaFinal)} / 20`}
                </span>
                <span className="kpi-detail">
                  {ficha.notaFinal === null ? 'Requiere ambos parciales completos' : 'Escala sobre 20 puntos'}
                </span>
              </div>
            </div>

            <div className="split-panels">
              <div className="panel-box">
                <div className="panel-head">
                  <h3 className="panel-title">Componentes por parcial</h3>
                  <span className="text-muted text-xs">Ponderación 40 / 30 / 30</span>
                </div>
                {[1, 2].map(parcial => (
                  <div key={parcial} className="componente-parcial-block">
                    <div className="componente-parcial-head">
                      <span className="parcial-num">Parcial {parcial}</span>
                      <strong>{numero(parcial === 1 ? ficha.notaParcial1 : ficha.notaParcial2)} / 10</strong>
                    </div>
                    <div className="componente-list">
                      {ficha.componentes.filter(c => c.parcial === parcial).map(c => (
                        <div key={c.componente} className="componente-row">
                          <div className="componente-row-head">
                            <span>{c.etiquetaCorta}</span>
                            <span className="text-muted text-xs">{Math.round(c.ponderacion * 100)} %</span>
                          </div>
                          <div className="distribution-track">
                            <div
                              className="distribution-fill fill-80_89"
                              style={{ width: `${Math.min(100, (c.promedioSobre10 ?? 0) * 10)}%` }}
                            />
                          </div>
                          <span className="text-xs">
                            {c.tieneDatos ? `${numero(c.promedioSobre10)} / 10` : 'Sin datos'} · {c.actividadesCalificadas} calificadas
                          </span>
                        </div>
                      ))}
                    </div>
                  </div>
                ))}
              </div>

              <div className="panel-box">
                <div className="panel-head">
                  <h3 className="panel-title">Evolución de notas</h3>
                  <span className="text-muted text-xs">Actividades con nota registrada</span>
                </div>
                {ficha.evolucion.length === 0 ? (
                  <p className="text-muted text-sm">Aún no hay notas registradas para este estudiante.</p>
                ) : (
                  <div className="evolution-list">
                    {ficha.evolucion.map(item => (
                      <div key={item.actividadId} className="evolution-row">
                        <span className="evolution-label" title={item.etiqueta}>{item.etiqueta}</span>
                        <div className="distribution-track">
                          <div
                            className={`distribution-fill ${Number(item.notaSobre10) < 6 ? 'fill-0_59' : Number(item.notaSobre10) < 8 ? 'fill-70_79' : 'fill-90_100'}`}
                            style={{ width: `${Math.min(100, (Number(item.notaSobre10) || 0) * 10)}%` }}
                          />
                        </div>
                        <span className="evolution-value">{numero(item.notaSobre10)}</span>
                      </div>
                    ))}
                  </div>
                )}

                <div className="panel-head panel-head-spaced">
                  <h3 className="panel-title">Resumen de actividades</h3>
                </div>
                <div className="chip-row">
                  <span className="state-chip state-chip-ok">Calificadas: {ficha.calificadas}</span>
                  <span className="state-chip state-chip-danger">Nota cero: {ficha.notaCero}</span>
                  <span className="state-chip state-chip-warn">Sin entrega: {ficha.sinEntrega}</span>
                  <span className="state-chip state-chip-neutral">Pendientes: {ficha.pendientes}</span>
                  <span className="state-chip state-chip-info">Completadas sin nota: {ficha.completadas}</span>
                </div>
              </div>
            </div>

            <div className="panel-box panel-box-spaced">
              <div className="panel-head">
                <h3 className="panel-title">Actividades evaluables</h3>
                <span className="text-muted text-xs">
                  {ficha.actividadesTotales} actividades · corte {fechaHora(new Date())}
                </span>
              </div>
              <div className="table-scroll-container">
                <table className="product-data-table">
                  <thead>
                    <tr>
                      <th>Actividad</th>
                      <th>Componente</th>
                      <th>Parcial</th>
                      <th>Entrega</th>
                      <th>Nota</th>
                      <th>Logro</th>
                      <th>Estado</th>
                      <th>Observación</th>
                    </tr>
                  </thead>
                  <tbody>
                    {ficha.actividades.length === 0 ? (
                      <tr>
                        <td colSpan={8} className="cell-empty-message">
                          El curso todavía no tiene actividades registradas.
                        </td>
                      </tr>
                    ) : (
                      ficha.actividades.map(a => (
                        <tr key={a.actividadId}>
                          <td>
                            <div className="cell-student-name">{a.nombre}</div>
                            <div className="text-muted text-xs">{a.tipo}</div>
                          </td>
                          <td>{a.componenteEtiquetaCorta}</td>
                          <td>P{a.parcial}</td>
                          <td>{fecha(a.fechaEntrega)}</td>
                          <td>
                            {a.nota === null ? '—' : `${numero(a.nota)} / ${numero(a.puntajeMaximo)}`}
                          </td>
                          <td>{porcentaje(a.porcentaje)}</td>
                          <td>
                            <span className={claseEstado(a.estado)}>{a.estadoEtiqueta}</span>
                          </td>
                          <td className="text-muted text-xs">{a.observacion || '—'}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="panel-box panel-box-spaced">
              <div className="panel-head">
                <h3 className="panel-title">Alertas de seguimiento</h3>
                <span className="text-muted text-xs">
                  Señales deterministas basadas en reglas; no constituyen un diagnóstico definitivo.
                </span>
              </div>
              {ficha.alertas.length === 0 ? (
                <p className="text-muted text-sm">
                  No se activaron reglas de alerta para este estudiante con los datos actuales.
                </p>
              ) : (
                <ul className="alert-evidence-list">
                  {ficha.alertas.map((alerta, i) => (
                    <li key={`${alerta.tipo}-${i}`} className="alert-evidence-item">
                      <div className="alert-evidence-head">
                        <span className={`state-chip ${alerta.severidad === 'alta' ? 'state-chip-danger' : 'state-chip-warn'}`}>
                          {alerta.tipoEtiqueta}
                        </span>
                        <span className="text-muted text-xs">{alerta.regla}</span>
                      </div>
                      <p className="alert-evidence-text">{alerta.evidencia}</p>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </>
        )}
      </section>
    </div>
  )
}
