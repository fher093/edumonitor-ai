import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/http.js'
import { IconSync, IconCheck } from '../components/Icons.jsx'
import { fechaHora } from '../utils/formato.js'

// Integración con Moodle para el Administrador Técnico.
// RF-02: configurar URL y token técnico seguros y verificar la conectividad.
// RF-03: sincronizar cursos, matriculados, ítems de calificación, notas y estados, registrando fecha y errores.

const ESTADOS = {
  exitosa: { texto: 'Exitosa', clase: 'state-chip state-chip-ok' },
  con_advertencias: { texto: 'Con advertencias', clase: 'state-chip state-chip-warn' },
  fallida: { texto: 'Fallida', clase: 'state-chip state-chip-danger' },
  en_curso: { texto: 'En curso', clase: 'state-chip state-chip-info' },
}

const DETALLE = {
  error: { texto: 'Error', clase: 'mini-chip mini-chip-danger' },
  advertencia: { texto: 'Advertencia', clase: 'mini-chip mini-chip-warn' },
  info: { texto: 'Info', clase: 'mini-chip mini-chip-info' },
}

function EstadoChip({ estado }) {
  const e = ESTADOS[estado] ?? { texto: estado ?? '—', clase: 'state-chip state-chip-neutral' }
  return <span className={e.clase}>{e.texto}</span>
}

function Kpi({ etiqueta, valor, detalle, tono = 'neutral' }) {
  return (
    <div className={`kpi-card kpi-${tono}`}>
      <span className="kpi-label">{etiqueta}</span>
      <span className="kpi-value">{valor}</span>
      {detalle && <span className="kpi-detail">{detalle}</span>}
    </div>
  )
}

function duracion(s) {
  if (!s?.fechaInicio || !s?.fechaFin) return '—'
  const ms = new Date(s.fechaFin) - new Date(s.fechaInicio)
  return ms < 1000 ? `${ms} ms` : `${(ms / 1000).toFixed(1)} s`
}

function ListaDetalle({ detalle }) {
  if (!detalle?.length) return <p className="text-muted text-sm">Sin errores ni advertencias.</p>
  return (
    <ul className="hallazgo-list">
      {detalle.map((d, i) => {
        const t = DETALLE[d.tipo] ?? DETALLE.info
        return (
          <li key={i} className="hallazgo-item">
            <span className={t.clase}>{t.texto}</span> {d.mensaje}
          </li>
        )
      })}
    </ul>
  )
}

