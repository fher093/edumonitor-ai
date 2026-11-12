import { useNavigate } from 'react-router-dom'
import { useCursoActivo } from '../cursos/CursoContext.jsx'
import { Cargando, PageHeader, Vacio } from '../components/ui.jsx'
import { IconExterno, IconFlecha, IconSync } from '../components/Icons.jsx'
import { fechaHora } from '../utils/formato.js'

/** Mis cursos (HU-04 / HU-06): asignaturas sincronizadas desde Moodle; abrir una la vuelve el curso activo. */
export default function MisCursos() {
  const { cursos, cursoActivo, seleccionarCurso, cargando } = useCursoActivo()
  const navigate = useNavigate()

  function abrir(id) {
    seleccionarCurso(id)
    navigate('/inicio')
  }

  return (
    <div className="ws-page ws-page-scroll">
      <PageHeader
        eyebrow="Mis asignaturas"
        titulo="Cursos asignados"
        descripcion="Se sincronizan automáticamente desde Moodle por tu correo. Abre uno para trabajar con él."
      />
      {cargando && cursos.length === 0 ? (
        <Cargando texto="Cargando asignaturas..." />
      ) : cursos.length === 0 ? (
        <Vacio titulo="Aún no tienes asignaturas">
          Si no ves tus cursos, verifica que tu correo en Moodle sea el mismo de tu cuenta de EduMonitor.
        </Vacio>
      ) : (
        <div className="ws-course-grid">
          {cursos.map(c => {
            const activo = c.id === cursoActivo?.id
            return (
              <article key={c.id} className={`ws-course ${activo ? 'is-active' : ''}`}>
                <div className="ws-course-top">
                  <span className="ws-switcher-mark">{c.codigo.slice(0, 2)}</span>
                  <span className="ws-course-code">{c.codigo}</span>
                  {activo && <span className="ws-pill ws-pill-brand">Activo</span>}
                </div>
                <h2>{c.nombre}</h2>
                <dl className="ws-course-meta">
                  <div>
                    <dt>Período</dt>
                    <dd>{c.periodo}</dd>
                  </div>
                  <div>
                    <dt>Estudiantes</dt>
                    <dd>{c.totalEstudiantes}</dd>
                  </div>
                  <div>
                    <dt>Escala</dt>
                    <dd>/{c.escalaCalificacion}</dd>
                  </div>
                </dl>
                <span className="ws-muted ws-small ws-course-sync">
                  <IconSync size={12} />{' '}
                  {c.origen === 'moodle' ? `Moodle · ${fechaHora(c.fechaSincronizacion)}` : 'Datos locales'}
                </span>
                <div className="ws-course-actions">
                  <button type="button" className="ws-btn ws-btn-dark ws-btn-sm" onClick={() => abrir(c.id)}>
                    Abrir <IconFlecha size={13} />
                  </button>
                  {c.urlMoodle && (
                    <a className="ws-btn ws-btn-ghost ws-btn-sm" href={c.urlMoodle} target="_blank" rel="noopener noreferrer">
                      <IconExterno size={13} /> Moodle
                    </a>
                  )}
                </div>
              </article>
            )
          })}
        </div>
      )}
    </div>
  )
}
