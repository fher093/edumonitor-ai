import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useCursoActivo, useDatosCurso } from '../cursos/CursoContext.jsx'
import { Avatar, Buscador, Cargando, FiltroChips, Modal, Panel, Tarjeta, Vacio } from '../components/ui.jsx'
import { IconAlertas, IconCampana, IconFlecha, IconReglas } from '../components/Icons.jsx'
import { fechaHora, TIPO_ALERTA_CORTO } from '../utils/formato.js'
import { SinCursos } from './Resumen.jsx'

const TIPOS = [
  { valor: 'todos', etiqueta: 'Todas' },
  { valor: 'descenso', etiqueta: 'Descenso' },
  { valor: 'atraso', etiqueta: 'Atraso' },
  { valor: 'baja_finalizacion', etiqueta: 'Baja finalización' },
]

const NOMBRE_PARAMETRO = { k: 'k', delta: 'Δ', n: 'n', p: 'p' }

function parametros(json) {
  try {
    return Object.entries(JSON.parse(json))
      .map(([k, v]) => `${NOMBRE_PARAMETRO[k] ?? k} = ${v}${k === 'p' ? ' %' : ''}`)
      .join(' · ')
  } catch {
    return json
  }
}

/**
 * Alertas tempranas (HU-10, RN-04 a RN-07): reglas deterministas con evidencia verificable.
 * Se presentan como señales de seguimiento, nunca como diagnósticos.
 */