export default function AdminMoodle() {
  // ---------- Conexión ----------
  const [conexion, setConexion] = useState(null)
  const [form, setForm] = useState({ url: '', token: '' })
  const [guardando, setGuardando] = useState(false)
  const [probando, setProbando] = useState(false)
  const [prueba, setPrueba] = useState(null)
  const [avisoConexion, setAvisoConexion] = useState({ tipo: '', texto: '' })

  // ---------- Sincronización ----------
  const [sincronizando, setSincronizando] = useState(false)
  const [ultima, setUltima] = useState(null)
  const [historial, setHistorial] = useState([])
  const [abierto, setAbierto] = useState(null)
  const [errorSync, setErrorSync] = useState('')

  // ---------- Cursos ----------
  const [cursos, setCursos] = useState([])
  const [estadoSync, setEstadoSync] = useState(null)
  const [errorCarga, setErrorCarga] = useState('')

  async function cargarConexion() {
    try {
      const c = await api('/api/moodle/conexion')
      setConexion(c)
      setForm({ url: c.url ?? '', token: '' })
    } catch (err) {
      setErrorCarga(`No se pudo consultar la conexión con Moodle: ${err.message}`)
    }
  }

  async function cargarHistorial() {
    const h = await api('/api/sincronizacion/moodle/historial?limite=15')
    setHistorial(h)
    if (h.length > 0) setUltima(prev => prev ?? h[0])
  }

  async function cargarCursos() {
    setCursos(await api('/api/cursos'))
  }

  async function cargarEstado() {
    setEstadoSync(await api('/api/sincronizacion/moodle/estado'))
  }

  useEffect(() => {
    cargarConexion()
    Promise.all([cargarHistorial(), cargarCursos(), cargarEstado()]).catch(err => setErrorCarga(err.message))
  }, [])

  async function probar() {
    setProbando(true)
    setPrueba(null)
    setAvisoConexion({ tipo: '', texto: '' })
    try {
      // Se prueban los valores escritos; si el token queda vacío se usa el guardado.
      setPrueba(await api('/api/moodle/conexion/probar', {
        method: 'POST',
        body: { url: form.url.trim(), token: form.token.trim() || null },
      }))
    } catch (err) {
      setAvisoConexion({ tipo: 'error', texto: err.message })
    } finally {
      setProbando(false)
    }
  }

  async function guardar(e) {
    e.preventDefault()
    setGuardando(true)
    setAvisoConexion({ tipo: '', texto: '' })
    try {
      const c = await api('/api/moodle/conexion', {
        method: 'PUT',
        body: { url: form.url.trim(), token: form.token.trim() || null },
      })
      setConexion(c)
      setForm({ url: c.url ?? '', token: '' })
      setAvisoConexion({ tipo: 'ok', texto: 'Conexión guardada. El token se almacenó cifrado.' })
    } catch (err) {
      setAvisoConexion({ tipo: 'error', texto: err.message })
    } finally {
      setGuardando(false)
    }
  }

  async function sincronizar() {
    setSincronizando(true)
    setErrorSync('')
    try {
      const r = await api('/api/sincronizacion/moodle', { method: 'POST', body: {} })
      setUltima(r)
      setAbierto(null)
      await Promise.all([cargarHistorial(), cargarCursos()])
    } catch (err) {
      setErrorSync(err.message)
    } finally {
      setSincronizando(false)
    }
  }

  const cursosMoodle = cursos.filter(c => c.origen === 'moodle')

  return (
    <div className="page-container">
      <section className="product-surface-card">
        <div className="section-head-split">
          <div>
            <div className="brand-dot-pill">
              <span className="dot-violet" />
              <span className="tag-uta-micro">ADMINISTRACIÓN TÉCNICA · RF-02 · RF-03</span>
            </div>
            <h1 className="display-headline">Integración con Moodle</h1>
            <p className="lead-subtitle">
              Los cursos, matriculados, actividades y notas se administran en Moodle y se importan a
              EduMonitor para su análisis. El sistema solo lee: nunca escribe en Moodle.
            </p>
          </div>
          <div className="stat-pill-counter">
            <span className="stat-number">{cursosMoodle.length}</span>
            <span className="stat-label">cursos sincronizados</span>
          </div>
        </div>

        <div className="hairline-divider" />

        {errorCarga && <div className="banner-alert banner-alert-error">{errorCarga}</div>}

        <div className="split-panels">
          {/* ================= RF-02: Conexión ================= */}
          <div className="panel-box">
            <div className="panel-head">
              <div>
                <h3 className="panel-title">Conexión con Moodle</h3>
                <p className="text-muted text-sm" style={{ margin: '4px 0 0' }}>
                  Servicio web REST con un usuario técnico de solo lectura.
                </p>
              </div>
              {conexion && (
                <span className={`status-pill ${conexion.tokenConfigurado ? 'status-pill-active' : 'status-pill-neutral'}`}>
                  {conexion.tokenConfigurado
                    ? conexion.origen === 'entorno' ? 'Configurada por entorno' : 'Configurada'
                    : 'Sin configurar'}
                </span>
              )}
            </div>

            {avisoConexion.texto && (
              <div className={`banner-alert ${avisoConexion.tipo === 'ok' ? 'banner-alert-ok' : 'banner-alert-error'}`}>
                {avisoConexion.texto}
              </div>
            )}

            <form onSubmit={guardar} className="form-clean">
              <div className="form-control-item">
                <label htmlFor="moodle-url">URL de Moodle: (*)</label>
                <input
                  id="moodle-url"
                  type="url"
                  required
                  placeholder="https://moodle.uta.edu.ec"
                  value={form.url}
                  onChange={e => setForm({ ...form, url: e.target.value })}
                />
              </div>
              <div className="form-control-item">
                <label htmlFor="moodle-token">Token del servicio web:</label>
                <input
                  id="moodle-token"
                  type="password"
                  autoComplete="off"
                  placeholder={conexion?.tokenConfigurado ? `Guardado: ${conexion.tokenEnmascarado} (déjalo vacío para conservarlo)` : 'Token generado en Moodle'}
                  value={form.token}
                  onChange={e => setForm({ ...form, token: e.target.value })}
                />
                <small className="form-hint-note">
                  Se guarda cifrado (AES-256) y nunca se vuelve a mostrar completo.
                  {conexion?.fechaActualizacion && ` Última actualización: ${fechaHora(conexion.fechaActualizacion)}${conexion.actualizadoPor ? ` por ${conexion.actualizadoPor}` : ''}.`}
                </small>
              </div>
              <div className="form-actions-cluster">
                <button type="submit" className="btn-pill btn-pill-dark" disabled={guardando || probando}>
                  {guardando ? 'Guardando...' : 'Guardar conexión'}
                </button>
                <button type="button" className="btn-pill btn-pill-ghost" onClick={probar} disabled={guardando || probando || !form.url}>
                  {probando ? 'Probando...' : 'Probar conexión'}
                </button>
              </div>
            </form>

            {prueba && (
              <div className={`banner-alert ${prueba.exitosa ? 'banner-alert-ok' : 'banner-alert-error'}`} style={{ marginTop: '1rem' }}>
                <strong>{prueba.mensaje}</strong>
                {prueba.sitio && (
                  <div className="text-sm" style={{ marginTop: 6 }}>
                    Sitio: <strong>{prueba.sitio}</strong> · Moodle {prueba.versionMoodle} · Usuario técnico:{' '}
                    <span className="mono-code-tag">{prueba.usuarioTecnico}</span> · {prueba.latenciaMs} ms
                  </div>
                )}
                {prueba.funcionesFaltantes?.length > 0 && (
                  <div className="text-sm" style={{ marginTop: 6 }}>
                    Agrega al servicio externo: {prueba.funcionesFaltantes.join(', ')}
                  </div>
                )}
              </div>
            )}
          </div>

          {/* ================= RF-03: Sincronización ================= */}
          <div className="panel-box">
            <div className="panel-head">
              <div>
                <h3 className="panel-title">Sincronización</h3>
                <p className="text-muted text-sm" style={{ margin: '4px 0 0' }}>
                  Importa cursos, matriculados, ítems de calificación, notas y estados de actividad.
                </p>
                {estadoSync && (
                  <p className="text-sm" style={{ margin: '6px 0 0' }}>
                    {estadoSync.sincronizacionAutomatica ? (
                      <span className="state-chip state-chip-ok">Automática cada {estadoSync.intervaloMinutos} min</span>
                    ) : (
                      <span className="state-chip state-chip-neutral">Automática desactivada</span>
                    )}
                  </p>
                )}
              </div>
              <button type="button" className="btn-pill btn-pill-dark" onClick={sincronizar} disabled={sincronizando || !conexion?.tokenConfigurado}>
                <IconSync size={15} /> {sincronizando ? 'Sincronizando...' : 'Sincronizar ahora'}
              </button>
            </div>

            {errorSync && <div className="banner-alert banner-alert-error">{errorSync}</div>}

            {!ultima ? (
              <div className="state-empty-box">
                <p className="text-muted">Aún no se ha sincronizado. Verifica la conexión y pulsa “Sincronizar ahora”.</p>
              </div>
            ) : (
              <>
                <div className="chip-row" style={{ marginBottom: 12 }}>
                  <EstadoChip estado={ultima.estado} />
                  <span className="text-muted text-sm">
                    {fechaHora(ultima.fechaFin ?? ultima.fechaInicio)} · {duracion(ultima)} · {ultima.usuarioCorreo ?? 'Automática'}
                  </span>
                </div>
                <p className="text-sm" style={{ marginTop: 0 }}>{ultima.mensaje}</p>
                <div className="kpi-grid">
                  <Kpi etiqueta="Cursos" valor={ultima.cursosImportados} tono="violet"
                    detalle={ultima.cursosOmitidos ? `${ultima.cursosOmitidos} omitido(s)` : 'sin omisiones'} />
                  <Kpi etiqueta="Estudiantes" valor={ultima.estudiantesImportados}
                    detalle={`${ultima.matriculasActivas} matrículas activas${ultima.matriculasRetiradas ? `, ${ultima.matriculasRetiradas} retiradas` : ''}`} />
                  <Kpi etiqueta="Actividades" valor={ultima.actividadesImportadas} detalle="ítems de calificación" />
                  <Kpi etiqueta="Calificaciones" valor={ultima.calificacionesImportadas} tono="ok" detalle="notas y estados" />
                </div>
                <div style={{ marginTop: 14 }}>
                  <ListaDetalle detalle={ultima.detalle} />
                </div>
                {ultima.cursosOmitidos > 0 && (
                  <div className="banner-alert banner-alert-info" style={{ marginTop: 12 }}>
                    Si un curso se omitió porque su profesor no tiene cuenta, créala en{' '}
                    <Link to="/admin" className="text-link">Docentes</Link> con el mismo correo de Moodle y vuelve a sincronizar.
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      </section>

      {/* ================= Historial (fecha y errores) ================= */}
      <section className="product-surface-card" style={{ marginTop: '1.5rem' }}>
        <div className="table-header-bar">
          <div>
            <h3 className="table-title">Historial de sincronizaciones</h3>
            <p className="text-muted text-sm" style={{ margin: 0 }}>Fecha, resultado, contadores y errores de cada ejecución.</p>
          </div>
        </div>
        {historial.length === 0 ? (
          <div className="state-empty-box"><p className="text-muted">Sin registros todavía.</p></div>
        ) : (
          <div className="table-scroll-container">
            <table className="product-data-table compact-table">
              <thead>
                <tr>
                  <th>Fecha</th>
                  <th>Estado</th>
                  <th>Cursos</th>
                  <th>Estudiantes</th>
                  <th>Actividades</th>
                  <th>Notas</th>
                  <th>Duración</th>
                  <th>Usuario</th>
                  <th style={{ textAlign: 'right' }}>Detalle</th>
                </tr>
              </thead>
              <tbody>
                {historial.map(s => (
                  <FilaHistorial key={s.id} s={s} abierto={abierto === s.id} alternar={() => setAbierto(abierto === s.id ? null : s.id)} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {/* ================= Cursos importados ================= */}
      <section className="product-surface-card" style={{ marginTop: '1.5rem' }}>
        <div className="table-header-bar">
          <div>
            <h3 className="table-title">Cursos en EduMonitor ({cursos.length})</h3>
            <p className="text-muted text-sm" style={{ margin: 0 }}>
              Cada curso se asigna al docente cuyo correo coincide con el del profesor en Moodle.
            </p>
          </div>
        </div>
        {cursos.length === 0 ? (
          <div className="state-empty-box"><p className="text-muted">No hay cursos. Ejecuta una sincronización.</p></div>
        ) : (
          <div className="table-scroll-container">
            <table className="product-data-table compact-table">
              <thead>
                <tr>
                  <th>Código</th>
                  <th>Asignatura</th>
                  <th>Período</th>
                  <th>Docente</th>
                  <th>Estudiantes</th>
                  <th>Origen</th>
                  <th>Última sincronización</th>
                </tr>
              </thead>
              <tbody>
                {cursos.map(c => (
                  <tr key={c.id}>
                    <td><span className="mono-code-tag">{c.codigo}</span></td>
                    <td className="cell-student-name">{c.nombre}</td>
                    <td>{c.periodo}</td>
                    <td>{c.docenteNombre}</td>
                    <td>{c.totalEstudiantes}</td>
                    <td>
                      {c.origen === 'moodle'
                        ? <span className="state-chip state-chip-info"><IconCheck size={12} /> Moodle</span>
                        : <span className="state-chip state-chip-neutral">Local</span>}
                    </td>
                    <td>{c.fechaSincronizacion ? fechaHora(c.fechaSincronizacion) : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}

function FilaHistorial({ s, abierto, alternar }) {
  const errores = s.detalle?.filter(d => d.tipo === 'error').length ?? 0
  const avisos = s.detalle?.filter(d => d.tipo === 'advertencia').length ?? 0
  return (
    <>
      <tr>
        <td>{fechaHora(s.fechaInicio)}</td>
        <td><EstadoChip estado={s.estado} /></td>
        <td>{s.cursosImportados}{s.cursosOmitidos ? ` (+${s.cursosOmitidos} omitido)` : ''}</td>
        <td>{s.estudiantesImportados}</td>
        <td>{s.actividadesImportadas}</td>
        <td>{s.calificacionesImportadas}</td>
        <td>{duracion(s)}</td>
        <td className="cell-student-email">{s.usuarioCorreo ?? 'Automática'}</td>
        <td style={{ textAlign: 'right' }}>
          <button type="button" className="btn-pill btn-pill-ghost btn-sm" onClick={alternar}>
            {abierto ? 'Ocultar' : `Ver${errores + avisos ? ` (${errores + avisos})` : ''}`}
          </button>
        </td>
      </tr>
      {abierto && (
        <tr>
          <td colSpan={9}>
            {s.mensaje && <p className="text-sm" style={{ marginTop: 0 }}>{s.mensaje}</p>}
            <ListaDetalle detalle={s.detalle} />
          </td>
        </tr>
      )}
    </>
  )
}
