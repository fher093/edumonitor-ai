import { useEffect, useState, useMemo } from 'react'
import { useLocation, Link } from 'react-router-dom'
import { useAuth } from '../auth/useAuth.js'
import { api } from '../api/http.js'
import ModuloPreview from '../components/ModuloPreview.jsx'
import {
  IconSearch,
  IconPlus,
  IconCursos,
} from '../components/Icons.jsx'

export default function DocenteHome() {
  const { usuario } = useAuth()
  const location = useLocation()

  // Determinar la vista activa según la ruta
  const rutaActual = location.pathname
  const vista = useMemo(() => {
    if (rutaActual === '/matricula') return 'matricula'
    if (rutaActual === '/cursos') return 'cursos'
    if (rutaActual === '/dashboard') return 'dashboard'
    if (rutaActual === '/alertas') return 'alertas'
    if (rutaActual === '/analisis') return 'analisis'
    if (rutaActual === '/informe') return 'informe'
    if (rutaActual === '/agente') return 'agente'
    return 'matricula'
  }, [rutaActual])

  // Cursos del docente
  const [cursos, setCursos] = useState([])
  const [cargandoCursos, setCargandoCursos] = useState(true)
  const [errorCursos, setErrorCursos] = useState('')
  const [cursoActivoId, setCursoActivoId] = useState(() => {
    return Number(localStorage.getItem('edumonitor_curso_activo')) || null
  })

  // Matrículas y Estudiantes
  const [matriculas, setMatriculas] = useState([])
  const [cargandoMatriculas, setCargandoMatriculas] = useState(false)
  const [errorMatriculas, setErrorMatriculas] = useState('')

  // Filtro local en tabla de matriculados
  const [filtroEstudiantes, setFiltroEstudiantes] = useState('')

  // Modal Matricular Alumno Existente
  const [modalMatricularAbierto, setModalMatricularAbierto] = useState(false)
  const [busquedaGlobal, setBusquedaGlobal] = useState('')
  const [resultadosBusquedaGlobal, setResultadosBusquedaGlobal] = useState([])
  const [buscandoGlobal, setBuscandoGlobal] = useState(false)
  const [mensajeMatricula, setMensajeMatricula] = useState({ tipo: '', texto: '' })

  // Modal Registrar Nuevo Estudiante
  const [modalEstudianteAbierto, setModalEstudianteAbierto] = useState(false)
  const [formEstudiante, setFormEstudiante] = useState({ identificacion: '', nombre: '', correo: '' })
  const [guardandoEstudiante, setGuardandoEstudiante] = useState(false)
  const [errorModalEstudiante, setErrorModalEstudiante] = useState('')

  // Cargar cursos asignados al docente
  async function cargarCursos() {
    setCargandoCursos(true)
    setErrorCursos('')
    try {
      const data = await api('/api/cursos')
      setCursos(data)
      if (data.length > 0) {
        if (!cursoActivoId || !data.some(c => c.id === cursoActivoId)) {
          seleccionarCurso(data[0].id)
        }
      } else {
        setCursoActivoId(null)
        localStorage.removeItem('edumonitor_curso_activo')
      }
    } catch (err) {
      setErrorCursos(err.message)
    } finally {
      setCargandoCursos(false)
    }
  }

  // Cargar matrículas del curso activo
  async function cargarMatriculas(cursoId) {
    if (!cursoId) {
      setMatriculas([])
      return
    }
    setCargandoMatriculas(true)
    setErrorMatriculas('')
    try {
      const data = await api(`/api/cursos/${cursoId}/matriculas`)
      setMatriculas(data)
    } catch (err) {
      setErrorMatriculas(err.message)
    } finally {
      setCargandoMatriculas(false)
    }
  }

  useEffect(() => {
    cargarCursos()
  }, [])

  useEffect(() => {
    if (cursoActivoId) {
      cargarMatriculas(cursoActivoId)
    }
  }, [cursoActivoId])

  function seleccionarCurso(id) {
    const idNum = Number(id)
    setCursoActivoId(idNum)
    localStorage.setItem('edumonitor_curso_activo', String(idNum))
  }

  // Buscar estudiantes en catálogo para matricular
  async function buscarEstudiantesGlobal(e) {
    if (e) e.preventDefault()
    if (!busquedaGlobal.trim()) return
    setBuscandoGlobal(true)
    setMensajeMatricula({ tipo: '', texto: '' })
    try {
      const data = await api(`/api/estudiantes?q=${encodeURIComponent(busquedaGlobal.trim())}`)
      const idsMatriculados = new Set(matriculas.map(m => m.estudianteId))
      setResultadosBusquedaGlobal(data.filter(est => !idsMatriculados.has(est.id)))
      if (data.length === 0) {
        setMensajeMatricula({ tipo: 'info', texto: 'No se encontraron estudiantes con ese criterio.' })
      }
    } catch (err) {
      setMensajeMatricula({ tipo: 'error', texto: err.message })
    } finally {
      setBuscandoGlobal(false)
    }
  }

  // Matricular estudiante existente
  async function matricularEstudiante(estudianteId, nombreEst) {
    if (!cursoActivoId) return
    try {
      await api(`/api/cursos/${cursoActivoId}/matriculas`, {
        method: 'POST',
        body: { estudianteId },
      })
      setResultadosBusquedaGlobal(prev => prev.filter(e => e.id !== estudianteId))
      setMensajeMatricula({ tipo: 'ok', texto: `Estudiante ${nombreEst || ''} matriculado con éxito.` })
      await cargarMatriculas(cursoActivoId)
      await cargarCursos()
    } catch (err) {
      setMensajeMatricula({ tipo: 'error', texto: err.message })
    }
  }

  // Desmatricular estudiante
  async function desmatricular(estudianteId, nombre) {
    if (!window.confirm(`¿Confirmas la desmatriculación de "${nombre}" del curso actual?`)) return
    try {
      await api(`/api/cursos/${cursoActivoId}/matriculas/${estudianteId}`, { method: 'DELETE' })
      await cargarMatriculas(cursoActivoId)
      await cargarCursos()
    } catch (err) {
      alert(`Error al desmatricular: ${err.message}`)
    }
  }

  // Registrar nuevo estudiante y matricularlo directamente
  async function registrarYMatricular(e) {
    e.preventDefault()
    setGuardandoEstudiante(true)
    setErrorModalEstudiante('')
    try {
      const nuevoEst = await api('/api/estudiantes', {
        method: 'POST',
        body: formEstudiante,
      })
      if (cursoActivoId) {
        await api(`/api/cursos/${cursoActivoId}/matriculas`, {
          method: 'POST',
          body: { estudianteId: nuevoEst.id },
        })
      }
      setModalEstudianteAbierto(false)
      setFormEstudiante({ identificacion: '', nombre: '', correo: '' })
      await cargarMatriculas(cursoActivoId)
      await cargarCursos()
    } catch (err) {
      setErrorModalEstudiante(err.message)
    } finally {
      setGuardandoEstudiante(false)
    }
  }

  const cursoActivo = cursos.find(c => c.id === cursoActivoId)

  // Filtrado de estudiantes matriculados en vivo
  const matriculasFiltradas = useMemo(() => {
    if (!filtroEstudiantes.trim()) return matriculas
    const q = filtroEstudiantes.toLowerCase().trim()
    return matriculas.filter(m =>
      (m.identificacion && m.identificacion.toLowerCase().includes(q)) ||
      (m.nombre && m.nombre.toLowerCase().includes(q)) ||
      (m.correo && m.correo.toLowerCase().includes(q))
    )
  }, [matriculas, filtroEstudiantes])

  return (
    <div className="page-container">
      {errorCursos && (
        <div className="banner-alert banner-alert-error">
          {errorCursos}
        </div>
      )}

      {/* Módulos de analítica y acompañamiento */}
      {['dashboard', 'alertas', 'analisis', 'informe', 'agente'].includes(vista) ? (
        <ModuloPreview tipo={vista} cursoActivo={cursoActivo} />
      ) : vista === 'cursos' ? (
        /* ================= VISTA: MIS CURSOS ================= */
        <section className="product-surface-card">
          <div className="section-head-split">
            <div>
              <div className="brand-dot-pill">
                <span className="dot-violet" />
                <span className="tag-uta-micro">MIS ASIGNATURAS</span>
              </div>
              <h1 className="display-headline">Mis Cursos Autorizados</h1>
              <p className="lead-subtitle">
                Asignaturas asignadas para el seguimiento del rendimiento académico • Docente: <strong>{usuario?.nombre}</strong>
              </p>
            </div>
            <div className="stat-pill-counter">
              <span className="stat-number">{cursos.length}</span>
              <span className="stat-label">Cursos asignados</span>
            </div>
          </div>

          <div className="hairline-divider" />

          {cargandoCursos ? (
            <div className="state-empty-box">
              <p className="text-muted">Cargando tus asignaturas...</p>
            </div>
          ) : cursos.length === 0 ? (
            <div className="state-empty-box">
              <div className="empty-icon-circle">
                <IconCursos size={26} />
              </div>
              <h3 className="empty-title">Aún no tienes asignaturas asignadas</h3>
              <p className="text-muted">
                El administrador técnico debe crear y asignarte los cursos correspondientes para el período académico actual.
              </p>
            </div>
          ) : (
            <>
              <div className="courses-grid-cards">
                {cursos.map(curso => {
                  const esActivo = curso.id === cursoActivoId
                  return (
                    <div key={curso.id} className={`course-product-card ${esActivo ? 'active-course-card' : ''}`}>
                      <div className="course-card-top">
                        <div className="course-tag-row">
                          <span className="mono-code-tag">{curso.codigo}</span>
                          {esActivo && <span className="status-pill status-pill-active">Activo</span>}
                        </div>
                        <h3 className="course-card-title">{curso.nombre}</h3>
                        <div className="course-spec-list">
                          {curso.paralelo && (
                            <div className="spec-row">
                              <span className="spec-key">Paralelo:</span>
                              <span className="spec-val">{curso.paralelo}</span>
                            </div>
                          )}
                          <div className="spec-row">
                            <span className="spec-key">Período:</span>
                            <span className="spec-val">{curso.periodo}</span>
                          </div>
                          <div className="spec-row">
                            <span className="spec-key">Escala:</span>
                            <span className="spec-val">sobre {curso.escalaCalificacion} pts</span>
                          </div>
                          <div className="spec-row">
                            <span className="spec-key">Matriculados:</span>
                            <span className="spec-val bold">{curso.totalEstudiantes} estudiantes</span>
                          </div>
                        </div>
                      </div>

                      <div className="course-card-actions">
                        <button
                          type="button"
                          className={`btn-pill btn-sm ${esActivo ? 'btn-pill-dark' : 'btn-pill-ghost'}`}
                          onClick={() => seleccionarCurso(curso.id)}
                        >
                          {esActivo ? 'Curso activo' : 'Seleccionar'}
                        </button>
                        <Link
                          to="/matricula"
                          onClick={() => seleccionarCurso(curso.id)}
                          className="btn-pill btn-pill-ghost btn-pill-link btn-sm"
                          title="Gestionar matrícula"
                        >
                          Gestionar matrícula
                        </Link>
                      </div>
                    </div>
                  )
                })}
              </div>

              <div className="footnote-meta">
                <span>La información se sincroniza automáticamente con tu cuenta.</span>
              </div>
            </>
          )}
        </section>
      ) : (
        /* ================= VISTA: REGISTRO Y MATRÍCULA ================= */
        <section className="product-surface-card">
          {/* Cabecera Principal */}
          <div className="section-head-split">
            <div>
              <div className="brand-dot-pill">
                <span className="dot-violet" />
                <span className="tag-uta-micro">GESTIÓN DE MATRÍCULA</span>
              </div>
              <h1 className="display-headline">Registro y Matrícula de Estudiantes</h1>
              <p className="lead-subtitle">
                Administración de estudiantes y nómina oficial por asignatura • Docente: <strong>{usuario?.nombre}</strong>
              </p>
            </div>

            {/* Selector de Asignatura Integrado */}
            <div className="course-switcher-pill-box">
              <label htmlFor="course-select-switcher" className="course-switcher-label">
                Asignatura activa:
              </label>
              {cargandoCursos ? (
                <span className="text-muted text-sm">Cargando cursos...</span>
              ) : cursos.length === 0 ? (
                <span className="text-muted text-sm">Sin cursos asignados</span>
              ) : (
                <select
                  id="course-select-switcher"
                  className="course-switcher-select"
                  value={cursoActivoId || ''}
                  onChange={e => seleccionarCurso(e.target.value)}
                >
                  {cursos.map(c => (
                    <option key={c.id} value={c.id}>
                      {c.nombre} ({c.codigo})
                    </option>
                  ))}
                </select>
              )}
            </div>
          </div>

          <div className="hairline-divider" />

          {/* Barra de Acciones y Búsqueda */}
          <div className="product-toolbar-row">
            <div className="toolbar-search-field">
              <IconSearch className="search-field-icon" size={16} />
              <input
                id="search-enrolled-input"
                type="text"
                className="input-text-clean"
                placeholder="Buscar estudiante por nombre o cédula..."
                value={filtroEstudiantes}
                onChange={e => setFiltroEstudiantes(e.target.value)}
              />
              {filtroEstudiantes && (
                <button
                  type="button"
                  className="search-clear-btn"
                  onClick={() => setFiltroEstudiantes('')}
                  title="Limpiar búsqueda"
                >
                  ✕
                </button>
              )}
            </div>

            <div className="toolbar-buttons-cluster">
              <button
                type="button"
                className="btn-pill btn-pill-dark"
                onClick={() => setModalEstudianteAbierto(true)}
                disabled={!cursoActivo}
              >
                <IconPlus size={15} />
                <span>Nuevo Estudiante</span>
              </button>

              <button
                type="button"
                className="btn-pill btn-pill-ghost"
                onClick={() => {
                  setModalMatricularAbierto(true)
                  setMensajeMatricula({ tipo: '', texto: '' })
                  setResultadosBusquedaGlobal([])
                  setBusquedaGlobal('')
                }}
                disabled={!cursoActivo}
              >
                <span>Matricular Alumno</span>
              </button>
            </div>
          </div>

          {errorMatriculas && (
            <div className="banner-alert banner-alert-error" style={{ marginBottom: '1.25rem' }}>
              {errorMatriculas}
            </div>
          )}

          {/* Tabla de Estudiantes Matriculados */}
          <div className="table-product-wrapper">
            <div className="table-header-bar">
              <h3 className="table-title">Nómina del Curso</h3>
              {cursoActivo && (
                <span className="status-pill status-pill-neutral">
                  {matriculas.length} {matriculas.length === 1 ? 'estudiante matriculado' : 'estudiantes matriculados'}
                </span>
              )}
            </div>

            {cargandoMatriculas ? (
              <div className="state-empty-box">
                <p className="text-muted">Cargando la nómina del curso...</p>
              </div>
            ) : matriculas.length === 0 ? (
              <div className="state-empty-box">
                <p className="text-muted" style={{ margin: '1rem 0' }}>
                  No hay estudiantes matriculados en <strong>{cursoActivo?.nombre || 'este curso'}</strong> aún.
                  <br />
                  Registra un nuevo alumno o matricula uno existente con los botones superiores.
                </p>
              </div>
            ) : (
              <div className="table-scroll-container">
                <table className="product-data-table">
                  <thead>
                    <tr>
                      <th style={{ width: '22%' }}>Identificación</th>
                      <th style={{ width: '38%' }}>Nombre del Estudiante</th>
                      <th style={{ width: '26%' }}>Correo Institucional</th>
                      <th style={{ width: '14%', textAlign: 'right' }}>Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {matriculasFiltradas.length === 0 ? (
                      <tr>
                        <td colSpan={4} className="cell-empty-message">
                          No se encontraron estudiantes coincidentes con "{filtroEstudiantes}".
                        </td>
                      </tr>
                    ) : (
                      matriculasFiltradas.map(m => (
                        <tr key={m.id}>
                          <td>
                            <span className="mono-id-badge">{m.identificacion}</span>
                          </td>
                          <td className="cell-student-name">{m.nombre}</td>
                          <td className="cell-student-email">{m.correo}</td>
                          <td style={{ textAlign: 'right' }}>
                            <button
                              type="button"
                              className="btn-pill btn-desmatr-danger btn-sm"
                              onClick={() => desmatricular(m.estudianteId, m.nombre)}
                              title={`Desmatricular a ${m.nombre}`}
                            >
                              Desmatricular
                            </button>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            )}

            <div className="table-footer-meta">
              <div className="footnote-meta">
                <span>La nómina se actualiza automáticamente al registrar o desmatricular estudiantes.</span>
              </div>
            </div>
          </div>
        </section>
      )}

      {/* ================= MODAL: REGISTRAR NUEVO ESTUDIANTE ================= */}
      {modalEstudianteAbierto && (
        <div className="modal-backdrop-blur" role="dialog" aria-modal="true">
          <div className="modal-surface-card">
            <div className="modal-head-row">
              <div>
                <span className="modal-eyebrow">Registro académico</span>
                <h3 className="modal-title">Registrar Nuevo Estudiante</h3>
              </div>
              <button
                type="button"
                className="modal-close-icon-btn"
                onClick={() => setModalEstudianteAbierto(false)}
              >
                ✕
              </button>
            </div>
            <p className="modal-subtitle">
              El estudiante se registrará en el sistema académico y se matriculará en <strong>{cursoActivo?.nombre} ({cursoActivo?.codigo})</strong>.
            </p>

            {errorModalEstudiante && (
              <div className="banner-alert banner-alert-error" style={{ marginBottom: '1rem' }}>
                {errorModalEstudiante}
              </div>
            )}

            <form onSubmit={registrarYMatricular} className="form-clean">
              <div className="form-control-item">
                <label htmlFor="modal-id">Identificación / Cédula (*)</label>
                <input
                  id="modal-id"
                  type="text"
                  required
                  placeholder="Ej. 1804567890"
                  value={formEstudiante.identificacion}
                  onChange={e => setFormEstudiante({ ...formEstudiante, identificacion: e.target.value })}
                />
              </div>

              <div className="form-control-item">
                <label htmlFor="modal-nom">Nombres y Apellidos (*)</label>
                <input
                  id="modal-nom"
                  type="text"
                  required
                  placeholder="Ej. Carlos Fernando López Gómez"
                  value={formEstudiante.nombre}
                  onChange={e => setFormEstudiante({ ...formEstudiante, nombre: e.target.value })}
                />
              </div>

              <div className="form-control-item">
                <label htmlFor="modal-mail">Correo Institucional (*)</label>
                <input
                  id="modal-mail"
                  type="email"
                  required
                  placeholder="Ej. clopez@uta.edu.ec"
                  value={formEstudiante.correo}
                  onChange={e => setFormEstudiante({ ...formEstudiante, correo: e.target.value })}
                />
              </div>

              <div className="modal-footer-actions">
                <button
                  type="button"
                  className="btn-pill btn-pill-ghost"
                  onClick={() => setModalEstudianteAbierto(false)}
                  disabled={guardandoEstudiante}
                >
                  Cancelar
                </button>
                <button type="submit" className="btn-pill btn-pill-dark" disabled={guardandoEstudiante}>
                  {guardandoEstudiante ? 'Registrando...' : 'Registrar y Matricular'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ================= MODAL: MATRICULAR ALUMNO EXISTENTE ================= */}
      {modalMatricularAbierto && (
        <div className="modal-backdrop-blur" role="dialog" aria-modal="true">
          <div className="modal-surface-card" style={{ maxWidth: 620 }}>
            <div className="modal-head-row">
              <div>
                <span className="modal-eyebrow">Catálogo institucional</span>
                <h3 className="modal-title">Matricular Alumno en {cursoActivo?.codigo}</h3>
              </div>
              <button
                type="button"
                className="modal-close-icon-btn"
                onClick={() => setModalMatricularAbierto(false)}
              >
                ✕
              </button>
            </div>
            <p className="modal-subtitle">
              Busca alumnos registrados en el sistema universitario para matricularlos en este curso:
            </p>

            {mensajeMatricula.texto && (
              <div
                className={`banner-alert ${mensajeMatricula.tipo === 'error' ? 'banner-alert-error' : mensajeMatricula.tipo === 'ok' ? 'banner-alert-ok' : 'banner-alert-info'}`}
                style={{ marginBottom: '1rem' }}
              >
                {mensajeMatricula.texto}
              </div>
            )}

            <form onSubmit={buscarEstudiantesGlobal} className="search-form-modal">
              <input
                type="text"
                placeholder="Buscar por cédula o apellido..."
                value={busquedaGlobal}
                onChange={e => setBusquedaGlobal(e.target.value)}
                className="input-text-clean"
                autoFocus
              />
              <button type="submit" className="btn-pill btn-pill-dark" disabled={buscandoGlobal}>
                {buscandoGlobal ? 'Buscando...' : 'Buscar'}
              </button>
            </form>

            {resultadosBusquedaGlobal.length > 0 && (
              <div className="modal-results-scroll">
                <table className="product-data-table" style={{ fontSize: '0.86rem' }}>
                  <thead>
                    <tr>
                      <th>Identificación</th>
                      <th>Estudiante</th>
                      <th style={{ textAlign: 'right' }}>Acción</th>
                    </tr>
                  </thead>
                  <tbody>
                    {resultadosBusquedaGlobal.map(est => (
                      <tr key={est.id}>
                        <td><span className="mono-id-badge">{est.identificacion}</span></td>
                        <td>
                          <div className="cell-student-name">{est.nombre}</div>
                          <div className="text-muted text-xs">{est.correo}</div>
                        </td>
                        <td style={{ textAlign: 'right' }}>
                          <button
                            type="button"
                            className="btn-pill btn-pill-ghost btn-sm"
                            onClick={() => matricularEstudiante(est.id, est.nombre)}
                          >
                            + Matricular
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <div className="modal-footer-actions">
              <button
                type="button"
                className="btn-pill btn-pill-ghost"
                onClick={() => setModalMatricularAbierto(false)}
              >
                Cerrar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
