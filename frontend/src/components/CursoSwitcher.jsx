import { useEffect, useRef, useState } from 'react'
import { useCursoActivo } from '../cursos/CursoContext.jsx'
import { IconCheck, IconChevronDown, IconSearch } from './Icons.jsx'

/**
 * Selector del curso activo, en la cabecera del menú lateral (patrón "workspace switcher").
 * Incluye el filtro de período y la búsqueda de asignaturas (HU-09).
 */
export default function CursoSwitcher({ colapsado }) {
  const { cursos, cursoActivo, seleccionarCurso, periodos, periodo, setPeriodo, q, setQ, cargando } = useCursoActivo()
  const [abierto, setAbierto] = useState(false)
  const ref = useRef(null)

  useEffect(() => {
    if (!abierto) return
    const fuera = e => ref.current && !ref.current.contains(e.target) && setAbierto(false)
    const escape = e => e.key === 'Escape' && setAbierto(false)
    document.addEventListener('mousedown', fuera)
    document.addEventListener('keydown', escape)
    return () => {
      document.removeEventListener('mousedown', fuera)
      document.removeEventListener('keydown', escape)
    }
  }, [abierto])

  const codigo = cursoActivo?.codigo ?? '—'

  return (
    <div className="ws-switcher" ref={ref}>
      <button
        type="button"
        className="ws-switcher-btn"
        onClick={() => setAbierto(a => !a)}
        aria-haspopup="listbox"
        aria-expanded={abierto}
        title={cursoActivo ? `${cursoActivo.nombre} (${cursoActivo.codigo})` : 'Seleccionar asignatura'}
      >
        <span className="ws-switcher-mark">{codigo.slice(0, 2)}</span>
        {!colapsado && (
          <>
            <span className="ws-switcher-text">
              <span className="ws-switcher-name">{cargando && !cursoActivo ? 'Cargando...' : cursoActivo?.nombre ?? 'Sin asignaturas'}</span>
              <span className="ws-switcher-meta">
                {cursoActivo ? `${cursoActivo.codigo} · ${cursoActivo.periodo}` : 'Elige una asignatura'}
              </span>
            </span>
            <IconChevronDown size={15} />
          </>
        )}
      </button>

      {abierto && (
        <div className="ws-switcher-pop">
          <div className="ws-switcher-filters">
            <label className="ws-search ws-search-dark">
              <IconSearch size={14} />
              <input
                type="search"
                value={q}
                onChange={e => setQ(e.target.value)}
                placeholder="Buscar asignatura..."
                aria-label="Buscar asignatura"
                autoFocus
              />
            </label>
            <select value={periodo} onChange={e => setPeriodo(e.target.value)} aria-label="Período académico">
              <option value="">Todos los períodos</option>
              {periodos.map(p => (
                <option key={p} value={p}>{p}</option>
              ))}
            </select>
          </div>
          <ul className="ws-switcher-list" role="listbox" aria-label="Asignaturas">
            {cursos.length === 0 && <li className="ws-switcher-empty">No hay asignaturas con ese criterio.</li>}
            {cursos.map(c => (
              <li key={c.id}>
                <button
                  type="button"
                  role="option"
                  aria-selected={c.id === cursoActivo?.id}
                  className={c.id === cursoActivo?.id ? 'is-active' : ''}
                  onClick={() => {
                    seleccionarCurso(c.id)
                    setAbierto(false)
                  }}
                >
                  <span className="ws-switcher-mark ws-switcher-mark-sm">{c.codigo.slice(0, 2)}</span>
                  <span className="ws-switcher-text">
                    <span className="ws-switcher-name">{c.nombre}</span>
                    <span className="ws-switcher-meta">{c.codigo} · {c.periodo} · {c.totalEstudiantes} est.</span>
                  </span>
                  {c.id === cursoActivo?.id && <IconCheck size={14} />}
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
