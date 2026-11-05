import { useEffect, useState } from 'react'
import { api } from '../api/http.js'

const SERVICIOS = [
  {
    id: 'backend',
    nombre: 'API académica',
    descripcion: 'Autenticación, cursos y matrícula',
    ruta: '/api/auth/me',
  },
  {
    id: 'ia',
    nombre: 'Servicio de IA',
    descripcion: 'Consultas académicas asistidas',
    ruta: '/api/ia/estado',
  },
]

/** Comprueba que la sesión activa puede acceder a cada servicio de la plataforma. */
export default function EstadoServicios() {
  const [estado, setEstado] = useState({})

  useEffect(() => {
    const ctrl = new AbortController()
    SERVICIOS.forEach(async (s) => {
      try {
        await api(s.ruta, { signal: ctrl.signal })
        setEstado((prev) => ({ ...prev, [s.id]: { ok: true } }))
      } catch (err) {
        if (err?.name === 'AbortError') return
        setEstado((prev) => ({ ...prev, [s.id]: { ok: false, mensaje: err.message } }))
      }
    })
    return () => ctrl.abort()
  }, [])

  return (
    <section className="service-health-section" aria-label="Estado de los servicios">
      <ul className="service-health-grid">
        {SERVICIOS.map((s) => {
          const e = estado[s.id]
          const nivel = !e ? 'pending' : e.ok ? 'ok' : 'error'
          const etiqueta = !e ? 'Verificando' : e.ok ? 'Operativo' : 'Con incidencias'

          return (
            <li key={s.id} className="service-health-card">
              <div className="service-health-head">
                <div className="service-health-info">
                  <span className="service-health-name">{s.nombre}</span>
                  <span className="service-health-desc">{s.descripcion}</span>
                </div>
                <span className={`status-pill service-pill-${nivel}`}>
                  <span className={`dot ${nivel}`} aria-hidden="true" />
                  {etiqueta}
                </span>
              </div>
              {e && !e.ok && <p className="service-health-error">{e.mensaje}</p>}
            </li>
          )
        })}
      </ul>
    </section>
  )
}
