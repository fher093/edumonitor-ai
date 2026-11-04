import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './useAuth.js'

/**
 * Exige sesión iniciada y, opcionalmente, uno de los roles indicados.
 * Sin sesión -> /login. Con sesión pero rol incorrecto -> /no-autorizado.
 */
export default function ProtectedRoute({ roles }) {
  const { usuario } = useAuth()
  const location = useLocation()

  if (!usuario) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (roles && !roles.includes(usuario.rol)) return <Navigate to="/no-autorizado" replace />
  return <Outlet />
}
