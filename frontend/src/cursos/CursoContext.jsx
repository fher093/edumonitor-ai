import { createContext, useContext, useEffect, useState } from 'react'
import { api } from '../api/http.js'
import useCursos from '../hooks/useCursos.js'

const CursoContext = createContext(null)

/**
 * Curso activo compartido por todo el espacio del docente: se elige una vez en el menú lateral
 * y lo usan Resumen, Estudiantes, Actividades, Alertas y Análisis. También expone el número de
 * alertas del curso para el contador del menú.
 */
export function CursoProvider({ children }) {
  const cursos = useCursos()
  const [totalAlertas, setTotalAlertas] = useState(null)
  const [conFoto, setConFoto] = useState(() => new Set())

  useEffect(() => {
    if (!cursos.cursoId) {
      setTotalAlertas(null)
      return
    }
    let vivo = true
    api(`/api/alertas/curso/${cursos.cursoId}`)
      .then(d => vivo && setTotalAlertas(d.resumen?.total ?? 0))
      .catch(() => vivo && setTotalAlertas(null))
    return () => {
      vivo = false
    }
  }, [cursos.cursoId])

  // Estudiantes del curso con foto de perfil en Moodle (para no pedir fotos inexistentes).
  useEffect(() => {
    setConFoto(new Set())
    if (!cursos.cursoId) return
    let vivo = true
    api(`/api/cursos/${cursos.cursoId}/fotos`)
      .then(ids => vivo && setConFoto(new Set(ids)))
      .catch(() => {
        /* Sin fotos: los avatares muestran iniciales. */
      })
    return () => {
      vivo = false
    }
  }, [cursos.cursoId])

  return <CursoContext.Provider value={{ ...cursos, totalAlertas, conFoto }}>{children}</CursoContext.Provider>
}

export function useCursoActivo() {
  const ctx = useContext(CursoContext)
  if (!ctx) throw new Error('useCursoActivo debe usarse dentro de <CursoProvider>.')
  return ctx
}

/** Datos opcionales del contexto (null fuera del espacio del docente, p. ej. en vistas de administrador). */
export function useCursoOpcional() {
  return useContext(CursoContext)
}

/**
 * Carga un recurso de la API que depende del curso activo.
 * `ruta` recibe el id del curso y devuelve la URL; se vuelve a pedir al cambiar de curso.
 */
export function useDatosCurso(ruta) {
  const { cursoId } = useCursoActivo()
  const [estado, setEstado] = useState({ datos: null, cargando: false, error: '' })

  useEffect(() => {
    if (!cursoId) {
      setEstado({ datos: null, cargando: false, error: '' })
      return
    }
    let vivo = true
    setEstado(prev => ({ ...prev, cargando: true, error: '' }))
    api(ruta(cursoId))
      .then(datos => vivo && setEstado({ datos, cargando: false, error: '' }))
      .catch(err => vivo && setEstado({ datos: null, cargando: false, error: err.message }))
    return () => {
      vivo = false
    }
    // `ruta` es una función estable por página; solo cambia el curso.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cursoId])

  return estado
}
