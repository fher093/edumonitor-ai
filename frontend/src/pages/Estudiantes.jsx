import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../api/http.js'
import { useCursoActivo, useDatosCurso } from '../cursos/CursoContext.jsx'
import {
  Avatar,
  Barra,
  Buscador,
  Cargando,
  EstadoChip,
  FiltroChips,
  NotaMeter,
  Panel,
  GraficoEvolucion,
  Tarjeta,
  Vacio,
} from '../components/ui.jsx'
import { IconCapas, IconMatricula, IconTendencia } from '../components/Icons.jsx'
import { fecha, numero, porcentaje, siglaComponente } from '../utils/formato.js'
import { SinCursos } from './Resumen.jsx'

// HU-09: filtros del seguimiento. Aprobación proyectada: nota final ≥ 14/20 (RN-08).
const FILTROS = [
  { valor: 'todos', etiqueta: 'Todos', cumple: () => true },
  { valor: 'alertas', etiqueta: 'Con alertas', cumple: e => e.alertas > 0 },
  { valor: 'riesgo', etiqueta: 'Final < 14', cumple: e => e.notaFinal !== null && e.notaFinal < 14 },
  { valor: 'sinEntrega', etiqueta: 'Sin entrega', cumple: e => e.sinEntrega > 0 },
  { valor: 'pendiente', etiqueta: 'Final pendiente', cumple: e => e.notaFinal === null },
]

/**
 * Estudiantes del curso (HU-06, HU-08, HU-09): lista con búsqueda y filtros a la izquierda,
 * ficha individual a la derecha. Sustituye a la nómina, a la tabla del dashboard y a la ficha.
 */
