import { useMemo } from 'react'
import { useLocation } from 'react-router-dom'
import EstadoServicios from '../components/EstadoServicios.jsx'
import ModuloPreview from '../components/ModuloPreview.jsx'

// Vistas técnicas del Administrador Técnico: estado de los servicios y auditoría (vista previa).
// Cursos, matrículas y notas se gestionan en Moodle; las cuentas de docentes, en /admin.
export default function AdminHome() {
  const location = useLocation()

  const rutaActual = location.pathname
  const vista = useMemo(() => {
    if (rutaActual.includes('/auditoria')) return 'auditoria'
    return 'servicios'
  }, [rutaActual])

  return (
    <div className="page-container">
      {vista === 'servicios' ? (
        <div className="product-surface-card">
          <div className="section-head-split">
            <div>
              <div className="brand-dot-pill">
                <span className="dot-violet" />
                <span className="tag-uta-micro">MONITOREO DE SERVICIOS</span>
              </div>
              <h1 className="display-headline">Salud de Servicios y API</h1>
              <p className="lead-subtitle">Verificación de conectividad con los servicios de la plataforma.</p>
            </div>
          </div>
          <div className="hairline-divider" />
          <EstadoServicios />
        </div>
      ) : (
        <ModuloPreview tipo={vista} />
      )}
    </div>
  )
}
