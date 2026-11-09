import { useEffect, useState, useMemo } from 'react'
import { useLocation } from 'react-router-dom'
import { useAuth } from '../auth/useAuth.js'
import { api } from '../api/http.js'
import EstadoServicios from '../components/EstadoServicios.jsx'
import ModuloPreview from '../components/ModuloPreview.jsx'
import { IconEdit, IconTrash, IconPlus } from '../components/Icons.jsx'

export default function AdminHome() {
  const { usuario } = useAuth()
  const location = useLocation()

  const rutaActual = location.pathname
  const vista = useMemo(() => {
    if (rutaActual.includes('/auditoria')) return 'auditoria'
    if (rutaActual.includes('/servicios')) return 'servicios'
    if (rutaActual.includes('/dashboard')) return 'dashboard'
    if (rutaActual.includes('/alertas')) return 'alertas'
    if (rutaActual.includes('/analisis')) return 'analisis'
    if (rutaActual.includes('/informe')) return 'informe'
    if (rutaActual.includes('/agente')) return 'agente'
    return 'cursos'
  }, [rutaActual])

  const [cursos, setCursos] = useState([])
  const [docentes, setDocentes] = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [avisoCurso, setAvisoCurso] = useState({ tipo: '', texto: '' })

  const [modalCursoAbierto, setModalCursoAbierto] = useState(false)
  const [modoEdicion, setModoEdicion] = useState(false)
  const [cursoEditandoId, setCursoEditandoId] = useState(null)
  const [formulario, setFormulario] = useState({
    nombre: '',
    codigo: '',
    periodo: 'Periodo Académico 2026-A',
    escalaCalificacion: 20.0,
    docenteId: '',
    paralelo: '',
  })
  const [guardando, setGuardando] = useState(false)
  const [mensajeFormulario, setMensajeFormulario] = useState({ tipo: '', texto: '' })

  async function cargarDatos() {
    setCargando(true)
    setError('')
    try {
      const [listaCursos, listaDocentes] = await Promise.all([
        api('/api/cursos'),
        api('/api/cursos/docentes-disponibles'),
      ])
      setCursos(listaCursos)
      setDocentes(listaDocentes)
      if (listaDocentes.length > 0 && !formulario.docenteId) {
        setFormulario(prev => ({ ...prev, docenteId: String(listaDocentes[0].id) }))
      }
    } catch (err) {
      setError(err.message)
    } finally {
      setCargando(false)
    }
  }

  useEffect(() => {
    cargarDatos()
  }, [])

  useEffect(() => {
    if (!modalCursoAbierto) return
    const overflowPrevio = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    function manejarTecla(e) {
      if (e.key === 'Escape' && !guardando) {
        setModalCursoAbierto(false)
      }
    }

    window.addEventListener('keydown', manejarTecla)
    return () => {
      document.body.style.overflow = overflowPrevio
      window.removeEventListener('keydown', manejarTecla)
    }
  }, [modalCursoAbierto, guardando])

  function resetearFormulario() {
    setModoEdicion(false)
    setCursoEditandoId(null)
    setMensajeFormulario({ tipo: '', texto: '' })
    setFormulario({
      nombre: '',
      codigo: '',
      periodo: 'Periodo Académico 2026-A',
      escalaCalificacion: 20.0,
      docenteId: docentes.length > 0 ? String(docentes[0].id) : '',
      paralelo: '',
    })
  }

  function abrirModalNuevoCurso() {
    setAvisoCurso({ tipo: '', texto: '' })
    resetearFormulario()
    setModalCursoAbierto(true)
  }

  function cerrarModalCurso() {
    setModalCursoAbierto(false)
    resetearFormulario()
  }

  function editarCurso(curso) {
    setAvisoCurso({ tipo: '', texto: '' })
    setModoEdicion(true)
    setCursoEditandoId(curso.id)
    setMensajeFormulario({ tipo: '', texto: '' })
    setFormulario({
      nombre: curso.nombre,
      codigo: curso.codigo,
      periodo: curso.periodo,
      escalaCalificacion: curso.escalaCalificacion,
      docenteId: String(curso.docenteId),
      paralelo: curso.paralelo || '',
    })
    setModalCursoAbierto(true)
  }

  async function guardarCurso(e) {
    e.preventDefault()
    setGuardando(true)
    setMensajeFormulario({ tipo: '', texto: '' })

    const payload = {
      nombre: formulario.nombre.trim(),
      codigo: formulario.codigo.trim(),
      periodo: formulario.periodo.trim(),
      escalaCalificacion: Number(formulario.escalaCalificacion),
      docenteId: Number(formulario.docenteId),
      paralelo: formulario.paralelo.trim() || null,
    }

    try {
      if (modoEdicion) {
        await api(`/api/cursos/${cursoEditandoId}`, {
          method: 'PUT',
          body: payload,
        })
        setAvisoCurso({ tipo: 'ok', texto: 'Curso actualizado correctamente en SQL Server.' })
      } else {
        await api('/api/cursos', {
          method: 'POST',
          body: payload,
        })
        setAvisoCurso({ tipo: 'ok', texto: 'Curso creado y asignado al docente exitosamente.' })
      }
      cerrarModalCurso()
      await cargarDatos()
    } catch (err) {
      setMensajeFormulario({ tipo: 'error', texto: err.message })
    } finally {
      setGuardando(false)
    }
  }

  async function eliminarCurso(curso) {
    if (!window.confirm(`¿Estás seguro de eliminar el curso "${curso.nombre} (${curso.codigo})"? Se eliminarán también sus matrículas.`)) {
      return
    }
    try {
      await api(`/api/cursos/${curso.id}`, { method: 'DELETE' })
      if (cursoEditandoId === curso.id) resetearFormulario()
      setAvisoCurso({ tipo: 'ok', texto: `Curso "${curso.nombre}" eliminado correctamente.` })
      await cargarDatos()
    } catch (err) {
      setAvisoCurso({ tipo: 'error', texto: err.message })
    }
  }

  return (
    <div className="page-container">
      {error && (
        <div className="banner-alert banner-alert-error" style={{ marginBottom: '1.25rem' }}>
          {error}
        </div>
      )}

      {vista === 'servicios' ? (
        <div className="product-surface-card">
          <div className="section-head-split">
            <div>
              <div className="brand-dot-pill">
                <span className="dot-violet" />
                <span className="tag-uta-micro">MONITOREO DE SERVICIOS</span>
              </div>
              <h1 className="display-headline">Salud de Servicios y API</h1>
              <p className="lead-subtitle">Verificación de conectividad con los servicios de la plataforma.</p>
            </div>
          </div>
          <div className="hairline-divider" />
          <EstadoServicios />
        </div>
      ) : vista === 'auditoria' ? (
        <ModuloPreview tipo="auditoria" />
      ) : ['dashboard', 'alertas', 'analisis', 'informe', 'agente'].includes(vista) ? (
        <ModuloPreview tipo={vista} />
      ) : (
        <>
          <section className="product-surface-card">
            <div className="section-head-split">
              <div>
                <div className="brand-dot-pill">
                  <span className="dot-violet" />
                  <span className="tag-uta-micro">ADMINISTRACIÓN ACADÉMICA</span>
                </div>
                <h1 className="display-headline">Administración de Cursos</h1>
                <p className="lead-subtitle">
                  Configuración de asignaturas, códigos y docentes responsables • Administrador: <strong>{usuario?.nombre}</strong>
                </p>
              </div>
              <div className="header-actions-group">
                <button
                  type="button"
                  className="btn-pill btn-pill-dark"
                  onClick={abrirModalNuevoCurso}
                >
                  <IconPlus size={15} /> Crear nuevo curso
                </button>
              </div>
            </div>

            <div className="hairline-divider" />

            {avisoCurso.texto && (
              <div
                className={`banner-alert ${avisoCurso.tipo === 'ok' ? 'banner-alert-ok' : 'banner-alert-error'}`}
                style={{ marginBottom: '1.25rem' }}
              >
                {avisoCurso.texto}
              </div>
            )}

            <div className="table-header-bar">
              <div>
                <h3 className="table-title">Asignaturas en el Sistema ({cursos.length})</h3>
                <p className="text-muted text-sm" style={{ margin: 0 }}>
                  Listado completo de cursos registrados para el período.
                </p>
              </div>
            </div>

            {cargando ? (
              <div className="state-empty-box">
                <p className="text-muted">Cargando cursos...</p>
              </div>
            ) : cursos.length === 0 ? (
              <div className="state-empty-box">
                <p className="text-muted">
                  Aún no hay cursos creados. Haz clic en "+ Crear nuevo curso" para registrar la primera asignatura.
                </p>
              </div>
            ) : (
              <div className="table-scroll-container">
                <table className="product-data-table">
                  <thead>
                    <tr>
                      <th>Código</th>
                      <th>Asignatura</th>
                      <th>Paralelo</th>
                      <th>Período</th>
                      <th>Docente Responsable</th>
                      <th>Matriculados</th>
                      <th style={{ textAlign: 'right' }}>Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {cursos.map(c => (
                      <tr key={c.id}>
                        <td><span className="mono-code-tag">{c.codigo}</span></td>
                        <td><strong className="cell-student-name">{c.nombre}</strong></td>
                        <td>{c.paralelo || <span className="text-muted">-</span>}</td>
                        <td>{c.periodo}</td>
                        <td>{c.docenteNombre}</td>
                        <td>
                          <span className="status-pill status-pill-neutral">
                            {c.totalEstudiantes} estudiantes
                          </span>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <div className="table-actions-row">
                            <button
                              type="button"
                              className="btn-pill btn-pill-ghost btn-sm"
                              onClick={() => editarCurso(c)}
                              title="Editar curso"
                            >
                              <IconEdit size={14} /> Editar
                            </button>
                            <button
                              type="button"
                              className="btn-pill btn-desmatr-danger btn-sm"
                              onClick={() => eliminarCurso(c)}
                              title="Eliminar curso"
                            >
                              <IconTrash size={14} /> Eliminar
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {modalCursoAbierto && (
            <div
              className="modal-backdrop-blur"
              role="dialog"
              aria-modal="true"
              aria-labelledby="modal-curso-titulo"
              onClick={e => {
                if (e.target === e.currentTarget && !guardando) cerrarModalCurso()
              }}
            >
              <div className="modal-surface-card modal-curso-hu02">
                <div className="modal-head-row">
                  <div>
                    <h3 id="modal-curso-titulo" className="modal-title" style={{ marginTop: 0 }}>
                      {modoEdicion ? 'Editar curso' : 'Crear nuevo curso'}
                    </h3>
                  </div>
                  <div className="modal-hu-badge-cluster">
                    <span className="hu-wireframe-badge">HU-02 • Sprint 1</span>
                    <button
                      type="button"
                      className="modal-close-icon-btn"
                      onClick={cerrarModalCurso}
                      disabled={guardando}
                      aria-label="Cerrar ventana"
                      title="Cerrar"
                    >
                      ✕
                    </button>
                  </div>
                </div>

                <p className="modal-subtitle" style={{ marginBottom: 0 }}>
                  Formulario de registro y administración académica para el docente en sesión
                </p>

                <div className="hairline-divider" style={{ margin: '14px 0 20px' }} />

                {mensajeFormulario.texto && (
                  <div
                    className={`banner-alert ${mensajeFormulario.tipo === 'ok' ? 'banner-alert-ok' : 'banner-alert-error'}`}
                    style={{ marginBottom: '1.25rem' }}
                  >
                    {mensajeFormulario.texto}
                  </div>
                )}

                <form onSubmit={guardarCurso} className="form-clean">
                  <div className="form-control-item">
                    <label htmlFor="curso-nombre">
                      Nombre de la asignatura / curso: (*)
                    </label>
                    <input
                      id="curso-nombre"
                      type="text"
                      required
                      autoFocus
                      placeholder="Ej. Bases de Datos"
                      value={formulario.nombre}
                      onChange={e => setFormulario({ ...formulario, nombre: e.target.value })}
                    />
                  </div>

                  <div className="form-grid-split">
                    <div className="form-control-item">
                      <label htmlFor="curso-codigo">
                        Código de curso: (*)
                      </label>
                      <input
                        id="curso-codigo"
                        type="text"
                        required
                        placeholder="EJ. BD-301"
                        value={formulario.codigo}
                        onChange={e => setFormulario({ ...formulario, codigo: e.target.value })}
                      />
                      <small className="form-hint-note">
                        * Único por docente y período
                      </small>
                    </div>

                    <div className="form-control-item">
                      <label htmlFor="curso-periodo">
                        Período académico: (*)
                      </label>
                      <select
                        id="curso-periodo"
                        value={formulario.periodo}
                        onChange={e => setFormulario({ ...formulario, periodo: e.target.value })}
                      >
                        <option value="Periodo Académico 2026-A">Periodo Académico 2026-A</option>
                        <option value="Periodo 2026-A">Periodo 2026-A</option>
                        <option value="Periodo Académico 2026-B">Periodo Académico 2026-B</option>
                        <option value="Octubre 2026 - Febrero 2027">Octubre 2026 - Febrero 2027</option>
                      </select>
                    </div>
                  </div>

                  <div className="form-control-item">
                    <label htmlFor="curso-escala">
                      Escala de calificación: (*)
                    </label>
                    <select
                      id="curso-escala"
                      value={formulario.escalaCalificacion}
                      onChange={e => setFormulario({ ...formulario, escalaCalificacion: Number(e.target.value) })}
                    >
                      <option value={20.0}>Escala UTA (0 a 20 puntos) - Aprobación mínima &gt;= 14</option>
                      <option value={10.0}>Escala UTA Parcial (0 a 10 puntos) - Aprobación mínima &gt;= 7</option>
                    </select>
                  </div>

                  <div className="form-control-item">
                    <label htmlFor="curso-docente">
                      Docente responsable:
                    </label>
                    <div className="docente-select-wrapper">
                      <select
                        id="curso-docente"
                        required
                        value={formulario.docenteId}
                        onChange={e => setFormulario({ ...formulario, docenteId: e.target.value })}
                      >
                        {docentes.length === 0 ? (
                          <option value="">No hay docentes registrados</option>
                        ) : (
                          docentes.map(d => (
                            <option key={d.id} value={d.id}>
                              {d.nombre} ({d.correo})
                            </option>
                          ))
                        )}
                      </select>
                      <span className="docente-assigned-tag">
                        [Asignado al docente seleccionado]
                      </span>
                    </div>
                  </div>

                  <div className="form-control-item">
                    <label htmlFor="curso-paralelo">
                      Paralelo o descripción breve: (Opcional)
                    </label>
                    <input
                      id="curso-paralelo"
                      type="text"
                      placeholder="Ej. Paralelo 'A' - Modalidad Presencial"
                      value={formulario.paralelo}
                      onChange={e => setFormulario({ ...formulario, paralelo: e.target.value })}
                    />
                  </div>

                  <div className="form-actions-cluster" style={{ marginTop: '0.5rem' }}>
                    <button
                      type="submit"
                      className="btn-pill btn-pill-dark"
                      disabled={guardando || docentes.length === 0}
                    >
                      {guardando ? 'Guardando...' : (modoEdicion ? 'Actualizar curso' : 'Guardar curso')}
                    </button>
                    <button
                      type="button"
                      className="btn-pill btn-pill-ghost"
                      onClick={cerrarModalCurso}
                      disabled={guardando}
                    >
                      Cancelar / Volver
                    </button>
                  </div>

                  <div className="modal-hu-footer-meta">
                    <span>* Criterios 1, 2, 3 y 4 de HU-02 implementados en el formulario.</span>
                    <strong>SQL Server Persistente</strong>
                  </div>
                </form>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  )
}
