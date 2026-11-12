import { useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/useAuth.js'
import { ROLES } from '../auth/roles.js'
import { CursoProvider, useCursoOpcional } from '../cursos/CursoContext.jsx'
import Sidebar from './Sidebar.jsx'
import { IconExterno, IconMenu, IconSync } from './Icons.jsx'
import { fechaHora } from '../utils/formato.js'

const TITULOS_SECCION = {
  '/inicio': 'Resumen',
  '/estudiantes': 'Estudiantes',
  '/actividades': 'Actividades',
  '/alertas': 'Alertas',
  '/analisis': 'Análisis grupal',
  '/cursos': 'Mis cursos',
  '/informe': 'Informes',
  '/agente': 'Asistente IA',
  '/admin': 'Cuentas de docentes',
  '/admin/moodle': 'Integración con Moodle',
  '/admin/reglas-alertas': 'Reglas de alerta',
  '/admin/auditoria': 'Auditoría',
  '/admin/servicios': 'Estado de servicios',
}

/** Secciones del docente que dependen del curso activo (se muestra el curso en la barra superior). */
const RUTAS_DE_CURSO = ['/inicio', '/estudiantes', '/actividades', '/alertas', '/analisis']

export default function Layout() {
  const { usuario } = useAuth()
  const contenido = <Shell />
  return usuario?.rol === ROLES.DOCENTE ? <CursoProvider>{contenido}</CursoProvider> : contenido
}

function Shell() {
  const { usuario, logout } = useAuth()
  const location = useLocation()

  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => {
    const stored = localStorage.getItem('edumonitor_sidebar_collapsed')
    if (stored !== null) return stored === 'true'
    return window.matchMedia('(max-width: 860px)').matches
  })

  function toggleSidebar() {
    setSidebarCollapsed(prev => {
      const next = !prev
      localStorage.setItem('edumonitor_sidebar_collapsed', String(next))
      return next
    })
  }

  const esRutaDeCurso = RUTAS_DE_CURSO.includes(location.pathname)

  return (
    <div className={`app-shell ws-shell ${sidebarCollapsed ? 'sidebar-is-collapsed' : ''}`}>
      <Sidebar usuario={usuario} logout={logout} collapsed={sidebarCollapsed} onToggle={toggleSidebar} />

      {!sidebarCollapsed && (
        <button type="button" className="sidebar-backdrop" aria-label="Cerrar menú de navegación" onClick={toggleSidebar} />
      )}

      <div className="app-viewport ws-viewport">
        <header className="ws-topbar">
          <div className="ws-topbar-left">
            <button type="button" className="topbar-mobile-btn ws-icon-btn" onClick={toggleSidebar} aria-label="Alternar menú">
              <IconMenu size={18} />
            </button>
            <Migas seccion={TITULOS_SECCION[location.pathname] ?? 'EduMonitor AI'} conCurso={esRutaDeCurso} />
          </div>
          {esRutaDeCurso && <FuenteDatos />}
        </header>

        <main className={`ws-content ${esRutaDeCurso ? 'ws-content-fit' : ''}`}>
          <Outlet />
        </main>
      </div>
    </div>
  )
}

function Migas({ seccion, conCurso }) {
  const curso = useCursoOpcional()
  const activo = conCurso ? curso?.cursoActivo : null
  return (
    <nav className="ws-crumbs" aria-label="Ubicación">
      {activo ? (
        <>
          <span className="ws-crumb-code">{activo.codigo}</span>
          <span className="ws-crumb-course">{activo.nombre}</span>
          <span className="ws-crumb-sep">/</span>
        </>
      ) : (
        <>
          <span className="ws-crumb-course">EduMonitor AI</span>
          <span className="ws-crumb-sep">/</span>
        </>
      )}
      <strong>{seccion}</strong>
    </nav>
  )
}

/** Fuente y fecha de los datos del curso activo (RNF-08) y acceso directo a Moodle. */
function FuenteDatos() {
  const curso = useCursoOpcional()
  const activo = curso?.cursoActivo
  if (!activo) return null
  return (
    <div className="ws-topbar-right">
      {activo.origen === 'moodle' ? (
        <span className="ws-source" title="Datos sincronizados automáticamente desde Moodle">
          <span className="ws-source-dot" />
          <IconSync size={12} /> Moodle · {fechaHora(activo.fechaSincronizacion)}
        </span>
      ) : (
        <span className="ws-source ws-source-local">Datos locales</span>
      )}
      {activo.urlMoodle && (
        <a className="ws-btn ws-btn-ghost ws-btn-sm" href={activo.urlMoodle} target="_blank" rel="noopener noreferrer">
          <IconExterno size={13} /> Abrir en Moodle
        </a>
      )}
    </div>
  )
}
