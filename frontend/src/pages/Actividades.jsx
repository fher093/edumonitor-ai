import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api/http.js'
import { useCursoActivo, useDatosCurso } from '../cursos/CursoContext.jsx'
import { Avatar, Barra, Buscador, Cargando, EstadoChip, FiltroChips, Panel, Vacio } from '../components/ui.jsx'
import { IconActividades, IconExterno } from '../components/Icons.jsx'
import { fecha, fechaHora, numero, porcentaje, siglaComponente } from '../utils/formato.js'
import { SinCursos } from './Resumen.jsx'

const FILTROS_NOTAS = [
  { valor: 'todos', etiqueta: 'Todos' },
  { valor: 'calificada', etiqueta: 'Calificada' },
  { valor: 'nota_cero', etiqueta: 'Nota cero' },
  { valor: 'sin_entrega', etiqueta: 'Sin entrega' },
  { valor: 'pendiente', etiqueta: 'Pendiente' },
  { valor: 'completada', etiqueta: 'Completada' },
]

/**
 * Actividades y calificaciones sincronizadas desde Moodle (HU-05). Solo consulta:
 * se crean y califican en Moodle (botón "Calificar en Moodle").
 */
export default function Actividades() {
  const { cursoId, cursos, cargando: cargandoCursos } = useCursoActivo()
  const { datos: actividades, cargando, error } = useDatosCurso(id => `/api/cursos/${id}/actividades`)
  const { datos: analisis } = useDatosCurso(id => `/api/analisis/curso/${id}`)
  const [params, setParams] = useSearchParams()
  const [busqueda, setBusqueda] = useState('')

  const logro = useMemo(() => {
    const mapa = {}
    analisis?.detalleActividades.forEach(a => (mapa[a.actividadId] = a))
    return mapa
  }, [analisis])

  const lista = useMemo(() => actividades ?? [], [actividades])
  const visibles = useMemo(() => {
    const q = busqueda.trim().toLowerCase()
    return q ? lista.filter(a => a.nombre.toLowerCase().includes(q) || a.tipo.toLowerCase().includes(q)) : lista
  }, [lista, busqueda])

  const seleccionadaId = Number(params.get('id')) || null
  const seleccionada = lista.find(a => a.id === seleccionadaId) ?? null

  useEffect(() => {
    if (lista.length > 0 && !seleccionada) setParams({ id: String(lista[0].id) }, { replace: true })
  }, [lista, seleccionada, setParams])

  if (!cargandoCursos && cursos.length === 0) return <SinCursos />

  return (
    <div className="ws-page">
      <div className="ws-split">
        <Panel
          className="ws-master"
          titulo="Actividades"
          icono={<IconActividades size={16} />}
          meta={actividades ? `${lista.length} ítems de calificación de Moodle` : ''}
          cuerpoClassName="ws-flush"
        >
          <div className="ws-master-tools">
            <Buscador valor={busqueda} onCambio={setBusqueda} placeholder="Buscar actividad..." />
          </div>
          {error && <div className="banner-alert banner-alert-error">{error}</div>}
          {cargando && !actividades ? (
            <Cargando texto="Cargando actividades..." />
          ) : visibles.length === 0 ? (
            <Vacio titulo={lista.length === 0 ? 'Sin actividades' : 'Sin coincidencias'}>
              {lista.length === 0 ? 'Créalas en Moodle: aparecerán aquí automáticamente en unos minutos.' : 'Prueba con otro nombre.'}
            </Vacio>
          ) : (
            [1, 2].map(parcial => {
              const delParcial = visibles.filter(a => a.parcial === parcial)
              if (delParcial.length === 0) return null
              return (
                <div key={parcial} className="ws-group">
                  <span className="ws-group-title">Parcial {parcial}</span>
                  <ul className="ws-list" role="listbox" aria-label={`Actividades del parcial ${parcial}`}>
                    {delParcial.map(a => {
                      const avance = a.totalEstudiantes ? (a.totalCalificados / a.totalEstudiantes) * 100 : 0
                      const bajo = logro[a.id]?.resultadoBajo
                      return (
                        <li key={a.id}>
                          <button
                            type="button"
                            role="option"
                            aria-selected={a.id === seleccionadaId}
                            className={`ws-list-row ${a.id === seleccionadaId ? 'is-selected' : ''}`}
                            onClick={() => setParams({ id: String(a.id) })}
                          >
                            <span className="ws-tag-comp" title={a.componenteEtiqueta}>{siglaComponente(a.componente)}</span>
                            <span className="ws-list-main">
                              <strong>{a.nombre}</strong>
                              <span className="ws-list-sub">
                                {a.vencida ? 'Vencida' : 'Abierta'} · {fecha(a.fechaEntrega)} · {a.totalCalificados}/{a.totalEstudiantes} calificados
                              </span>
                              <Barra porcentaje={avance} tono={avance >= 100 ? 'ok' : 'brand'} />
                            </span>
                            {bajo && <span className="ws-pill ws-pill-danger" title="Logro promedio menor al 60 %">Bajo</span>}
                          </button>
                        </li>
                      )
                    })}
                  </ul>
                </div>
              )
            })
          )}
        </Panel>

        <section className="ws-detail">
          {seleccionada ? (
            <DetalleActividad key={`${cursoId}-${seleccionada.id}`} actividad={seleccionada} logro={logro[seleccionada.id]} />
          ) : (
            <Panel>
              <Vacio titulo="Selecciona una actividad" icono={<IconActividades size={22} />}>
                Verás la nota y el estado de cada estudiante.
              </Vacio>
            </Panel>
          )}
        </section>
      </div>
    </div>
  )
}

