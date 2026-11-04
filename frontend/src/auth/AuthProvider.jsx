import { useCallback, useEffect, useMemo, useState } from 'react'
import { api, setUnauthorizedHandler } from '../api/http.js'
import { AuthContext } from './AuthContext.js'
import { clearSession, loadSession, saveSession } from './session.js'

const MAX_TIMEOUT = 2_147_483_647 // límite de setTimeout (~24 días)

export function AuthProvider({ children }) {
  const [session, setSession] = useState(() => loadSession())

  const logout = useCallback(() => {
    clearSession()
    setSession(null)
  }, [])

  const login = useCallback(async (correo, password) => {
    const data = await api('/api/auth/login', {
      method: 'POST',
      body: { correo: correo.trim(), password },
      auth: false,
    })
    const nueva = { token: data.token, expiresAt: data.expiresAt, usuario: data.usuario }
    saveSession(nueva)
    setSession(nueva)
    return nueva.usuario
  }, [])

  // Un 401 en cualquier llamada protegida cierra la sesión
  useEffect(() => {
    setUnauthorizedHandler(logout)
  }, [logout])

  // Cierra la sesión sola cuando el token expira
  useEffect(() => {
    if (!session) return undefined
    const ms = new Date(session.expiresAt).getTime() - Date.now()
    const id = setTimeout(logout, Math.min(Math.max(ms, 0), MAX_TIMEOUT))
    return () => clearTimeout(id)
  }, [session, logout])

  const value = useMemo(
    () => ({ usuario: session?.usuario ?? null, token: session?.token ?? null, login, logout }),
    [session, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
