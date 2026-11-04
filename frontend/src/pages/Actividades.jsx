import { useEffect, useMemo, useState } from 'react'
import { api } from '../api/http.js'
import useCursos from '../hooks/useCursos.js'
import SelectorCurso from '../components/SelectorCurso.jsx'
import { IconPlus, IconEdit, IconTrash, IconClose } from '../components/Icons.jsx'
import {
  claseEstado,
  etiquetaEstado,
  fecha,
  numero,
  paraInputFecha,
  porcentaje,
} from '../utils/formato.js'

const COMPONENTES = [
  { valor: 'contacto_docente', etiqueta: 'Contacto con el docente (40 %)' },
  { valor: 'practico_experimental', etiqueta: 'Aprendizaje práctico-experimental (30 %)' },
  { valor: 'autonomo', etiqueta: 'Aprendizaje autónomo (30 %)' },
]

const ESTADOS = ['calificada', 'nota_cero', 'sin_entrega', 'pendiente', 'completada']

const FORM_VACIO = {
  nombre: '',
  tipo: 'Tarea',
  componente: 'autonomo',
  parcial: 1,
  fechaEntrega: '',
  puntajeMaximo: 10,
}

export default function Actividades() {
  const cursosHook = useCursos()
  const { cursoId, cursoActivo, cargando: cargandoCursos, recargar } = cursosHook

  const [actividades, setActividades] = useState([])
  const [finalizacion, setFinalizacion] = useState(null)
  const [cargando, setCargando] = useState(false)
  const [error, setError] = useState('')

  // Formulario de creación/edición
  const [formAbierto, setFormAbierto] = useState(false)
  const [editando, setEditando] = useState(null)
  const [form, setForm] = useState(FORM_VACIO)
  const [guardando, setGuardando] = useState(false)
  const [mensajeForm, setMensajeForm] = useState({ tipo: '', texto: '' })

  // Panel de calificaciones
  const [calificando, setCalificando] = useState(null)
  const [filas, setFilas] = useState([])
  const [originales, setOriginales] = useState({})
  const [cargandoNotas, setCargandoNotas] = useState(false)
  const [guardandoNotas, setGuardandoNotas] = useState(false)
  const [mensajeNotas, setMensajeNotas] = useState({ tipo: '', texto: '' })

  async function cargarActividades(id) {
    if (!id) {
      setActividades([])
      setFinalizacion(null)
      return
    }
    setCargando(true)
    setError('')
    try {
      const [lista, fin] = await Promise.all([
        api(`/api/cursos/${id}/actividades`),
        api(`/api/cursos/${id}/finalizacion`).catch(() => null),
      ])
      setActividades(lista)
      setFinalizacion(fin)
    } catch (err) {
      setError(err.message)
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => {
    cargarActividades(cursoId)
  }, [cursoId])

  useEffect(() => {
    if (!formAbierto) return
    if (editando) {
      setForm({
        nombre: editando.nombre,
        tipo: editando.tipo,
        componente: editando.componente,
        parcial: editando.parcial,
        fechaEntrega: paraInputFecha(editando.fechaEntrega),
        puntajeMaximo: editando.puntajeMaximo,
      })
    } else {
      setForm(FORM_VACIO)
    }
    setMensajeForm({ tipo: '', texto: '' })
  }, [formAbierto, editando])

  function abrirCrear() {
    setEditando(null)
    setFormAbierto(true)
  }

  function abrirEditar(actividad) {
    setEditando(actividad)
    setFormAbierto(true)
  }

  async function guardarActividad(e) {
    e.preventDefault()
    if (!cursoId) return
    setGuardando(true)
    setMensajeForm({ tipo: '', texto: '' })
    try {
      const payload = {
        nombre: form.nombre.trim(),
        tipo: form.tipo.trim(),
        componente: form.componente,
        parcial: Number(form.parcial),
        fechaEntrega: form.fechaEntrega ? new Date(form.fechaEntrega).toISOString() : null,
        puntajeMaximo: Number(form.puntajeMaximo),
      }
      if (editando) {
        await api(`/api/actividades/${editando.id}`, { method: 'PUT', body: payload })
      } else {
        await api(`/api/cursos/${cursoId}/actividades`, { method: 'POST', body: payload })
      }
      setFormAbierto(false)
      setEditando(null)
      await cargarActividades(cursoId)
      recargar()
    } catch (err) {
      setMensajeForm({ tipo: 'error', texto: err.message })
    } finally {
      setGuardando(false)
    }
  }

  async function eliminarActividad(actividad) {
    const confirmado = window.confirm(
      `¿Eliminar la actividad "${actividad.nombre}"? También se eliminarán sus calificaciones registradas.`
    )
    if (!confirmado) return
    try {
      await api(`/api/actividades/${actividad.id}`, { method: 'DELETE' })
      await cargarActividades(cursoId)
      recargar()
    } catch (err) {
      setError(err.message)
    }
  }

  function snapshot(lista) {
    const mapa = {}
    lista.forEach(c => {
      mapa[c.estudianteId] = {
        estado: c.estado,
        nota: c.nota,
        observacion: c.observacion ?? '',
      }
    })
    return mapa
  }

  async function abrirCalificar(actividad) {
    setCalificando(actividad)
    setFilas([])
    setOriginales({})
    setMensajeNotas({ tipo: '', texto: '' })
    setCargandoNotas(true)
    try {
      const data = await api(`/api/actividades/${actividad.id}/calificaciones`)
      setFilas(data)
      setOriginales(snapshot(data))
    } catch (err) {
      setMensajeNotas({ tipo: 'error', texto: err.message })
    } finally {
      setCargandoNotas(false)
    }
  }

  function actualizarFila(estudianteId, campo, valor) {
    setFilas(prev =>
      prev.map(f => (f.estudianteId === estudianteId ? { ...f, [campo]: valor } : f))
    )
  }

  const filasModificadas = useMemo(() => {
    return filas.filter(f => {
      const original = originales[f.estudianteId]
      if (!original) return true
      return (
        String(f.estado) !== String(original.estado) ||
        String(f.nota ?? '') !== String(original.nota ?? '') ||
        String(f.observacion ?? '') !== String(original.observacion ?? '')
      )
    })
  }, [filas, originales])

  async function guardarCalificaciones() {
    if (!calificando) return
    if (filasModificadas.length === 0) {
      setMensajeNotas({ tipo: 'info', texto: 'No hay cambios por guardar.' })
      return
    }
    setGuardandoNotas(true)
    setMensajeNotas({ tipo: '', texto: '' })
    try {
      const payload = {
        calificaciones: filasModificadas.map(f => ({
          estudianteId: f.estudianteId,
          estado: f.estado,
          nota: f.nota === '' || f.nota === null ? null : Number(f.nota),
          observacion: f.observacion?.trim() ? f.observacion.trim() : null,
        })),
      }
      const data = await api(`/api/actividades/${calificando.id}/calificaciones`, {
        method: 'POST',
        body: payload,
      })
      setFilas(data)
      setOriginales(snapshot(data))
      setMensajeNotas({
        tipo: 'ok',
        texto: `Se guardaron ${payload.calificaciones.length} calificación(es) correctamente.`,
      })
      await cargarActividades(cursoId)
    } catch (err) {
      setMensajeNotas({ tipo: 'error', texto: err.message })
    } finally {
      setGuardandoNotas(false)
    }
  }

  return (
    <div className="page-container">
      <section className="product-surface-card">
        <div className="section-head-split">
          <div>
            <div className="brand-dot-pill">
              <span className="dot-violet" />
              <span className="tag-uta-micro">ACTIVIDADES Y CALIFICACIONES</span>
            </div>
            <h1 className="display-headline">Actividades Evaluables</h1>
            <p className="lead-subtitle">
              Registra actividades con su componente UTA, fecha de entrega y puntaje máximo.
              El estado de cada estudiante se calcula con las reglas de la asignatura.
            </p>
          </div>
          <button
            type="button"
            className="btn-pill btn-pill-dark"
            onClick={abrirCrear}
            disabled={!cursoId}
          >
            <IconPlus size={15} />
            <span>Nueva actividad</span>
          </button>
        </div>

        <div className="hairline-divider" />

        <SelectorCurso cursosHook={cursosHook} />

        {error && <div className="banner-alert banner-alert-error">{error}</div>}
        {!cargandoCursos && !cursoId && (
          <div className="state-empty-box">
            <p className="text-muted">No hay asignaturas disponibles para gestionar actividades.</p>
          </div>
        )}
        {cargando && (
          <div className="state-empty-box">
            <p className="text-muted">Cargando actividades del curso...</p>
          </div>
        )}

        {!cargando && cursoId && (
          <div className="panel-box">
            <div className="panel-head">
              <h3 className="panel-title">Actividades del curso</h3>
              <span className="text-muted text-xs">
                {cursoActivo?.nombre} · {actividades.length} actividades
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
                    <th>Puntaje</th>
                    <th>Calificados</th>
                    <th>Estado</th>
                    <th style={{ textAlign: 'right' }}>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {actividades.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="cell-empty-message">
                        Este curso todavía no tiene actividades. Crea la primera con el botón superior.
                      </td>
                    </tr>
                  ) : (
                    actividades.map(a => (
                      <tr key={a.id}>
                        <td>
                          <div className="cell-student-name">{a.nombre}</div>
                          <div className="text-muted text-xs">{a.tipo}</div>
                        </td>
                        <td>{a.componenteEtiquetaCorta}</td>
                        <td>P{a.parcial}</td>
                        <td>{fecha(a.fechaEntrega)}</td>
                        <td>{numero(a.puntajeMaximo)}</td>
                        <td>{a.totalCalificados} / {a.totalEstudiantes}</td>
                        <td>
                          <span className={`state-chip ${a.vencida ? 'state-chip-warn' : 'state-chip-info'}`}>
                            {a.vencida ? 'Vencida' : 'Abierta'}
                          </span>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <div className="table-actions-row">
                            <button
                              type="button"
                              className="btn-pill btn-pill-dark btn-sm"
                              onClick={() => abrirCalificar(a)}
                            >
                              Calificar
                            </button>
                            <button
                              type="button"
                              className="btn-pill btn-pill-ghost btn-sm"
                              onClick={() => abrirEditar(a)}
                              title="Editar actividad"
                            >
                              <IconEdit size={14} />
                            </button>
                            <button
                              type="button"
                              className="btn-pill btn-desmatr-danger btn-sm"
                              onClick={() => eliminarActividad(a)}
                              title="Eliminar actividad"
                            >
                              <IconTrash size={14} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {!cargando && finalizacion && finalizacion.estudiantes.length > 0 && (
          <div className="panel-box panel-box-spaced">
            <div className="panel-head">
              <h3 className="panel-title">Finalización por estudiante</h3>
              <span className="text-muted text-xs">
                Actividades completadas sobre el total del curso
              </span>
            </div>
            <div className="table-scroll-container">
              <table className="product-data-table compact-table">
                <thead>
                  <tr>
                    <th>Estudiante</th>
                    <th>Total</th>
                    <th>Completadas</th>
                    <th>Vencidas</th>
                    <th>Sin entrega</th>
                    <th>Pendientes</th>
                    <th>% Finalización</th>
                  </tr>
                </thead>
                <tbody>
                  {finalizacion.estudiantes.map(est => (
                    <tr key={est.estudianteId}>
                      <td>
                        <div className="cell-student-name">{est.nombre}</div>
                        <div className="text-muted text-xs">{est.identificacion}</div>
                      </td>
                      <td>{est.totalActividades}</td>
                      <td>{est.completadas}</td>
                      <td>{est.vencidas}</td>
                      <td>{est.sinEntrega}</td>
                      <td>{est.pendientes}</td>
                      <td>{porcentaje(est.porcentajeFinalizacion)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </section>

      {/* ============ MODAL: CREAR / EDITAR ACTIVIDAD ============ */}
      {formAbierto && (
        <div className="modal-backdrop-blur" role="dialog" aria-modal="true">
          <div className="modal-surface-card">
            <div className="modal-head-row">
              <div>
                <span className="modal-eyebrow">{editando ? 'Edición' : 'Nueva actividad'}</span>
                <h3 className="modal-title">
                  {editando ? 'Editar actividad evaluable' : 'Registrar actividad evaluable'}
                </h3>
              </div>
              <button type="button" className="modal-close-icon-btn" onClick={() => setFormAbierto(false)}>
                <IconClose size={16} />
              </button>
            </div>
            <p className="modal-subtitle">
              Curso: <strong>{cursoActivo?.nombre} ({cursoActivo?.codigo})</strong>
            </p>

            {mensajeForm.texto && (
              <div className={`banner-alert ${mensajeForm.tipo === 'error' ? 'banner-alert-error' : 'banner-alert-ok'}`}>
                {mensajeForm.texto}
              </div>
            )}

            <form onSubmit={guardarActividad} className="form-clean">
              <div className="form-control-item">
                <label htmlFor="act-nombre">Nombre de la actividad (*)</label>
                <input
                  id="act-nombre"
                  type="text"
                  required
                  minLength={2}
                  maxLength={150}
                  value={form.nombre}
                  onChange={e => setForm({ ...form, nombre: e.target.value })}
                  placeholder="Ej. Lección 1: Fundamentos de Scrum"
                />
              </div>

              <div className="form-grid-2">
                <div className="form-control-item">
                  <label htmlFor="act-tipo">Tipo (*)</label>
                  <input
                    id="act-tipo"
                    type="text"
                    required
                    maxLength={40}
                    value={form.tipo}
                    onChange={e => setForm({ ...form, tipo: e.target.value })}
                    placeholder="Tarea, Taller, Lección..."
                  />
                </div>
                <div className="form-control-item">
                  <label htmlFor="act-parcial">Parcial (*)</label>
                  <select
                    id="act-parcial"
                    value={form.parcial}
                    onChange={e => setForm({ ...form, parcial: Number(e.target.value) })}
                  >
                    <option value={1}>Parcial 1</option>
                    <option value={2}>Parcial 2</option>
                  </select>
                </div>
              </div>

              <div className="form-control-item">
                <label htmlFor="act-componente">Componente de evaluación (*)</label>
                <select
                  id="act-componente"
                  value={form.componente}
                  onChange={e => setForm({ ...form, componente: e.target.value })}
                >
                  {COMPONENTES.map(c => (
                    <option key={c.valor} value={c.valor}>{c.etiqueta}</option>
                  ))}
                </select>
              </div>

              <div className="form-grid-2">
                <div className="form-control-item">
                  <label htmlFor="act-fecha">Fecha de entrega</label>
                  <input
                    id="act-fecha"
                    type="datetime-local"
                    value={form.fechaEntrega}
                    onChange={e => setForm({ ...form, fechaEntrega: e.target.value })}
                  />
                </div>
                <div className="form-control-item">
                  <label htmlFor="act-puntaje">Puntaje máximo (*)</label>
                  <input
                    id="act-puntaje"
                    type="number"
                    required
                    min={0.1}
                    max={100}
                    step={0.5}
                    value={form.puntajeMaximo}
                    onChange={e => setForm({ ...form, puntajeMaximo: e.target.value })}
                  />
                </div>
              </div>

              <div className="modal-footer-actions">
                <button
                  type="button"
                  className="btn-pill btn-pill-ghost"
                  onClick={() => setFormAbierto(false)}
                  disabled={guardando}
                >
                  Cancelar
                </button>
                <button type="submit" className="btn-pill btn-pill-dark" disabled={guardando}>
                  {guardando ? 'Guardando...' : editando ? 'Guardar cambios' : 'Crear actividad'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ============ MODAL: CALIFICACIONES ============ */}
      {calificando && (
        <div className="modal-backdrop-blur" role="dialog" aria-modal="true">
          <div className="modal-surface-card modal-ancho">
            <div className="modal-head-row">
              <div>
                <span className="modal-eyebrow">Registro de calificaciones</span>
                <h3 className="modal-title">{calificando.nombre}</h3>
                <p className="modal-subtitle">
                  {calificando.componenteEtiquetaCorta} · Parcial {calificando.parcial} · Puntaje máximo{' '}
                  {numero(calificando.puntajeMaximo)} · Entrega {fecha(calificando.fechaEntrega)}
                </p>
              </div>
              <button
                type="button"
                className="modal-close-icon-btn"
                onClick={() => setCalificando(null)}
              >
                <IconClose size={16} />
              </button>
            </div>

            {mensajeNotas.texto && (
              <div className={`banner-alert ${mensajeNotas.tipo === 'error' ? 'banner-alert-error' : mensajeNotas.tipo === 'ok' ? 'banner-alert-ok' : 'banner-alert-info'}`}>
                {mensajeNotas.texto}
              </div>
            )}

            <div className="banner-alert banner-alert-info">
              Usa <strong>calificada</strong> con una nota entre 0 y {numero(calificando.puntajeMaximo)}.
              Una nota 0 se registra como <strong>nota_cero</strong>. Los estados sin nota no promedian.
            </div>

            {cargandoNotas ? (
              <div className="state-empty-box">
                <p className="text-muted">Cargando estudiantes y calificaciones...</p>
              </div>
            ) : (
              <>
                <div className="modal-results-scroll">
                  <table className="product-data-table compact-table">
                    <thead>
                      <tr>
                        <th>Estudiante</th>
                        <th style={{ minWidth: 170 }}>Estado</th>
                        <th style={{ minWidth: 90 }}>Nota</th>
                        <th style={{ minWidth: 200 }}>Observación</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filas.length === 0 ? (
                        <tr>
                          <td colSpan={4} className="cell-empty-message">
                            No hay estudiantes matriculados activamente en este curso.
                          </td>
                        </tr>
                      ) : (
                        filas.map(fila => {
                          const estadoActual = fila.estado || 'pendiente'
                          return (
                            <tr key={fila.estudianteId}>
                              <td>
                                <div className="cell-student-name">{fila.nombre}</div>
                                <div className="text-muted text-xs">{fila.identificacion}</div>
                                <span className={claseEstado(estadoActual)}>
                                  {etiquetaEstado(estadoActual)}
                                </span>
                              </td>
                              <td>
                                <select
                                  className="input-text-clean input-compacto"
                                  value={estadoActual}
                                  onChange={e => actualizarFila(fila.estudianteId, 'estado', e.target.value)}
                                  aria-label={`Estado de ${fila.nombre}`}
                                >
                                  {ESTADOS.map(estado => (
                                    <option key={estado} value={estado}>{etiquetaEstado(estado)}</option>
                                  ))}
                                </select>
                              </td>
                              <td>
                                <input
                                  type="number"
                                  className="input-text-clean input-compacto"
                                  min={0}
                                  max={fila.puntajeMaximo}
                                  step={0.01}
                                  value={fila.nota ?? ''}
                                  placeholder="—"
                                  onChange={e => actualizarFila(fila.estudianteId, 'nota', e.target.value)}
                                  aria-label={`Nota de ${fila.nombre}`}
                                />
                              </td>
                              <td>
                                <input
                                  type="text"
                                  className="input-text-clean input-compacto"
                                  maxLength={500}
                                  value={fila.observacion ?? ''}
                                  placeholder="Opcional"
                                  onChange={e => actualizarFila(fila.estudianteId, 'observacion', e.target.value)}
                                  aria-label={`Observación de ${fila.nombre}`}
                                />
                              </td>
                            </tr>
                          )
                        })
                      )}
                    </tbody>
                  </table>
                </div>

                <div className="modal-footer-actions modal-footer-split">
                  <span className="text-muted text-xs">
                    {filasModificadas.length} cambio(s) sin guardar
                  </span>
                  <div className="table-actions-row">
                    <button
                      type="button"
                      className="btn-pill btn-pill-ghost"
                      onClick={() => setCalificando(null)}
                      disabled={guardandoNotas}
                    >
                      Cerrar
                    </button>
                    <button
                      type="button"
                      className="btn-pill btn-pill-dark"
                      onClick={guardarCalificaciones}
                      disabled={guardandoNotas || filasModificadas.length === 0}
                    >
                      {guardandoNotas ? 'Guardando...' : 'Guardar calificaciones'}
                    </button>
                  </div>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
