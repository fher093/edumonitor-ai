import { useCallback, useEffect, useMemo, useState } from 'react'
import { api } from '../api/http.js'

const STORAGE_KEY = 'edumonitor_curso_activo'

/**
 * Carga los cursos autorizados del usuario y centraliza los filtros de período
 * y texto (HU-09). Mantiene el curso activo en localStorage para conservar el
 * contexto entre secciones.
 */
export default function useCursos() {
  const [cursos, setCursos] = useState([])
  const [periodos, setPeriodos] = useState([])
  const [periodo, setPeriodo] = useState('')
  const [q, setQ] = useState('')
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [recarga, setRecarga] = useState(0)
  const [cursoId, setCursoId] = useState(() => {
    const guardado = Number(localStorage.getItem(STORAGE_KEY))
    return Number.isFinite(guardado) && guardado > 0 ? guardado : null
  })

  useEffect(() => {
    const ctrl = new AbortController()
    const params = new URLSearchParams()
    if (periodo) params.set('periodo', periodo)
    if (q.trim()) params.set('q', q.trim())
    const query = params.toString()

    setCargando(true)
    setError('')
    api(`/api/cursos${query ? `?${query}` : ''}`, { signal: ctrl.signal })
      .then(data => {
        setCursos(data)
        setCargando(false)
      })
      .catch(err => {
        if (err?.name === 'AbortError') return
        setError(err.message)
        setCargando(false)
      })

    return () => ctrl.abort()
  }, [periodo, q, recarga])

  useEffect(() => {
    let vivo = true
    api('/api/cursos/periodos')
      .then(data => {
        if (vivo) setPeriodos(data)
      })
      .catch(() => {
        /* El filtro de período es opcional; no bloquea la vista. */
      })
    return () => {
      vivo = false
    }
  }, [])

  useEffect(() => {
    if (cursos.length === 0) return
    if (!cursoId || !cursos.some(c => c.id === cursoId)) {
      const primero = cursos[0].id
      setCursoId(primero)
      localStorage.setItem(STORAGE_KEY, String(primero))
    }
  }, [cursos, cursoId])

  const seleccionarCurso = useCallback(id => {
    const numero = Number(id)
    setCursoId(numero)
    if (numero > 0) localStorage.setItem(STORAGE_KEY, String(numero))
    else localStorage.removeItem(STORAGE_KEY)
  }, [])

  const cursoActivo = useMemo(
    () => cursos.find(c => c.id === cursoId) ?? null,
    [cursos, cursoId]
  )

  const recargar = useCallback(() => setRecarga(n => n + 1), [])

  return {
    cursos,
    periodos,
    periodo,
    setPeriodo,
    q,
    setQ,
    cargando,
    error,
    cursoId,
    cursoActivo,
    seleccionarCurso,
    recargar,
  }
}
