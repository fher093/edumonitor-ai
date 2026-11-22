import { useEffect, useState } from 'react'
import { api } from '../api/http.js'
import { Buscador, Cargando, FiltroChips, Vacio } from '../components/ui.jsx'
import { IconAuditoria } from '../components/Icons.jsx'
import { fechaHora } from '../utils/formato.js'

const CATEGORIAS = [
  { valor: '', etiqueta: 'Todas' },
  { valor: 'agente', etiqueta: 'Agente IA' },
  { valor: 'consulta', etiqueta: 'Consultas' },
  { valor: 'cambio', etiqueta: 'Cambios' },
  { valor: 'acceso', etiqueta: 'Accesos' },
]

const RESULTADOS = [
  { valor: '', etiqueta: 'Cualquier resultado' },
  { valor: 'exito', etiqueta: 'Éxito' },
  { valor: 'denegado', etiqueta: 'Denegado' },
  { valor: 'fallido', etiqueta: 'Login fallido' },
  { valor: 'no_encontrado', etiqueta: 'No encontrado' },
  { valor: 'rechazado', etiqueta: 'Rechazado' },
  { valor: 'error', etiqueta: 'Error' },
]

const TONO_RESULTADO = { exito: 'ok', denegado: 'danger', fallido: 'danger', error: 'danger', no_encontrado: 'warn', rechazado: 'warn' }
const TONO_CATEGORIA = { agente: 'brand', consulta: 'info', cambio: 'warn', acceso: 'ok' }
const TAMANO = 25

/**
 * Auditoría (HU-17 / RF-12): quién consultó o cambió qué, con qué resultado, y qué herramientas usó
 * el agente de IA. Solo muestra acción, recurso e ids: nunca notas, contraseñas ni tokens.
 */
export default function AdminAuditoria() {
  const [filtros, setFiltros] = useState({ categoria: '', usuario: '', resultado: '', desde: '', hasta: '' })
  const [pagina, setPagina] = useState(1)
  const [datos, setDatos] = useState(null)
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => setPagina(1), [filtros])

  useEffect(() => {
    let vivo = true
    const params = new URLSearchParams({ pagina: String(pagina), tamano: String(TAMANO) })
    if (filtros.categoria) params.set('categoria', filtros.categoria)
    if (filtros.resultado) params.set('resultado', filtros.resultado)
    if (filtros.usuario.trim()) params.set('usuario', filtros.usuario.trim())
    if (filtros.desde) params.set('desde', new Date(`${filtros.desde}T00:00:00`).toISOString())
    if (filtros.hasta) params.set('hasta', new Date(`${filtros.hasta}T23:59:59`).toISOString())

    setCargando(true)
    const espera = setTimeout(() => {
      api(`/api/auditoria?${params}`)
        .then(d => {
          if (!vivo) return
          setDatos(d)
          setError('')
        })
        .catch(err => vivo && setError(err.message))
        .finally(() => vivo && setCargando(false))
    }, 250) // evita una petición por cada tecla en la búsqueda
    return () => {
      vivo = false
      clearTimeout(espera)
    }
  }, [filtros, pagina])

  const cambiar = (campo, valor) => setFiltros(f => ({ ...f, [campo]: valor }))
  const paginas = datos ? Math.max(1, Math.ceil(datos.total / datos.tamano)) : 1

  return (
    <div className="page-container">
      <section className="product-surface-card">
        <div className="section-head-split">
          <div>
            <div className="brand-dot-pill">
              <span className="dot-violet" />
              <span className="tag-uta-micro">SISTEMA · TRAZABILIDAD</span>
            </div>
            <h1 className="display-headline">Auditoría</h1>
            <p className="lead-subtitle">
              Accesos, consultas a datos académicos, cambios de configuración y herramientas usadas por el agente de IA.
              Solo se registran la acción, el recurso y el resultado: nunca notas, contraseñas ni tokens.
            </p>
          </div>
          <div className="stat-pill-counter">
            <span className="stat-number">{datos?.total ?? '—'}</span>
            <span className="stat-label">registros</span>
          </div>
        </div>

        <div className="hairline-divider" />

        <div className="aud-filtros">
          <FiltroChips etiqueta="Categoría" opciones={CATEGORIAS} valor={filtros.categoria} onCambio={v => cambiar('categoria', v)} />
          <div className="aud-filtros-fila">
            <Buscador valor={filtros.usuario} onCambio={v => cambiar('usuario', v)} placeholder="Correo del usuario..." etiqueta="Filtrar por usuario" />
            <select className="aud-select" value={filtros.resultado} onChange={e => cambiar('resultado', e.target.value)} aria-label="Resultado">
              {RESULTADOS.map(r => (
                <option key={r.valor} value={r.valor}>
                  {r.etiqueta}
                </option>
              ))}
            </select>
            <label className="aud-fecha">
              Desde <input type="date" value={filtros.desde} onChange={e => cambiar('desde', e.target.value)} />
            </label>
            <label className="aud-fecha">
              Hasta <input type="date" value={filtros.hasta} onChange={e => cambiar('hasta', e.target.value)} />
            </label>
          </div>
        </div>

        {error && <div className="banner-alert banner-alert-error">{error}</div>}

        {cargando && !datos ? (
          <Cargando texto="Cargando registros..." />
        ) : datos && datos.registros.length === 0 ? (
          <Vacio titulo="Sin registros" icono={<IconAuditoria size={22} />}>
            No hay eventos con estos filtros.
          </Vacio>
        ) : (
          datos && (
            <div className={`aud-tabla-wrap ${cargando ? 'is-loading' : ''}`}>
              <table className="ws-table aud-tabla">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Usuario</th>
                    <th>Categoría</th>
                    <th>Acción</th>
                    <th>Recurso</th>
                    <th>Detalle</th>
                    <th>Resultado</th>
                  </tr>
                </thead>
                <tbody>
                  {datos.registros.map(r => (
                    <tr key={r.id}>
                      <td className="aud-nowrap">{fechaHora(r.fecha)}</td>
                      <td>
                        <span className="aud-usuario">{r.usuarioCorreo ?? 'Sistema'}</span>
                        {r.rol && <span className="aud-rol">{r.rol === 'AdministradorTecnico' ? 'Administrador' : r.rol}</span>}
                      </td>
                      <td>
                        <span className={`ws-pill ws-pill-${TONO_CATEGORIA[r.categoria] ?? 'info'}`}>{r.categoria}</span>
                      </td>
                      <td>
                        <code>{r.accion}</code>
                      </td>
                      <td>{r.recurso ? <code>{r.recurso}</code> : '—'}</td>
                      <td className="aud-detalle">{r.detalle ?? '—'}</td>
                      <td>
                        <span className={`ws-pill ws-pill-${TONO_RESULTADO[r.resultado] ?? 'info'}`}>
                          {r.resultado}
                          {r.codigoHttp ? ` · ${r.codigoHttp}` : ''}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        )}

        {datos && datos.total > 0 && (
          <div className="aud-paginacion">
            <button type="button" className="ws-btn ws-btn-ghost ws-btn-sm" onClick={() => setPagina(p => p - 1)} disabled={pagina <= 1 || cargando}>
              Anterior
            </button>
            <span>
              Página {datos.pagina} de {paginas}
            </span>
            <button type="button" className="ws-btn ws-btn-ghost ws-btn-sm" onClick={() => setPagina(p => p + 1)} disabled={pagina >= paginas || cargando}>
              Siguiente
            </button>
          </div>
        )}
      </section>
    </div>
  )
}
