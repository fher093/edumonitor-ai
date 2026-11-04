import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth.js'
import { rutaInicial } from '../auth/roles.js'

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export default function Login() {
  const { usuario, login } = useAuth()
  const navigate = useNavigate()

  const [correo, setCorreo] = useState('')
  const [password, setPassword] = useState('')
  const [verClave, setVerClave] = useState(false)
  const [cargando, setCargando] = useState(false)
  const [error, setError] = useState('')

  if (usuario) return <Navigate to={rutaInicial(usuario.rol)} replace />

  async function onSubmit(e) {
    e.preventDefault()
    setError('')

    if (!EMAIL_RE.test(correo.trim())) return setError('Ingresa un correo electrónico válido.')
    if (!password) return setError('Ingresa tu contraseña.')

    setCargando(true)
    try {
      const u = await login(correo, password)
      navigate(rutaInicial(u.rol), { replace: true })
    } catch (err) {
      setError(err.message)
      setPassword('')
    } finally {
      setCargando(false)
    }
  }

  return (
    <div className="login-canvas-page">
      <form className="login-product-card" onSubmit={onSubmit} noValidate>
        <div className="login-header-group">
          <div className="brand-dot-pill">
            <span className="dot-violet" />
            <span className="tag-uta-micro">UTA • ACADÉMICO</span>
          </div>
          <h1 className="login-headline">
            EduMonitor<span className="text-violet">AI</span>
          </h1>
          <p className="login-subcopy">Plataforma de seguimiento y analítica docente</p>
        </div>

        {error && (
          <div className="banner-alert banner-alert-error login-alert" role="alert">
            {error}
          </div>
        )}

        <div className="login-form-fields">
          <div className="form-control-item">
            <label htmlFor="correo">Correo institucional</label>
            <input
              id="correo"
              type="email"
              autoComplete="username"
              placeholder="docente@edumonitor.local"
              value={correo}
              onChange={e => setCorreo(e.target.value)}
              disabled={cargando}
              autoFocus
              required
            />
          </div>

          <div className="form-control-item">
            <label htmlFor="password">Contraseña</label>
            <div className="password-input-wrapper">
              <input
                id="password"
                type={verClave ? 'text' : 'password'}
                autoComplete="current-password"
                placeholder="••••••••"
                value={password}
                onChange={e => setPassword(e.target.value)}
                disabled={cargando}
                required
              />
              <button
                type="button"
                className="password-toggle-text-btn"
                onClick={() => setVerClave(!verClave)}
                aria-label={verClave ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                aria-pressed={verClave}
              >
                {verClave ? 'Ocultar' : 'Mostrar'}
              </button>
            </div>
          </div>
        </div>

        <button
          type="submit"
          className="btn-pill btn-pill-dark btn-pill-block login-submit"
          disabled={cargando}
        >
          {cargando ? 'Iniciando sesión...' : 'Ingresar a la plataforma'}
        </button>

        <div className="login-meta-footer">
          <span>Universidad Técnica de Ambato · Acceso institucional</span>
        </div>
      </form>
    </div>
  )
}
