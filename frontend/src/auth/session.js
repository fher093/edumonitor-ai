// La sesión vive en sessionStorage: se borra al cerrar la pestaña y no se comparte entre pestañas.
const KEY = 'edumonitor.session'

export function loadSession() {
  try {
    const raw = sessionStorage.getItem(KEY)
    if (!raw) return null
    const s = JSON.parse(raw)
    if (!s?.token || !s?.expiresAt || !s?.usuario || new Date(s.expiresAt) <= new Date()) {
      sessionStorage.removeItem(KEY)
      return null
    }
    return s
  } catch {
    return null
  }
}

export function saveSession(session) {
  sessionStorage.setItem(KEY, JSON.stringify(session))
}

export function clearSession() {
  sessionStorage.removeItem(KEY)
}