function DetalleActividad({ actividad, logro }) {
  const [notas, setNotas] = useState(null)
  const [error, setError] = useState('')
  const [filtro, setFiltro] = useState('todos')

  useEffect(() => {
    let vivo = true
    api(`/api/actividades/${actividad.id}/calificaciones`)
      .then(d => vivo && setNotas(d))
      .catch(err => vivo && setError(err.message))
    return () => {
      vivo = false
    }
  }, [actividad.id])

  const conteo = useMemo(() => {
    const c = {}
    notas?.forEach(n => (c[n.estado] = (c[n.estado] ?? 0) + 1))
    return c
  }, [notas])
  const visibles = (notas ?? []).filter(n => filtro === 'todos' || n.estado === filtro)

  return (
    <div className="ws-ficha">
      <div className="ws-ficha-head">
        <span className="ws-tile-icon"><IconActividades size={20} /></span>
        <div className="ws-ficha-id">
          <span className="ws-eyebrow">
            Parcial {actividad.parcial} · {actividad.componenteEtiqueta} · {actividad.tipo}
          </span>
          <h1>{actividad.nombre}</h1>
          <span className="ws-muted">
            Entrega {fechaHora(actividad.fechaEntrega)} · puntaje máximo {numero(actividad.puntajeMaximo, 0)} ·{' '}
            <span className={`ws-pill ws-pill-${actividad.vencida ? 'warn' : 'info'}`}>{actividad.vencida ? 'Vencida' : 'Abierta'}</span>
          </span>
        </div>
        {actividad.urlMoodle && (
          <a className="ws-btn ws-btn-dark" href={actividad.urlMoodle} target="_blank" rel="noopener noreferrer">
            <IconExterno size={14} /> Calificar en Moodle
          </a>
        )}
      </div>

      <div className="ws-mini-kpis">
        <div className="ws-mini-kpi">
          <span className="ws-eyebrow">Calificados</span>
          <strong>
            {actividad.totalCalificados}/{actividad.totalEstudiantes}
          </strong>
        </div>
        <div className={`ws-mini-kpi ${logro?.resultadoBajo ? 'ws-tone-danger' : ''}`}>
          <span className="ws-eyebrow">Logro promedio</span>
          <strong>{porcentaje(logro?.promedioPorcentaje, 0)}</strong>
        </div>
        <div className="ws-mini-kpi">
          <span className="ws-eyebrow">Nota promedio</span>
          <strong>
            {numero(logro?.promedioNota)}
            <small>/{numero(actividad.puntajeMaximo, 0)}</small>
          </strong>
        </div>
        <div className="ws-mini-kpi">
          <span className="ws-eyebrow">Finalización</span>
          <strong>{porcentaje(logro?.porcentajeFinalizacion, 0)}</strong>
        </div>
      </div>

      <div className="ws-tab-tools">
        <FiltroChips
          etiqueta="Filtrar por estado"
          valor={filtro}
          onCambio={setFiltro}
          opciones={FILTROS_NOTAS.map(f => ({ ...f, cantidad: f.valor === 'todos' ? notas?.length ?? 0 : conteo[f.valor] ?? 0 }))}
        />
      </div>

      <div className="ws-tab-body">
        {error ? (
          <div className="banner-alert banner-alert-error">{error}</div>
        ) : !notas ? (
          <Cargando texto="Cargando calificaciones..." />
        ) : visibles.length === 0 ? (
          <Vacio titulo="Sin estudiantes en este estado" />
        ) : (
          <table className="ws-table">
            <thead>
              <tr>
                <th>Estudiante</th>
                <th>Estado</th>
                <th className="ws-right">Nota</th>
                <th className="ws-right">Actualizado</th>
              </tr>
            </thead>
            <tbody>
              {visibles.map(n => (
                <tr key={n.estudianteId}>
                  <td>
                    <span className="ws-person">
                      <Avatar nombre={n.nombre} estudianteId={n.estudianteId} tamano={26} />
                      <span>
                        <strong>{n.nombre}</strong>
                        <small>{n.identificacion}</small>
                      </span>
                    </span>
                  </td>
                  <td>
                    <EstadoChip estado={n.estado} />
                  </td>
                  <td className="ws-right ws-num">{n.nota === null ? '—' : `${numero(n.nota)}/${numero(n.puntajeMaximo, 0)}`}</td>
                  <td className="ws-right ws-muted ws-small">{fechaHora(n.fechaActualizacion)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <p className="ws-foot-note">Solo lectura: las notas se registran en Moodle y se sincronizan automáticamente.</p>
    </div>
  )
}