export default function Estudiantes() {
  const { cursoId, cursos, cargando: cargandoCursos } = useCursoActivo()
  const { datos: dashboard, cargando, error } = useDatosCurso(id => `/api/indicadores/curso/${id}`)
  const [params, setParams] = useSearchParams()
  const [busqueda, setBusqueda] = useState('')
  const [filtro, setFiltro] = useState('todos')

  const estudiantes = useMemo(() => dashboard?.estudiantes ?? [], [dashboard])
  const visibles = useMemo(() => {
    const q = busqueda.trim().toLowerCase()
    const criterio = FILTROS.find(f => f.valor === filtro)?.cumple ?? (() => true)
    return estudiantes.filter(
      e =>
        criterio(e) &&
        (!q || e.nombre.toLowerCase().includes(q) || e.identificacion.includes(q) || e.correo?.toLowerCase().includes(q))
    )
  }, [estudiantes, busqueda, filtro])

  const seleccionadoId = Number(params.get('id')) || null
  const seleccionado = estudiantes.find(e => e.estudianteId === seleccionadoId) ?? null

  // Sin selección (o selección de otro curso): abre el primero de la lista.
  useEffect(() => {
    if (estudiantes.length > 0 && !seleccionado) {
      setParams({ id: String(estudiantes[0].estudianteId) }, { replace: true })
    }
  }, [estudiantes, seleccionado, setParams])

  if (!cargandoCursos && cursos.length === 0) return <SinCursos />

  const escala = dashboard?.escalaCalificacion ?? 20

  return (
    <div className="ws-page">
      <div className="ws-split">
        <Panel
          className="ws-master"
          titulo="Estudiantes"
          icono={<IconMatricula size={16} />}
          meta={dashboard ? `${visibles.length} de ${estudiantes.length} · nómina sincronizada desde Moodle` : ''}
          cuerpoClassName="ws-flush"
        >
          <div className="ws-master-tools">
            <Buscador valor={busqueda} onCambio={setBusqueda} placeholder="Nombre, cédula o correo..." etiqueta="Buscar estudiante" />
            <FiltroChips
              etiqueta="Filtrar estudiantes"
              valor={filtro}
              onCambio={setFiltro}
              opciones={FILTROS.map(f => ({ ...f, cantidad: estudiantes.filter(f.cumple).length }))}
            />
          </div>
          {error && <div className="banner-alert banner-alert-error">{error}</div>}
          {cargando && !dashboard ? (
            <Cargando texto="Cargando estudiantes..." />
          ) : visibles.length === 0 ? (
            <Vacio titulo={estudiantes.length === 0 ? 'Sin estudiantes matriculados' : 'Sin coincidencias'}>
              {estudiantes.length === 0
                ? 'La matrícula se gestiona en Moodle y aparece aquí automáticamente.'
                : 'Prueba con otro nombre o quita el filtro.'}
            </Vacio>
          ) : (
            <ul className="ws-list" role="listbox" aria-label="Estudiantes">
              {visibles.map(e => (
                <li key={e.estudianteId}>
                  <button
                    type="button"
                    role="option"
                    aria-selected={e.estudianteId === seleccionadoId}
                    className={`ws-list-row ${e.estudianteId === seleccionadoId ? 'is-selected' : ''}`}
                    onClick={() => setParams({ id: String(e.estudianteId) })}
                  >
                    <Avatar nombre={e.nombre} estudianteId={e.estudianteId} />
                    <span className="ws-list-main">
                      <strong>{e.nombre}</strong>
                      <span className="ws-list-sub">{e.identificacion}</span>
                    </span>
                    <span className="ws-list-side">
                      <NotaMeter valor={e.notaFinal} escala={20} compacto />
                      {e.alertas > 0 && <span className="ws-dot-count" title={`${e.alertas} alerta(s)`}>{e.alertas}</span>}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </Panel>

        <section className="ws-detail">
          {seleccionado ? (
            <FichaEstudiante key={`${cursoId}-${seleccionado.estudianteId}`} cursoId={cursoId} resumen={seleccionado} escala={escala} />
          ) : (
            <Panel>
              <Vacio titulo="Selecciona un estudiante" icono={<IconMatricula size={22} />}>
                Su ficha muestra parciales, componentes, evolución, actividades y alertas.
              </Vacio>
            </Panel>
          )}
        </section>
      </div>
    </div>
  )
}

/** Ficha individual (HU-08) con pestañas para no apilar todo en una columna larga. */
function FichaEstudiante({ cursoId, resumen, escala }) {
  const [ficha, setFicha] = useState(null)
  const [error, setError] = useState('')
  const [pestana, setPestana] = useState('resumen')

  useEffect(() => {
    let vivo = true
    api(`/api/indicadores/curso/${cursoId}/estudiantes/${resumen.estudianteId}`)
      .then(d => vivo && setFicha(d))
      .catch(err => vivo && setError(err.message))
    return () => {
      vivo = false
    }
  }, [cursoId, resumen.estudianteId])

  return (
    <div className="ws-ficha">
      <div className="ws-ficha-head">
        <Avatar nombre={resumen.nombre} estudianteId={resumen.estudianteId} tamano={48} />
        <div className="ws-ficha-id">
          <h1>{resumen.nombre}</h1>
          <span className="ws-muted">
            {resumen.identificacion} · {resumen.correo}
          </span>
        </div>
        <div className="ws-ficha-final">
          <span className="ws-eyebrow">Nota final</span>
          <NotaMeter valor={resumen.notaFinal} escala={20} />
        </div>
      </div>

      <div className="ws-mini-kpis">
        <MiniKpi etiqueta={`Promedio (/${numero(escala, 0)})`} valor={numero(resumen.promedioActividades)} />
        <MiniKpi etiqueta="Parcial 1 (/10)" valor={numero(resumen.notaParcial1)} />
        <MiniKpi etiqueta="Parcial 2 (/10)" valor={numero(resumen.notaParcial2)} />
        <MiniKpi etiqueta="Finalización" valor={porcentaje(resumen.porcentajeFinalizacion, 0)} />
        <MiniKpi etiqueta="Alertas" valor={resumen.alertas} tono={resumen.alertas > 0 ? 'warn' : undefined} />
      </div>

      <div className="ws-tabs" role="tablist">
        {[
          ['resumen', 'Resumen'],
          ['actividades', `Actividades${ficha ? ` (${ficha.actividades.length})` : ''}`],
          ['alertas', `Alertas${ficha ? ` (${ficha.alertas.length})` : ''}`],
        ].map(([valor, etiqueta]) => (
          <button
            key={valor}
            type="button"
            role="tab"
            aria-selected={pestana === valor}
            className={pestana === valor ? 'is-active' : ''}
            onClick={() => setPestana(valor)}
          >
            {etiqueta}
          </button>
        ))}
      </div>

      <div className="ws-tab-body">
        {error ? (
          <div className="banner-alert banner-alert-error">{error}</div>
        ) : !ficha ? (
          <Cargando texto="Cargando ficha..." />
        ) : pestana === 'resumen' ? (
          <ResumenFicha ficha={ficha} />
        ) : pestana === 'actividades' ? (
          <ActividadesFicha ficha={ficha} />
        ) : (
          <AlertasFicha ficha={ficha} />
        )}
      </div>
    </div>
  )
}

function MiniKpi({ etiqueta, valor, tono }) {
  return (
    <div className={`ws-mini-kpi ${tono ? `ws-tone-${tono}` : ''}`}>
      <span className="ws-eyebrow">{etiqueta}</span>
      <strong>{valor}</strong>
    </div>
  )
}

function ResumenFicha({ ficha }) {
  const completo = { 1: ficha.parcial1Completo, 2: ficha.parcial2Completo }
  return (
    <div className="ws-grid ws-grid-2 ws-grid-auto">
      <Tarjeta
        className="ws-card-full"
        titulo="Evolución de notas"
        meta="Cada punto es una actividad, en orden de entrega · nota normalizada sobre 10"
        icono={<IconTendencia size={15} />}
      >
        <GraficoEvolucion
          actividades={ficha.actividades
            .slice()
            .sort((a, b) => new Date(a.fechaEntrega ?? 8.64e15) - new Date(b.fechaEntrega ?? 8.64e15))
            .map(a => ({
              id: a.actividadId,
              nombre: a.nombre,
              nota: a.nota,
              puntajeMaximo: a.puntajeMaximo,
              estado: a.estado,
              fechaEntrega: a.fechaEntrega,
            }))}
        />
        <div className="ws-chart-legend">
          <div className="ws-chart-keys">
            <span><i className="ws-key ws-key-punto" /> Nota ≥ 7</span>
            <span><i className="ws-key ws-key-bajo" /> Nota &lt; 7</span>
            <span><i className="ws-key ws-key-umbral" /> Umbral 7/10 (14/20)</span>
            <span><i className="ws-key ws-key-hueco" /> Sin nota</span>
          </div>
          <div className="ws-states">
            <span className="state-chip state-chip-ok">{ficha.calificadas} calificadas</span>
            <span className="state-chip state-chip-danger">{ficha.notaCero} nota cero</span>
            <span className="state-chip state-chip-warn">{ficha.sinEntrega} sin entrega</span>
            <span className="state-chip state-chip-neutral">{ficha.pendientes} pendientes</span>
            <span className="state-chip state-chip-info">{ficha.completadas} completadas</span>
          </div>
        </div>
      </Tarjeta>
      <Tarjeta className="ws-card-full" titulo="Componentes UTA" meta="40 / 30 / 30 · sobre 10" icono={<IconCapas size={15} />} expandible>
        {abierto =>
          abierto ? (
            [1, 2].map(parcial => (
              <div key={parcial} className="ws-comp-block ws-reveal">
                <span className="ws-eyebrow">
                  Parcial {parcial} · {completo[parcial] ? 'completo' : 'incompleto'}
                </span>
                {ficha.componentes
                  .filter(c => c.parcial === parcial)
                  .map(c => (
                    <FilaComponente key={c.componente} c={c} />
                  ))}
              </div>
            ))
          ) : (
            <div className="ws-comp-block">
              <span className="ws-eyebrow">Los más bajos</span>
              {ficha.componentes
                .filter(c => c.tieneDatos)
                .sort((x, y) => x.promedioSobre10 - y.promedioSobre10)
                .slice(0, 3)
                .map(c => (
                  <FilaComponente key={`${c.parcial}-${c.componente}`} c={c} conParcial />
                ))}
            </div>
          )
        }
      </Tarjeta>
    </div>
  )
}

function FilaComponente({ c, conParcial = false }) {
  const tono = c.tieneDatos ? (c.promedioSobre10 >= 7 ? 'ok' : c.promedioSobre10 >= 5 ? 'warn' : 'danger') : 'none'
  return (
    <div className="ws-comp-row">
      <span className="ws-comp-name">
        {conParcial && <span className="ws-muted">P{c.parcial} · </span>}
        {c.etiquetaCorta} <small>{numero(c.ponderacion * 100, 0)} %</small>
      </span>
      <Barra porcentaje={(c.promedioSobre10 ?? 0) * 10} tono={tono} />
      <span className="ws-num">{c.tieneDatos ? numero(c.promedioSobre10) : '—'}</span>
    </div>
  )
}

function ActividadesFicha({ ficha }) {
  return (
    <table className="ws-table">
      <thead>
        <tr>
          <th>Actividad</th>
          <th>Comp.</th>
          <th>Entrega</th>
          <th>Estado</th>
          <th className="ws-right">Nota</th>
        </tr>
      </thead>
      <tbody>
        {ficha.actividades.map(a => (
          <tr key={a.actividadId}>
            <td>
              <strong>{a.nombre}</strong>
            </td>
            <td>
              <span className="ws-tag-comp" title={a.componenteEtiqueta}>P{a.parcial}·{siglaComponente(a.componente)}</span>
            </td>
            <td className="ws-muted">{fecha(a.fechaEntrega)}</td>
            <td>
              <EstadoChip estado={a.estado} />
            </td>
            <td className="ws-right ws-num">
              {a.nota === null ? '—' : `${numero(a.nota)}/${numero(a.puntajeMaximo, 0)}`}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function AlertasFicha({ ficha }) {
  if (ficha.alertas.length === 0)
    return <Vacio titulo="Sin alertas">Este estudiante no activa ninguna regla de seguimiento.</Vacio>
  return (
    <ul className="ws-alert-cards">
      {ficha.alertas.map((a, i) => (
        <li key={i} className={`ws-alert-card ws-alert-${a.severidad === 'alta' ? 'danger' : 'warn'}`}>
          <div className="ws-alert-top">
            <strong>{a.tipoEtiqueta}</strong>
            <span className="ws-muted ws-small">{a.regla}</span>
          </div>
          <p>{a.evidencia}</p>
        </li>
      ))}
      <li className="ws-muted ws-small">Son señales para el acompañamiento docente, no diagnósticos (RN-07).</li>
    </ul>
  )
}
