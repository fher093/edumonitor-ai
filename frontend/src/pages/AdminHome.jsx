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

  // Determinar la sección activa
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

  // Lista de cursos y docentes
  const [cursos, setCursos] = useState([])
  const [docentes, setDocentes] = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  // Estado del modal y formulario
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
    resetearFormulario()
    setModalCursoAbierto(true)
  }

  function cerrarModalCurso() {
    setModalCursoAbierto(false)
    resetearFormulario()
  }

  function editarCurso(curso) {
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
      } else {
        await api('/api/cursos', {
          method: 'POST',
          body: payload,
        })
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
      await cargarDatos()
    } catch (err) {
      alert(err.message)
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
        /* ================= VISTA: LISTADO Y ADMINISTRACIÓN DE CURSOS ================= */
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
        </>
      )}
    </div>
  )
}
