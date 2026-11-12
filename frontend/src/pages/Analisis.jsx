import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useCursoActivo, useDatosCurso } from '../cursos/CursoContext.jsx'
import { Aviso, Barra, Cargando, Kpi, Modal, PageHeader, Panel, Vacio, VerMas } from '../components/ui.jsx'
import { IconBarras, IconCampana, IconCapas, IconDiana, IconFlecha, IconFoco, IconTendencia } from '../components/Icons.jsx'
import { fecha, fechaHora, numero, porcentaje, siglaComponente } from '../utils/formato.js'
import { SinCursos } from './Resumen.jsx'

const TONO_TENDENCIA = { mejora: 'ok', descenso: 'danger', estable: 'neutral' }
const UMBRAL = 60
const HALLAZGOS_VISIBLES = 3

function tono(p) {
  if (p === null || p === undefined) return 'none'
  return p < UMBRAL ? 'danger' : p < 70 ? 'warn' : 'ok'
}

/**
 * Análisis grupal (HU-11): logro por actividad frente al umbral del 60 %, tendencia del curso,
 * hallazgos y componentes. Todo visible en una pantalla; el detalle de cada actividad va en un modal.
 */
export default function Analisis() {
  const { cursos, cargando: cargandoCursos } = useCursoActivo()
  const { datos: a, cargando, error } = useDatosCurso(id => `/api/analisis/curso/${id}`)
  const [detalle, setDetalle] = useState(null)

  if (!cargandoCursos && cursos.length === 0) return <SinCursos />
  if (error) return <div className="ws-page"><div className="banner-alert banner-alert-error">{error}</div></div>
  if (!a || (cargando && !a)) return <div className="ws-page"><Cargando texto="Analizando el desempeño del grupo..." /></div>

  const actividades = a.detalleActividades

  return (
    <div className="ws-page">
      <PageHeader
        titulo="¿Dónde se le complica el curso al grupo?"
        datos={[
          { etiqueta: 'Estudiantes', valor: a.totalEstudiantes },
          { etiqueta: 'Actividades evaluadas', valor: a.actividades },
          { etiqueta: 'Umbral de logro', valor: '60 %' },
          { etiqueta: 'Corte', valor: fechaHora(a.fechaCalculo) },
        ]}
      >
        <Aviso titulo="Datos incompletos" mensajes={a.advertencias} />
        <span className={`ws-pill ws-pill-${TONO_TENDENCIA[a.tendenciaGeneral] ?? 'neutral'}`}>
          Tendencia: {a.tendenciaGeneral}
          {a.variacionTendencia !== null && ` (${a.variacionTendencia > 0 ? '+' : ''}${numero(a.variacionTendencia, 1)} pts)`}
        </span>
      </PageHeader>

      <div className="ws-kpis ws-kpis-4">
        <Kpi
          etiqueta="Promedio del curso"
          icono={<IconBarras size={14} />}
          valor={<>{numero(a.promedioCurso)}<small>/{numero(a.escalaCalificacion, 0)}</small></>}
          detalle={`σ ${numero(a.desviacionEstandar)} · ${a.totalEstudiantes} estudiantes`}
          tono="brand"
        />
        <Kpi etiqueta="Actividades bajo el umbral" icono={<IconDiana size={14} />} valor={`${a.actividadesBajas}/${a.actividades}`} detalle="Logro menor al 60 %" tono="danger" />
        <Kpi etiqueta="Estudiantes con alertas" icono={<IconCampana size={14} />} valor={a.estudiantesEnRiesgo} detalle="Requieren seguimiento" tono="warn" />
        <Kpi
          etiqueta="Variación del grupo"
          icono={<IconTendencia size={14} />}
          valor={a.variacionTendencia === null ? '—' : `${a.variacionTendencia > 0 ? '+' : ''}${numero(a.variacionTendencia, 1)}`}
          detalle="Puntos de logro, primeras vs. últimas"
        />
      </div>

      <div className="ws-grid ws-grid-analisis">
        <Panel titulo="Logro por actividad" meta="Clic en una columna para ver el detalle · línea = 60 %" icono={<IconBarras size={16} />}>
          {actividades.length === 0 ? (
            <Vacio titulo="Sin actividades evaluadas" />
          ) : (
            <div className="ws-columns" role="list">
              {actividades.map(act => (
                <button
                  key={act.actividadId}
                  type="button"
                  role="listitem"
                  className="ws-column"
                  onClick={() => setDetalle(act)}
                  title={`${act.nombre}: ${porcentaje(act.promedioPorcentaje, 0)}`}
                >
                  <span className="ws-column-value">{act.promedioPorcentaje === null ? '—' : numero(act.promedioPorcentaje, 0)}</span>
                  <span className="ws-column-track">
                    <span className={`ws-column-fill ws-fill-${tono(act.promedioPorcentaje)}`} style={{ height: `${Math.min(100, act.promedioPorcentaje ?? 0)}%` }} />
                  </span>
                  <span className="ws-column-label">
                    P{act.parcial}·{siglaComponente(act.componente)}
                  </span>
                </button>
              ))}
            </div>
          )}
        </Panel>

        <div className="ws-stack">
          <Panel
            titulo="Hallazgos"
            meta="Descriptivos, calculados sin IA"
            icono={<IconFoco size={16} />}
            expandible={a.hallazgos.length > HALLAZGOS_VISIBLES}
          >
            {(abierto, abrir) =>
              a.hallazgos.length === 0 ? (
                <p className="ws-muted ws-small">No hay hallazgos con los datos actuales.</p>
              ) : (
                <>
                  <ul className="ws-findings">
                    {(abierto ? a.hallazgos : a.hallazgos.slice(0, HALLAZGOS_VISIBLES)).map((h, i) => (
                      <li key={i}>{h}</li>
                    ))}
                  </ul>
                  {!abierto && <VerMas restantes={a.hallazgos.length - HALLAZGOS_VISIBLES} onClick={abrir} />}
                </>
              )
            }
          </Panel>
          <Panel
            titulo="Componentes UTA"
            meta="Promedio sobre 10 · cerrado: los 3 más bajos"
            icono={<IconCapas size={16} />}
            expandible
          >
            {abierto =>
              abierto ? (
                [1, 2].map(p => (
                  <div key={p} className="ws-comp-block">
                    <span className="ws-eyebrow">Parcial {p}</span>
                    {a.componentes
                      .filter(c => c.parcial === p)
                      .map(c => (
                        <FilaComponente key={c.componente} c={c} />
                      ))}
                  </div>
                ))
              ) : (
                <div className="ws-comp-block">
                  {a.componentes
                    .filter(c => c.promedioSobre10 !== null)
                    .sort((x, y) => x.promedioSobre10 - y.promedioSobre10)
                    .slice(0, 3)
                    .map(c => (
                      <FilaComponente key={`${c.parcial}-${c.componente}`} c={c} conParcial />
                    ))}
                </div>
              )
            }
          </Panel>
        </div>
      </div>

      <Modal
        abierto={!!detalle}
        onCerrar={() => setDetalle(null)}
        eyebrow={detalle ? `Parcial ${detalle.parcial} · ${detalle.componenteEtiquetaCorta}` : ''}
        titulo={detalle?.nombre ?? ''}
        pie={
          detalle && (
            <Link to={`/actividades?id=${detalle.actividadId}`} className="ws-btn ws-btn-dark">
              Ver notas de la actividad <IconFlecha size={14} />
            </Link>
          )
        }
      >
        {detalle && (
          <>
            <div className="ws-mini-kpis">
              <div className={`ws-mini-kpi ws-tone-${tono(detalle.promedioPorcentaje)}`}>
                <span className="ws-eyebrow">Logro promedio</span>
                <strong>{porcentaje(detalle.promedioPorcentaje, 0)}</strong>
              </div>
              <div className="ws-mini-kpi">
                <span className="ws-eyebrow">Nota promedio</span>
                <strong>
                  {numero(detalle.promedioNota)}
                  <small>/{numero(detalle.puntajeMaximo, 0)}</small>
                </strong>
              </div>
              <div className="ws-mini-kpi">
                <span className="ws-eyebrow">Calificados</span>
                <strong>
                  {detalle.calificados}/{detalle.estudiantes}
                </strong>
              </div>
              <div className="ws-mini-kpi">
                <span className="ws-eyebrow">Finalización</span>
                <strong>{porcentaje(detalle.porcentajeFinalizacion, 0)}</strong>
              </div>
            </div>
            <p className="ws-muted ws-small">
              Entrega: {fecha(detalle.fechaEntrega)}.{' '}
              {detalle.resultadoBajo
                ? 'El grupo no alcanzó el 60 % de logro: conviene reforzar este tema.'
                : 'El grupo supera el umbral del 60 % de logro.'}
            </p>
          </>
        )}
      </Modal>
    </div>
  )
}

function FilaComponente({ c, conParcial = false }) {
  return (
    <div className="ws-comp-row">
      <span className="ws-comp-name">
        {conParcial && <span className="ws-muted">P{c.parcial} · </span>}
        {c.etiquetaCorta} <small>{numero(c.ponderacion * 100, 0)} %</small>
      </span>
      <Barra porcentaje={(c.promedioSobre10 ?? 0) * 10} tono={tono(c.promedioSobre10 === null ? null : c.promedioSobre10 * 10)} />
      <span className="ws-num">{numero(c.promedioSobre10)}</span>
    </div>
  )
}
