import { Link } from 'react-router-dom'
import { useAuth } from '../auth/useAuth.js'
import { rutaInicial } from '../auth/roles.js'
import { IconAuditoria } from '../components/Icons.jsx'

export default function NoAutorizado() {
  const { usuario } = useAuth()

  return (
    <section className="product-surface-card unauthorized-card">
      <div className="empty-icon-circle">
        <IconAuditoria size={26} />
      </div>
      <h1 className="display-headline">Acceso restringido</h1>
      <p className="lead-subtitle">Tu rol no tiene permiso para abrir esta pantalla.</p>
      <Link to={rutaInicial(usuario.rol)} className="btn-pill btn-pill-dark">
        Volver a mi inicio
      </Link>
    </section>
  )
}
