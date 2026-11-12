import { loadSession } from '../auth/session.js'

const BASE = import.meta.env.VITE_API_URL ?? ''

export class ApiError extends Error {
  constructor(message, status) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

let onUnauthorized = () => {}
export function setUnauthorizedHandler(fn) {
  onUnauthorized = fn
}

const MENSAJES = {
  400: 'Revisa los datos ingresados.',
  401: 'Tu sesión no es válida o expiró. Inicia sesión nuevamente.',
  403: 'No tienes permisos para realizar esta acción.',
  429: 'Demasiados intentos. Espera un minuto e inténtalo de nuevo.',
  500: 'Error interno del servidor. Inténtalo más tarde.',
}

/**
 * Llama a la API. Adjunta el token si hay sesión (salvo { auth: false }).
 * Un 401 en una ruta protegida cierra la sesión automáticamente.
 */
export async function api(path, { method = 'GET', body, auth = true, signal } = {}) {
  const headers = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  if (auth) {
    const s = loadSession()
    if (s?.token) headers.Authorization = `Bearer ${s.token}`
  }

  let resp
  try {
    resp = await fetch(`${BASE}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal,
    })
  } catch (err) {
    if (err?.name === 'AbortError') throw err
    throw new ApiError('No se pudo conectar con el servidor. Verifica que los servicios estén en ejecución.', 0)
  }

  let data = null
  const texto = await resp.text()
  if (texto) {
    try {
      data = JSON.parse(texto)
    } catch {
      data = null
    }
  }

  if (!resp.ok) {
    if (resp.status === 401 && auth) onUnauthorized()
    throw new ApiError(data?.message ?? MENSAJES[resp.status] ?? `Error inesperado (${resp.status}).`, resp.status)
  }
  return data
}

/**
 * Descarga un archivo binario autenticado (p. ej. una foto) y devuelve un Blob,
 * o null si no existe. Las etiquetas <img> no pueden enviar el token por sí solas.
 */
export async function apiBlob(path, { signal } = {}) {
  const s = loadSession()
  const headers = s?.token ? { Authorization: `Bearer ${s.token}` } : {}
  const resp = await fetch(`${BASE}${path}`, { headers, signal })
  if (resp.status === 401) onUnauthorized()
  if (!resp.ok) return null
  return resp.blob()
}
