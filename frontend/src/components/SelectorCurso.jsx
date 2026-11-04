import { IconSearch } from './Icons.jsx'

/**
 * Barra contextual compartida: período (HU-09), búsqueda de asignatura y
 * selector de curso activo. Los cursos ya vienen autorizados por la API.
 */
export default function SelectorCurso({ cursosHook, children }) {
  const {
    cursos,
    periodos,
    periodo,
    setPeriodo,
    q,
    setQ,
    cargando,
    cursoId,
    cursoActivo,
    seleccionarCurso,
  } = cursosHook

  return (
    <div className="course-context-bar">
      <div className="course-context-fields">
        <label className="mini-select-field">
          <span>Período</span>
          <select value={periodo} onChange={e => setPeriodo(e.target.value)}>
            <option value="">Todos los períodos</option>
            {periodos.map(p => (
              <option key={p} value={p}>{p}</option>
            ))}
          </select>
        </label>

        <label className="mini-select-field">
          <span>Asignatura</span>
          <select
            value={cursoId ?? ''}
            onChange={e => seleccionarCurso(e.target.value)}
            disabled={cargando || cursos.length === 0}
          >
            {cursos.length === 0 && <option value="">Sin cursos disponibles</option>}
            {cursos.map(c => (
              <option key={c.id} value={c.id}>
                {c.nombre} ({c.codigo})
              </option>
            ))}
          </select>
        </label>

        <div className="toolbar-search-field selector-search-field">
          <IconSearch className="search-field-icon" size={15} />
          <input
            type="text"
            className="input-text-clean"
            placeholder="Buscar por nombre, código o paralelo..."
            value={q}
            onChange={e => setQ(e.target.value)}
            aria-label="Buscar asignatura"
          />
        </div>
      </div>

      <div className="course-context-meta">
        {cursoActivo && (
          <>
            <span className="status-pill status-pill-neutral">{cursoActivo.periodo}</span>
            <span className="status-pill status-pill-neutral">
              {cursoActivo.totalEstudiantes} estudiantes
            </span>
            <span className="status-pill status-pill-active">Curso activo</span>
          </>
        )}
        {children}
      </div>
    </div>
  )
}