export default function Alertas() {
  const { cursos, cargando: cargandoCursos } = useCursoActivo()
  const { datos, cargando, error } = useDatosCurso(id => `/api/alertas/curso/${id}`)
  const [tipo, setTipo] = useState('todos')
  const [busqueda, setBusqueda] = useState('')
  const [seleccion, setSeleccion] = useState(0)
  const [verReglas, setVerReglas] = useState(false)

  const alertas = useMemo(() => datos?.alertas ?? [], [datos])
  const visibles = useMemo(() => {
    const q = busqueda.trim().toLowerCase()
    return alertas.filter(
      a =>
        (tipo === 'todos' || a.tipo === tipo) &&
        (!q || a.nombre.toLowerCase().includes(q) || a.identificacion.includes(q) || a.evidencia.toLowerCase().includes(q))
    )
  }, [alertas, tipo, busqueda])

  useEffect(() => setSeleccion(0), [tipo, busqueda, datos])

  if (!cargandoCursos && cursos.length === 0) return <SinCursos />

  const actual = visibles[seleccion] ?? null
  const regla = actual ? datos?.reglas.find(r => r.tipo === actual.tipo) : null
  const otras = actual ? alertas.filter(a => a.estudianteId === actual.estudianteId && a !== actual) : []

  return (
    <div className="ws-page">
      <div className="ws-split">
        <Panel
          className="ws-master"
          titulo="Alertas"
          icono={<IconCampana size={16} />}
          meta={datos ? `${datos.resumen.total} alertas · ${datos.resumen.estudiantesAfectados} estudiantes · corte ${fechaHora(datos.fechaCalculo)}` : ''}
          acciones={
            <button type="button" className="ws-btn ws-btn-ghost ws-btn-sm" onClick={() => setVerReglas(true)} disabled={!datos}>
              <IconReglas size={13} /> Reglas
            </button>
          }
          cuerpoClassName="ws-flush"
        >
          <div className="ws-master-tools">
            <Buscador valor={busqueda} onCambio={setBusqueda} placeholder="Estudiante o evidencia..." etiqueta="Buscar alerta" />
            <FiltroChips
              etiqueta="Tipo de alerta"
              valor={tipo}
              onCambio={setTipo}
              opciones={TIPOS.map(t => ({
                ...t,
                cantidad: t.valor === 'todos' ? alertas.length : alertas.filter(a => a.tipo === t.valor).length,
              }))}
            />
          </div>
          {error && <div className="banner-alert banner-alert-error">{error}</div>}
          {cargando && !datos ? (
            <Cargando texto="Evaluando reglas de alerta..." />
          ) : visibles.length === 0 ? (
            <Vacio titulo={alertas.length === 0 ? 'Sin alertas' : 'Sin coincidencias'}>
              {alertas.length === 0 ? 'Ningún estudiante activa las reglas configuradas.' : 'Prueba con otro filtro.'}
            </Vacio>
          ) : (
            <ul className="ws-list" role="listbox" aria-label="Alertas">
              {visibles.map((a, i) => (
                <li key={`${a.estudianteId}-${a.tipo}-${i}`}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={i === seleccion}
                    className={`ws-list-row ${i === seleccion ? 'is-selected' : ''}`}
                    onClick={() => setSeleccion(i)}
                  >
                    <span className={`ws-severity ws-severity-${a.severidad === 'alta' ? 'danger' : 'warn'}`} aria-hidden="true" />
                    <Avatar nombre={a.nombre} estudianteId={a.estudianteId} tamano={30} />
                    <span className="ws-list-main">
                      <strong>{a.nombre}</strong>
                      <span className="ws-list-sub">{a.evidencia}</span>
                    </span>
                    <span className={`ws-pill ws-pill-${a.severidad === 'alta' ? 'danger' : 'warn'}`} title={a.tipoEtiqueta}>{TIPO_ALERTA_CORTO[a.tipo] ?? a.tipoEtiqueta}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Panel>

        <section className="ws-detail">
          {!actual ? (
            <Panel>
              <Vacio titulo="Selecciona una alerta" icono={<IconAlertas size={22} />}>
                Verás la evidencia numérica que la activó y la regla aplicada.
              </Vacio>
            </Panel>
          ) : (
            <div className="ws-ficha">
              <div className="ws-ficha-head">
                <Avatar nombre={actual.nombre} estudianteId={actual.estudianteId} tamano={48} />
                <div className="ws-ficha-id">
                  <span className="ws-eyebrow">
                    {actual.tipoEtiqueta} · severidad {actual.severidad}
                  </span>
                  <h1>{actual.nombre}</h1>
                  <span className="ws-muted">{actual.identificacion}</span>
                </div>
                <Link to={`/estudiantes?id=${actual.estudianteId}`} className="ws-btn ws-btn-dark">
                  Ver ficha <IconFlecha size={14} />
                </Link>
              </div>

              <div className="ws-tab-body">
                <div className={`ws-evidence ws-evidence-${actual.severidad === 'alta' ? 'danger' : 'warn'}`}>
                  <span className="ws-eyebrow">Evidencia</span>
                  <p>{actual.evidencia}</p>
                </div>

                <div className="ws-grid ws-grid-2 ws-grid-auto">
                  <Tarjeta key={`regla-${seleccion}`} titulo="Regla aplicada" icono={<IconReglas size={15} />} expandible>
                    {abierto => (
                      <>
                        <p className="ws-card-text">{regla?.nombre ?? actual.tipoEtiqueta}</p>
                        {regla && <code className="ws-code">{parametros(regla.parametrosJson)}</code>}
                        {abierto && (
                          <p className="ws-muted ws-small ws-reveal">
                            Calculada el {fechaHora(actual.fechaCalculo)} con la regla {actual.regla}. Los umbrales los configura el
                            administrador técnico; la alerta desaparece sola cuando el estudiante deja de cumplir la condición.
                          </p>
                        )}
                      </>
                    )}
                  </Tarjeta>
                  <Tarjeta
                    key={`otras-${seleccion}`}
                    titulo="Otras alertas del estudiante"
                    icono={<IconCampana size={15} />}
                    expandible={otras.length > 0}
                  >
                    {abierto =>
                      otras.length === 0 ? (
                        <p className="ws-muted ws-small">Ninguna otra regla se activó para este estudiante.</p>
                      ) : !abierto ? (
                        <div className="ws-states">
                          {otras.map((o, i) => (
                            <span key={i} className={`ws-pill ws-pill-${o.severidad === 'alta' ? 'danger' : 'warn'}`}>
                              {TIPO_ALERTA_CORTO[o.tipo] ?? o.tipoEtiqueta}
                            </span>
                          ))}
                        </div>
                      ) : (
                        <ul className="ws-mini-list ws-reveal">
                          {otras.map((o, i) => (
                            <li key={i}>
                              <span className={`ws-pill ws-pill-${o.severidad === 'alta' ? 'danger' : 'warn'}`}>{o.tipoEtiqueta}</span>
                              <span>{o.evidencia}</span>
                            </li>
                          ))}
                        </ul>
                      )
                    }
                  </Tarjeta>
                </div>
              </div>
              <p className="ws-foot-note">
                Las alertas son señales para el acompañamiento docente, no etiquetas definitivas del estudiante (RN-07).
              </p>
            </div>
          )}
        </section>
      </div>

      <Modal abierto={verReglas} onCerrar={() => setVerReglas(false)} eyebrow="Configuradas por el administrador técnico" titulo="Reglas de alerta aplicadas" ancho={640}>
        <table className="ws-table">
          <thead>
            <tr>
              <th>Regla</th>
              <th>Parámetros</th>
              <th>Estado</th>
            </tr>
          </thead>
          <tbody>
            {datos?.reglas.map(r => (
              <tr key={r.id}>
                <td>
                  <strong>{r.nombre}</strong>
                  <small className="ws-block ws-muted">{r.tipo}</small>
                </td>
                <td>
                  <code className="ws-code">{parametros(r.parametrosJson)}</code>
                </td>
                <td>
                  <span className={`ws-pill ws-pill-${r.activa ? 'ok' : 'neutral'}`}>{r.activa ? 'Activa' : 'Inactiva'}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Modal>
    </div>
  )
}
