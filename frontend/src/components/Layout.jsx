import { useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../auth/useAuth.js'
import Sidebar from './Sidebar.jsx'
import { IconMenu } from './Icons.jsx'

const TITULOS_SECCION = {
  '/matricula': 'Registro de Estudiantes y Matrícula',
  '/cursos': 'Mis Cursos Autorizados',
  '/actividades': 'Actividades y Calificaciones',
  '/dashboard': 'Dashboard de Rendimiento',
  '/alertas': 'Alertas Tempranas',
  '/analisis': 'Análisis Grupal del Curso',
  '/informe': 'Informes Académicos',
  '/agente': 'Asistente Académico IA',
  '/admin': 'Administración de Cursos',
  '/admin/cursos': 'Administración de Cursos',
  '/admin/actividades': 'Actividades y Calificaciones',
  '/admin/reglas-alertas': 'Reglas de Alertas Académicas',
  '/admin/dashboard': 'Dashboard de Rendimiento',
  '/admin/alertas': 'Alertas Tempranas',
  '/admin/analisis': 'Análisis Grupal del Curso',
  '/admin/auditoria': 'Auditoría Técnica del Sistema',
  '/admin/servicios': 'Estado de Servicios',
}

function tituloDeRuta(pathname) {
  if (pathname.startsWith('/admin/ficha/') || pathname.startsWith('/ficha/')) {
    return 'Ficha del Estudiante'
  }
  return TITULOS_SECCION[pathname] || 'Seguimiento Académico'
}

export default function Layout() {
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

  const tituloActual = tituloDeRuta(location.pathname)

  return (
    <div className={`app-shell ${sidebarCollapsed ? 'sidebar-is-collapsed' : ''}`}>
      <Sidebar
        usuario={usuario}
        logout={logout}
        collapsed={sidebarCollapsed}
        onToggle={toggleSidebar}
      />

      {!sidebarCollapsed && (
        <button
          type="button"
          className="sidebar-backdrop"
          aria-label="Cerrar menú de navegación"
          onClick={toggleSidebar}
        />
      )}

      <div className="app-viewport">
        {/* Barra superior moderna y limpia (ClickUp™ Style) */}
        <header className="app-topbar">
          <div className="topbar-left">
            <button
              type="button"
              className="topbar-mobile-btn"
              onClick={toggleSidebar}
              aria-label="Alternar menú"
            >
              <IconMenu size={18} />
            </button>
            <div className="topbar-breadcrumb">
              <span className="breadcrumb-brand">EduMonitor AI</span>
              <span className="breadcrumb-separator">/</span>
              <span className="breadcrumb-current">{tituloActual}</span>
            </div>
          </div>

          <div className="topbar-right">
            <div className="status-pill-clean" title="Sesión autenticada">
              <span className="status-dot-pulse" />
              <span className="status-pill-text">Sesión activa</span>
            </div>

            {usuario && (
              <div className="topbar-user-badge" title={`${usuario.nombre} (${usuario.rol})`}>
                <span className="user-pill-name">{usuario.nombre}</span>
                <span className="user-avatar-micro">
                  {usuario.nombre ? usuario.nombre[0].toUpperCase() : 'U'}
                </span>
              </div>
            )}
          </div>
        </header>

        {/* Contenido principal sobre canvas blanco */}
        <main className="app-content-body">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
