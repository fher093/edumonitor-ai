import { NavLink } from 'react-router-dom'
import { ROLES, ETIQUETA_ROL } from '../auth/roles.js'
import { useCursoOpcional } from '../cursos/CursoContext.jsx'
import CursoSwitcher from './CursoSwitcher.jsx'
import { Avatar } from './ui.jsx'
import {
  IconMenu,
  IconCursos,
  IconMatricula,
  IconActividades,
  IconReglas,
  IconAlertas,
  IconAnalisis,
  IconInforme,
  IconAgente,
  IconAuditoria,
  IconServicios,
  IconLogout,
  IconSync,
  IconInicio,
} from './Icons.jsx'

// Navegación por rol. `badge: 'alertas'` muestra el número de alertas del curso activo.
const NAV_DOCENTE = [
  {
    grupo: 'Curso',
    items: [
      { to: '/inicio', etiqueta: 'Resumen', icono: IconInicio },
      { to: '/estudiantes', etiqueta: 'Estudiantes', icono: IconMatricula },
      { to: '/actividades', etiqueta: 'Actividades', icono: IconActividades },
      { to: '/alertas', etiqueta: 'Alertas', icono: IconAlertas, badge: 'alertas' },
      { to: '/analisis', etiqueta: 'Análisis grupal', icono: IconAnalisis },
    ],
  },
  {
    grupo: 'Asistencia IA',
    items: [
      { to: '/agente', etiqueta: 'Asistente', icono: IconAgente, pronto: true },
      { to: '/informe', etiqueta: 'Informes', icono: IconInforme, pronto: true },
    ],
  },
  {
    grupo: 'General',
    items: [{ to: '/cursos', etiqueta: 'Mis cursos', icono: IconCursos }],
  },
]

const NAV_ADMIN = [
  {
    grupo: 'Administración',
    items: [
      { to: '/admin', etiqueta: 'Docentes', icono: IconMatricula, end: true },
      { to: '/admin/moodle', etiqueta: 'Moodle', icono: IconSync },
      { to: '/admin/reglas-alertas', etiqueta: 'Reglas de alerta', icono: IconReglas },
    ],
  },
  {
    grupo: 'Sistema',
    items: [
      { to: '/admin/servicios', etiqueta: 'Servicios', icono: IconServicios },
      { to: '/admin/auditoria', etiqueta: 'Auditoría', icono: IconAuditoria, pronto: true },
    ],
  },
]

export default function Sidebar({ usuario, logout, collapsed, onToggle }) {
  const curso = useCursoOpcional()
  if (!usuario) return null

  const esDocente = usuario.rol === ROLES.DOCENTE
  const grupos = esDocente ? NAV_DOCENTE : NAV_ADMIN

  return (
    <aside className={`app-sidebar ws-sidebar ${collapsed ? 'collapsed' : ''}`} aria-label="Navegación principal">
      <div className="ws-sidebar-top">
        <div className="ws-brand">
          <span className="ws-brand-mark" aria-hidden="true">
            <span />
          </span>
          {!collapsed && (
            <span className="ws-brand-text">
              <strong>
                EduMonitor<em>AI</em>
              </strong>
              <small>UTA · {esDocente ? 'Docente' : 'Administración'}</small>
            </span>
          )}
        </div>
        <button
          type="button"
          className="ws-icon-btn ws-icon-btn-dark"
          onClick={onToggle}
          title={collapsed ? 'Expandir menú' : 'Contraer menú'}
          aria-label="Alternar menú lateral"
        >
          <IconMenu size={16} />
        </button>
      </div>

      {esDocente && curso && <CursoSwitcher colapsado={collapsed} />}

      <nav className="ws-nav">
        {grupos.map(g => (
          <div key={g.grupo} className="ws-nav-group">
            {!collapsed && <span className="ws-nav-title">{g.grupo}</span>}
            <ul>
              {g.items.map(item => {
                const Icono = item.icono
                const contador = item.badge === 'alertas' ? curso?.totalAlertas : null
                return (
                  <li key={item.to}>
                    <NavLink
                      to={item.to}
                      end={item.end}
                      className={({ isActive }) => `ws-nav-link ${isActive ? 'is-active' : ''}`}
                      title={item.etiqueta}
                    >
                      <Icono size={17} />
                      {!collapsed && <span className="ws-nav-label">{item.etiqueta}</span>}
                      {!collapsed && item.pronto && <span className="ws-nav-tag">Sprint 3</span>}
                      {contador > 0 && <span className="ws-nav-count">{contador}</span>}
                    </NavLink>
                  </li>
                )
              })}
            </ul>
          </div>
        ))}
      </nav>

      <div className="ws-sidebar-foot">
        <div className="ws-user" title={`${usuario.nombre} (${ETIQUETA_ROL[usuario.rol] ?? usuario.rol})`}>
          <Avatar nombre={usuario.nombre} tamano={30} />
          {!collapsed && (
            <span className="ws-user-text">
              <strong>{usuario.nombre}</strong>
              <small>{ETIQUETA_ROL[usuario.rol] ?? usuario.rol}</small>
            </span>
          )}
        </div>
        <button type="button" className="ws-icon-btn ws-icon-btn-dark" onClick={logout} title="Cerrar sesión" aria-label="Cerrar sesión">
          <IconLogout size={16} />
        </button>
      </div>
    </aside>
  )
}
