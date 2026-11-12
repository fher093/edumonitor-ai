import { Link, useNavigate } from 'react-router-dom'
import { useCursoActivo, useDatosCurso } from '../cursos/CursoContext.jsx'
import { Avatar, Aviso, Barra, Cargando, Kpi, PageHeader, Panel, Vacio, VerMas } from '../components/ui.jsx'
import {
  IconAdvertencia,
  IconBandera,
  IconBarras,
  IconCampana,
  IconCheckCirculo,
  IconDiana,
  IconFlecha,
  IconOjo,
} from '../components/Icons.jsx'
import { fechaHora, numero, porcentaje, siglaComponente, TIPO_ALERTA_CORTO } from '../utils/formato.js'

const TONO_RANGO = ['danger', 'warn', 'ok', 'ok', 'ok']
const TONO_TENDENCIA = { mejora: 'ok', descenso: 'danger', estable: 'neutral' }
/** Elementos visibles con el panel cerrado; el resto se despliega con "+". */
const VISIBLES = 4

/**
 * Resumen del curso (HU-07): indicadores, distribución, parciales, a quién atender y qué
 * actividades reforzar, en una sola pantalla. Cada panel muestra lo esencial y se despliega con "+".
 */
export default function Resumen() {
  const { cursoActivo, cargando: cargandoCursos, cursos } = useCursoActivo()
  const dashboard = useDatosCurso(id => `/api/indicadores/curso/${id}`)
  const alertas = useDatosCurso(id => `/api/alertas/curso/${id}`)
  const analisis = useDatosCurso(id => `/api/analisis/curso/${id}`)
  const navigate = useNavigate()

  if (!cargandoCursos && cursos.length === 0) return <SinCursos />
  if (dashboard.error) return <div className="ws-page"><div className="banner-alert banner-alert-error">{dashboard.error}</div></div>
  const d = dashboard.datos
  if (!d) return <div className="ws-page"><Cargando texto="Calculando indicadores del curso..." /></div>

  const atencion = (alertas.datos?.alertas ?? [])
    .slice()
    .sort((a, b) => (a.severidad === 'alta' ? -1 : 1) - (b.severidad === 'alta' ? -1 : 1))
  const bajas = (analisis.datos?.detalleActividades ?? [])
    .filter(a => a.resultadoBajo)
    .sort((a, b) => (a.promedioPorcentaje ?? 0) - (b.promedioPorcentaje ?? 0))
  const tendencia = analisis.datos?.tendenciaGeneral

  return (
    <div className="ws-page">
      <PageHeader
        titulo={cursoActivo?.nombre ?? d.cursoNombre}
        datos={[
          { etiqueta: 'Estudiantes', valor: d.totalMatriculados },
          { etiqueta: 'Actividades evaluadas', valor: `${d.actividadesEvaluadas} de ${d.actividadesTotales}` },
          { etiqueta: 'Escala', valor: `sobre ${numero(d.escalaCalificacion, 0)}` },
          { etiqueta: 'Corte', valor: fechaHora(d.fechaCalculo) },
        ]}
      >
        <Aviso titulo="Datos incompletos" mensajes={d.advertencias} />
        {tendencia && (
          <span className={`ws-pill ws-pill-${TONO_TENDENCIA[tendencia] ?? 'neutral'}`}>Tendencia: {tendencia}</span>
        )}
        <Link to="/estudiantes" className="ws-btn ws-btn-dark">
          Ver estudiantes <IconFlecha size={14} />
        </Link>
      </PageHeader>

      <div className="ws-kpis">
        <Kpi
          etiqueta="Promedio del curso"
          icono={<IconBarras size={14} />}
          valor={<>{numero(d.promedioCurso)}<small>/{numero(d.escalaCalificacion, 0)}</small></>}
          detalle={`σ ${numero(d.desviacionEstandar)} · ${d.estudiantesConNota} con nota`}
        />
        <Kpi etiqueta="Aprobados proyectados" icono={<IconCheckCirculo size={14} />} valor={d.aprobadosProyectados} detalle="Nota final ≥ 14/20" tono="ok" />
        <Kpi etiqueta="En riesgo" icono={<IconAdvertencia size={14} />} valor={d.estudiantesEnRiesgo} detalle="Nota final < 14/20" tono="danger" />
        <Kpi
          etiqueta="Alertas activas"
          icono={<IconCampana size={14} />}
          valor={alertas.datos?.resumen.total ?? '—'}
          detalle={alertas.datos ? `${alertas.datos.resumen.estudiantesAfectados} estudiantes` : ''}
          tono="warn"
        />
        <Kpi
          etiqueta="Finalización"
          icono={<IconBandera size={14} />}
          valor={porcentaje(d.progreso.porcentajeFinalizacionVencidas, 0)}
          detalle={`${d.progreso.completadas}/${d.progreso.totalEsperado} entregas`}
        />
      </div>

      <div className="ws-grid ws-grid-3">
        <Panel titulo="Rendimiento" meta="Distribución por % de logro" icono={<IconBarras size={16} />} expandible>
          {abierto => (
            <>
              <ul className="ws-dist">
                {d.distribucion.map((r, i) => (
                  <li key={r.rango}>
                    <span className="ws-dist-label" title={r.etiqueta}>{r.rango} %</span>
                    <Barra porcentaje={r.porcentaje} tono={TONO_RANGO[i] ?? 'ok'} />
                    <span className="ws-dist-value">{r.cantidad}</span>
                  </li>
                ))}
              </ul>
              {abierto && (
                <div className="ws-reveal">
                  <div className="ws-divider" />
                  <div className="ws-parciales">
                    {d.parciales.map(p => (
                      <div key={p.parcial} className="ws-parcial">
                        <span className="ws-eyebrow">Parcial {p.parcial}</span>
                        <strong>
                          {numero(p.promedioSobre10)}
                          <small>/10</small>
                        </strong>
                        <span className="ws-muted ws-small">
                          {p.completo ? 'Componentes completos' : `${p.estudiantesIncompletos} incompletos`}
                        </span>
                      </div>
                    ))}
                  </div>
                  <div className="ws-divider" />
                  <div className="ws-states">
                    <span className="state-chip state-chip-ok">{d.progreso.calificadas} calificadas</span>
                    <span className="state-chip state-chip-danger">{d.progreso.notaCero} nota cero</span>
                    <span className="state-chip state-chip-warn">{d.progreso.sinEntrega} sin entrega</span>
                    <span className="state-chip state-chip-neutral">{d.progreso.pendientes} pendientes</span>
                  </div>
                </div>
              )}
            </>
          )}
        </Panel>

        <Panel
          titulo="Requieren atención"
          meta="Señales de seguimiento, no diagnósticos"
          icono={<IconOjo size={16} />}
          acciones={<Link to="/alertas" className="ws-link">Ver todas</Link>}
          expandible={atencion.length > VISIBLES}
          cuerpoClassName="ws-flush"
        >
          {(abierto, abrir) =>
            alertas.cargando ? (
              <Cargando />
            ) : atencion.length === 0 ? (
              <Vacio titulo="Sin alertas">Ningún estudiante activa las reglas configuradas.</Vacio>
            ) : (
              <>
                <ul className="ws-list">
                  {(abierto ? atencion : atencion.slice(0, VISIBLES)).map((a, i) => (
                    <li key={`${a.estudianteId}-${a.tipo}-${i}`}>
                      <button type="button" className="ws-list-row" onClick={() => navigate(`/estudiantes?id=${a.estudianteId}`)}>
                        <Avatar nombre={a.nombre} estudianteId={a.estudianteId} tamano={30} />
                        <span className="ws-list-main">
                          <strong>{a.nombre}</strong>
                          <span className="ws-list-sub">{a.evidencia}</span>
                        </span>
                        <span className={`ws-pill ws-pill-${a.severidad === 'alta' ? 'danger' : 'warn'}`} title={a.tipoEtiqueta}>
                          {TIPO_ALERTA_CORTO[a.tipo] ?? a.tipoEtiqueta}
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
                {!abierto && <VerMas restantes={atencion.length - VISIBLES} onClick={abrir} />}
              </>
            )
          }
        </Panel>

        <Panel
          titulo="Actividades a reforzar"
          meta="Logro promedio menor al 60 %"
          icono={<IconDiana size={16} />}
          acciones={<Link to="/analisis" className="ws-link">Análisis</Link>}
          expandible={bajas.length > VISIBLES}
          cuerpoClassName="ws-flush"
        >
          {(abierto, abrir) =>
            analisis.cargando ? (
              <Cargando />
            ) : bajas.length === 0 ? (
              <Vacio titulo="Sin actividades bajo el umbral">Todas las actividades evaluadas superan el 60 % de logro.</Vacio>
            ) : (
              <>
                <ul className="ws-list">
                  {(abierto ? bajas : bajas.slice(0, VISIBLES)).map(a => (
                    <li key={a.actividadId}>
                      <button type="button" className="ws-list-row" onClick={() => navigate(`/actividades?id=${a.actividadId}`)}>
                        <span className="ws-tag-comp" title={a.componenteEtiquetaCorta}>
                          P{a.parcial}·{siglaComponente(a.componente)}
                        </span>
                        <span className="ws-list-main">
                          <strong>{a.nombre}</strong>
                          <Barra porcentaje={a.promedioPorcentaje} tono="danger" />
                        </span>
                        <span className="ws-num ws-tone-danger">{porcentaje(a.promedioPorcentaje, 0)}</span>
                      </button>
                    </li>
                  ))}
                </ul>
                {!abierto && <VerMas restantes={bajas.length - VISIBLES} onClick={abrir} />}
              </>
            )
          }
        </Panel>
      </div>
    </div>
  )
}

export function SinCursos() {
  return (
    <div className="ws-page">
      <Vacio titulo="Aún no tienes asignaturas">
        Tus cursos de Moodle aparecen aquí automáticamente. Si no los ves, verifica que tu correo en Moodle sea el mismo
        de tu cuenta de EduMonitor.
      </Vacio>
    </div>
  )
}
