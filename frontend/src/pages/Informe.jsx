import { createPortal } from 'react-dom'
import { useCursoActivo, useDatosCurso } from '../cursos/CursoContext.jsx'
import { Cargando, PageHeader, Vacio } from '../components/ui.jsx'
import { IconInforme } from '../components/Icons.jsx'
import { fechaHora, numero, porcentaje } from '../utils/formato.js'
import { SinCursos } from './Resumen.jsx'

const PRIORIDAD = { alta: 'Alta', media: 'Media' }

/**
 * Informe del período (HU-16 / RF-11). El backend compone el dashboard, el análisis grupal, las alertas
 * y las recomendaciones con una sola fecha de corte; aquí solo se presentan. "Descargar PDF" usa la
 * impresión del navegador sobre una copia del documento fuera del diseño de la aplicación.
 */
export default function Informe() {
  const { cursos, cargando: cargandoCursos } = useCursoActivo()
  const { datos, cargando, error } = useDatosCurso(id => `/api/informes/curso/${id}`)

  if (!cargandoCursos && cursos.length === 0) return <SinCursos />

  return (
    <div className="ws-page ws-page-scroll">
      <PageHeader
        eyebrow="Informe del período"
        titulo={datos ? `${datos.cursoNombre} (${datos.cursoCodigo})` : 'Informe de seguimiento'}
        descripcion="Métricas, hallazgos, alertas y recomendaciones con fecha de corte. Mismos cálculos que el resumen y las alertas."
      >
        <button type="button" className="ws-btn ws-btn-dark ws-btn-sm" onClick={() => window.print()} disabled={!datos}>
          Descargar PDF
        </button>
      </PageHeader>

      {error && <div className="banner-alert banner-alert-error">{error}</div>}
      {cargando && !datos && <Cargando texto="Preparando el informe..." />}
      {!cargando && !datos && !error && <Vacio titulo="Selecciona un curso" icono={<IconInforme size={22} />} />}

      {datos && (
        <>
          <article className="inf-doc">
            <DocumentoInforme d={datos} />
          </article>
          {createPortal(
            <div className="inf-print-root">
              <article className="inf-doc">
                <DocumentoInforme d={datos} />
              </article>
            </div>,
            document.body
          )}
        </>
      )}
    </div>
  )
}

