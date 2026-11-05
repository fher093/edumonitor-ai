import { NavLink } from 'react-router-dom'
import { ROLES, ETIQUETA_ROL } from '../auth/roles.js'
import {
  IconMenu,
  IconCursos,
  IconMatricula,
  IconActividades,
  IconReglas,
  IconDashboard,
  IconAlertas,
  IconAnalisis,
  IconInforme,
  IconAgente,
  IconAuditoria,
  IconServicios,
  IconLogout,
} from './Icons.jsx'

export default function Sidebar({ usuario, logout, collapsed, onToggle }) {
  if (!usuario) return null

  const esDocente = usuario.rol === ROLES.DOCENTE
  const esAdmin = usuario.rol === ROLES.ADMIN

  const iniciales = usuario.nombre
    ? usuario.nombre
        .split(' ')
        .map(n => n[0])
        .slice(0, 2)
        .join('')
        .toUpperCase()
    : 'U'

  return (
    <aside className={`app-sidebar ${collapsed ? 'collapsed' : ''}`} aria-label="Navegación principal">
      {/* Cabecera del Sidebar */}
      <div className="sidebar-brand-bar">
        <div className="sidebar-brand">
          <div className="brand-icon-box">
            <span className="brand-dot" />
          </div>
          {!collapsed && (
            <div className="brand-text">
              <span className="brand-title">
                EduMonitor<span className="brand-highlight">AI</span>
              </span>
              <span className="brand-tag-uta">UTA • ACADÉMICO</span>
            </div>
          )}
        </div>
        <button
          type="button"
          className="sidebar-toggle-btn"
          onClick={onToggle}
          title={collapsed ? 'Expandir barra lateral' : 'Contraer barra lateral'}
          aria-label="Alternar barra lateral"
        >
          <IconMenu size={16} />
        </button>
      </div>

      {/* Menú de Navegación */}
      <nav className="sidebar-nav">
        {!collapsed && esDocente && <span className="nav-group-title">ESPACIO ACADÉMICO</span>}

        <ul className="sidebar-menu-list">
          {esDocente && (
            <>
              <li>
                <NavLink
                  to="/matricula"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Matrícula de Estudiantes"
                >
                  <IconMatricula size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Matrícula</span>
                    </span>
                  )}
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/cursos"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Cursos Asignados"
                >
                  <IconCursos size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Mis Cursos</span>
                    </span>
                  )}
                </NavLink>
              </li>

              <li>
                <NavLink
                  to="/actividades"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Actividades y Calificaciones"
                >
                  <IconActividades size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Actividades</span>
                    </span>
                  )}
                </NavLink>
              </li>

              {!collapsed && <div className="nav-divider" />}
              {!collapsed && <span className="nav-group-title">ANALÍTICA & SEGUIMIENTO</span>}

              <li>
                <NavLink
                  to="/dashboard"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Dashboard"
                >
                  <IconDashboard size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Dashboard</span>
                    </span>
                  )}
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/alertas"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Alertas Tempranas"
                >
                  <IconAlertas size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Alertas</span>
                    </span>
                  )}
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/analisis"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Análisis Grupal"
                >
                  <IconAnalisis size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Análisis grupal</span>
                    </span>
                  )}
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/informe"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Informe Académico"
                >
                  <IconInforme size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Informe</span>
                    </span>
                  )}
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/agente"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Agente IA"
                >
                  <IconAgente size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Agente IA</span>
                      <span className="nav-badge nav-badge-violet">IA</span>
                    </span>
                  )}
                </NavLink>
              </li>
            </>
          )}

          {esAdmin && (
            <>
              {!collapsed && <span className="nav-group-title">ADMINISTRACIÓN</span>}
              <li>
                <NavLink
                  to="/admin"
                  end
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Administración de Cursos"
                >
                  <IconCursos size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Cursos</span>
                    </span>
                  )}
                </NavLink>
              </li>

              <li>
                <NavLink
                  to="/admin/actividades"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Actividades y Calificaciones"
                >
                  <IconActividades size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Actividades</span>
                    </span>
                  )}
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/admin/reglas-alertas"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Reglas de Alertas Académicas"
                >
                  <IconReglas size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Reglas de alerta</span>
                    </span>
                  )}
                </NavLink>
              </li>

              {!collapsed && <div className="nav-divider" />}
              {!collapsed && <span className="nav-group-title">ANALÍTICA & SEGUIMIENTO</span>}

              <li>
                <NavLink
                  to="/admin/dashboard"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Dashboard de Rendimiento"
                >
                  <IconDashboard size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Dashboard</span>
                    </span>
                  )}
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/admin/alertas"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Alertas Tempranas"
                >
                  <IconAlertas size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Alertas</span>
                    </span>
                  )}
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/admin/analisis"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Análisis Grupal"
                >
                  <IconAnalisis size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Análisis grupal</span>
                    </span>
                  )}
                </NavLink>
              </li>

              {!collapsed && <div className="nav-divider" />}
              {!collapsed && <span className="nav-group-title">TRAZABILIDAD Y SISTEMA</span>}

              <li>
                <NavLink
                  to="/admin/auditoria"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Auditoría Técnica"
                >
                  <IconAuditoria size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Auditoría</span>
                    </span>
                  )}
                </NavLink>
              </li>
              <li>
                <NavLink
                  to="/admin/servicios"
                  className={({ isActive }) => `sidebar-pill-link ${isActive ? 'active' : ''}`}
                  title="Estado de Servicios"
                >
                  <IconServicios size={17} className="pill-icon" />
                  {!collapsed && (
                    <span className="pill-label">
                      <span>Servicios</span>
                    </span>
                  )}
                </NavLink>
              </li>
            </>
          )}
        </ul>
      </nav>

      {/* Pie del Sidebar: Usuario y Logout */}
      <div className="sidebar-footer">
        {!collapsed ? (
          <div className="sidebar-user-card">
            <div className="user-avatar">{iniciales}</div>
            <div className="user-details">
              <span className="user-name" title={usuario.nombre}>{usuario.nombre}</span>
              <span className="user-role-tag">{ETIQUETA_ROL[usuario.rol] ?? usuario.rol}</span>
            </div>
          </div>
        ) : (
          <div className="sidebar-user-card-collapsed" title={`${usuario.nombre} (${ETIQUETA_ROL[usuario.rol] ?? usuario.rol})`}>
            <div className="user-avatar">{iniciales}</div>
          </div>
        )}

        <button
          type="button"
          className="btn-ghost-pill btn-logout"
          onClick={logout}
          title="Cerrar sesión"
        >
          <IconLogout size={15} />
          {!collapsed && <span>Cerrar sesión</span>}
        </button>
      </div>
    </aside>
  )
}
