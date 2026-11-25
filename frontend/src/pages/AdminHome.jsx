import EstadoServicios from '../components/EstadoServicios.jsx'

// Vista técnica del Administrador Técnico: estado de los servicios de la plataforma.
// Cursos, matrículas y notas se gestionan en Moodle; las cuentas de docentes, en /admin;
// la auditoría, en /admin/auditoria.
export default function AdminHome() {
  return (
    <div className="page-container">
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
    </div>
  )
}