function DocumentoInforme({ d }) {
  const m = d.metricas
  const a = d.analisis
  const bajas = a.detalleActividades.filter(x => x.resultadoBajo)
  const advertencias = [...new Set([...m.advertencias, ...a.advertencias, ...d.alertas.advertencias, ...d.recomendaciones.advertencias])]

  return (
    <>
      <header className="inf-head">
        <div>
          <span className="inf-eyebrow">EduMonitor AI · Universidad Técnica de Ambato</span>
          <h2>Informe de seguimiento académico</h2>
          <p className="inf-curso">
            {d.cursoNombre} ({d.cursoCodigo}){d.paralelo ? ` · Paralelo ${d.paralelo}` : ''} · Período {d.periodo}
          </p>
        </div>
        <dl className="inf-meta">
          <div>
            <dt>Docente</dt>
            <dd>{d.docenteNombre}</dd>
          </div>
          <div>
            <dt>Fecha de corte</dt>
            <dd>{fechaHora(d.fechaCorte)}</dd>
          </div>
          <div>
            <dt>Fuente de los datos</dt>
            <dd>
              {d.fuenteDatos}
              {d.fechaSincronizacion ? ` · sincronizado ${fechaHora(d.fechaSincronizacion)}` : ''}
            </dd>
          </div>
        </dl>
      </header>

      <section className="inf-section">
        <h3>1. Métricas</h3>
        <div className="inf-kpis">
          <Metrica etiqueta="Promedio del curso" valor={m.promedioCurso == null ? 'Datos insuficientes' : `${numero(m.promedioCurso)} / ${numero(m.escalaCalificacion, 0)}`} detalle={m.desviacionEstandar == null ? null : `σ ${numero(m.desviacionEstandar)}`} />
          <Metrica etiqueta="Aprobados proyectados" valor={`${m.aprobadosProyectados} de ${m.totalMatriculados}`} detalle="nota final ≥ 14/20" />
          <Metrica etiqueta="Requieren atención" valor={m.estudiantesEnRiesgo} detalle="con al menos una alerta" />
          <Metrica etiqueta="Actividades evaluadas" valor={`${m.actividadesEvaluadas} de ${m.actividadesTotales}`} />
          <Metrica etiqueta="Finalización (vencidas)" valor={porcentaje(m.progreso.porcentajeFinalizacionVencidas)} detalle={`${m.progreso.sinEntrega} entregas sin realizar`} />
        </div>
        <table className="inf-table">
          <thead>
            <tr>
              <th>Rango de promedio</th>
              <th className="num">Estudiantes</th>
              <th className="num">%</th>
            </tr>
          </thead>
          <tbody>
            {m.distribucion.map(x => (
              <tr key={x.rango}>
                <td>{x.etiqueta}</td>
                <td className="num">{x.cantidad}</td>
                <td className="num">{porcentaje(x.porcentaje)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <section className="inf-section">
        <h3>2. Hallazgos</h3>
        {a.hallazgos.length === 0 ? (
          <p className="inf-muted">Datos insuficientes para extraer hallazgos.</p>
        ) : (
          <ul>
            {a.hallazgos.map((h, i) => (
              <li key={i}>{h}</li>
            ))}
          </ul>
        )}
        {bajas.length > 0 && (
          <table className="inf-table">
            <thead>
              <tr>
                <th>Actividad con logro bajo el 60 %</th>
                <th>Parcial</th>
                <th className="num">Logro</th>
                <th className="num">Calificados</th>
              </tr>
            </thead>
            <tbody>
              {bajas.map(x => (
                <tr key={x.actividadId}>
                  <td>{x.nombre}</td>
                  <td>{x.parcial}</td>
                  <td className="num">{porcentaje(x.promedioPorcentaje)}</td>
                  <td className="num">{x.calificados}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="inf-section">
        <h3>3. Alertas (señales de seguimiento)</h3>
        {d.alertas.alertas.length === 0 ? (
          <p className="inf-muted">Ningún estudiante activa las reglas de alerta configuradas.</p>
        ) : (
          <table className="inf-table">
            <thead>
              <tr>
                <th>Estudiante</th>
                <th>Regla</th>
                <th>Evidencia</th>
              </tr>
            </thead>
            <tbody>
              {d.alertas.alertas.map((x, i) => (
                <tr key={i}>
                  <td className="nowrap">{x.nombre}</td>
                  <td>{x.regla}</td>
                  <td>{x.evidencia}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="inf-section">
        <h3>4. Recomendaciones</h3>
        {d.recomendaciones.recomendaciones.length === 0 ? (
          <p className="inf-muted">Sin recomendaciones: los indicadores del curso no muestran situaciones que requieran acción.</p>
        ) : (
          <table className="inf-table">
            <thead>
              <tr>
                <th>Prioridad</th>
                <th>Acción sugerida</th>
                <th>Justificación</th>
              </tr>
            </thead>
            <tbody>
              {d.recomendaciones.recomendaciones.map((x, i) => (
                <tr key={i}>
                  <td>
                    <span className={`inf-prioridad inf-prioridad-${x.prioridad}`}>{PRIORIDAD[x.prioridad] ?? x.prioridad}</span>
                  </td>
                  <td>{x.accion}</td>
                  <td>
                    {x.justificacion} <span className="inf-indicador">{x.indicador}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {advertencias.length > 0 && (
        <section className="inf-section">
          <h3>Advertencias sobre los datos</h3>
          <ul>
            {advertencias.map((x, i) => (
              <li key={i}>{x}</li>
            ))}
          </ul>
        </section>
      )}

      <footer className="inf-foot">
        Generado por EduMonitor AI el {fechaHora(d.fechaCorte)}. Las cifras se calculan de forma determinista a partir de los
        datos sincronizados; las alertas son señales de seguimiento, no diagnósticos. No sustituye el criterio del docente.
      </footer>
    </>
  )
}

function Metrica({ etiqueta, valor, detalle }) {
  return (
    <div className="inf-kpi">
      <span>{etiqueta}</span>
      <strong>{valor}</strong>
      {detalle && <small>{detalle}</small>}
    </div>
  )
}
