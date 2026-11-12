import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/http.js'
import { IconClose, IconEdit, IconPlus, IconSearch } from '../components/Icons.jsx'
import { fechaHora } from '../utils/formato.js'

const RUTA = '/api/usuarios/docentes'
const FORM_VACIO = { nombre: '', correo: '', claveTemporal: '' }

/** Clave temporal legible: 4 letras + 4 números + 2 letras (cumple la política: ≥ 8, letras y números). */
function generarClave() {
  const letras = 'abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ'
  const digitos = '23456789'
  const azar = (fuente, n) => {
    const valores = crypto.getRandomValues(new Uint32Array(n))
    return Array.from(valores, v => fuente[v % fuente.length]).join('')
  }
  return `${azar(letras, 4)}${azar(digitos, 4)}${azar(letras, 2)}`
}

function claveValida(clave) {
  return clave.length >= 8 && /\p{L}/u.test(clave) && /\d/.test(clave)
}

/**
 * Gestión de cuentas de docentes (HU-02). El correo de cada cuenta debe coincidir con el del docente
 * en Moodle: la sincronización usa el correo para asignarle sus cursos.
 */
export default function AdminDocentes() {
  const [docentes, setDocentes] = useState([])
  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState('')
  const [aviso, setAviso] = useState('')
  const [filtro, setFiltro] = useState('')
  const [procesandoId, setProcesandoId] = useState(null)

  // Modal crear/editar: { modo: 'crear' | 'editar', docente? }
  const [modal, setModal] = useState(null)
  const [form, setForm] = useState(FORM_VACIO)
  const [guardando, setGuardando] = useState(false)
  const [errorModal, setErrorModal] = useState('')

  // Modal restablecer clave
  const [claveDe, setClaveDe] = useState(null)
  const [nuevaClave, setNuevaClave] = useState('')

  useEffect(() => {
    let vivo = true
    api(RUTA)
      .then(data => vivo && setDocentes(data))
      .catch(err => vivo && setError(err.message))
      .finally(() => vivo && setCargando(false))
    return () => {
      vivo = false
    }
  }, [])

  useEffect(() => {
    if (!modal && !claveDe) return
    const cerrar = e => {
      if (e.key === 'Escape' && !guardando) cerrarModales()
    }
    window.addEventListener('keydown', cerrar)
    return () => window.removeEventListener('keydown', cerrar)
  }, [modal, claveDe, guardando])

  const filtrados = useMemo(() => {
    const q = filtro.trim().toLowerCase()
    if (!q) return docentes
    return docentes.filter(d => d.nombre.toLowerCase().includes(q) || d.correo.includes(q))
  }, [docentes, filtro])

  const sinCursos = docentes.filter(d => d.activo && d.cursosAsignados === 0).length

  function reemplazar(actualizado) {
    setDocentes(prev => {
      const existe = prev.some(d => d.id === actualizado.id)
      const lista = existe ? prev.map(d => (d.id === actualizado.id ? actualizado : d)) : [...prev, actualizado]
      return lista.sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'))
    })
  }

  function cerrarModales() {
    setModal(null)
    setClaveDe(null)
    setErrorModal('')
  }

  function abrirCrear() {
    setForm({ ...FORM_VACIO, claveTemporal: generarClave() })
    setErrorModal('')
    setModal({ modo: 'crear' })
  }

  function abrirEditar(docente) {
    setForm({ nombre: docente.nombre, correo: docente.correo, claveTemporal: '' })
    setErrorModal('')
    setModal({ modo: 'editar', docente })
  }

  function abrirClave(docente) {
    setNuevaClave(generarClave())
    setErrorModal('')
    setClaveDe(docente)
  }

  async function guardar(e) {
    e.preventDefault()
    if (modal.modo === 'crear' && !claveValida(form.claveTemporal)) {
      setErrorModal('La clave temporal debe tener al menos 8 caracteres, con letras y números.')
      return
    }
    setGuardando(true)
    setErrorModal('')
    try {
      const nombre = form.nombre.trim()
      const correo = form.correo.trim()
      const actualizado =
        modal.modo === 'crear'
          ? await api(RUTA, { method: 'POST', body: { nombre, correo, claveTemporal: form.claveTemporal } })
          : await api(`${RUTA}/${modal.docente.id}`, { method: 'PUT', body: { nombre, correo } })
      reemplazar(actualizado)
      setAviso(
        modal.modo === 'crear'
          ? `Cuenta creada para ${actualizado.nombre}. Comparte la clave temporal por un canal seguro.`
          : `Datos de ${actualizado.nombre} actualizados.`
      )
      cerrarModales()
    } catch (err) {
      setErrorModal(err.message)
    } finally {
      setGuardando(false)
    }
  }

  async function guardarClave(e) {
    e.preventDefault()
    if (!claveValida(nuevaClave)) {
      setErrorModal('La clave temporal debe tener al menos 8 caracteres, con letras y números.')
      return
    }
    setGuardando(true)
    setErrorModal('')
    try {
      const actualizado = await api(`${RUTA}/${claveDe.id}/restablecer-clave`, {
        method: 'POST',
        body: { claveTemporal: nuevaClave },
      })
      reemplazar(actualizado)
      setAviso(`Clave de ${actualizado.nombre} restablecida. Comparte la nueva clave por un canal seguro.`)
      cerrarModales()
    } catch (err) {
      setErrorModal(err.message)
    } finally {
      setGuardando(false)
    }
  }

  async function accion(docente, ejecutar, mensaje) {
    setProcesandoId(docente.id)
    setError('')
    setAviso('')
    try {
      const actualizado = await ejecutar()
      reemplazar(actualizado)
      setAviso(mensaje(actualizado))
    } catch (err) {
      setError(err.message)
    } finally {
      setProcesandoId(null)
    }
  }

  function alternarEstado(docente) {
    if (docente.activo && !window.confirm(`¿Desactivar la cuenta de ${docente.nombre}? No podrá iniciar sesión.`)) return
    accion(
      docente,
      () => api(`${RUTA}/${docente.id}/estado`, { method: 'PUT', body: { activo: !docente.activo } }),
      d => `Cuenta de ${d.nombre} ${d.activo ? 'activada' : 'desactivada'}.`
    )
  }

  function desbloquear(docente) {
    accion(
      docente,
      () => api(`${RUTA}/${docente.id}/desbloquear`, { method: 'POST' }),
      d => `Cuenta de ${d.nombre} desbloqueada.`
    )
  }

  return (
    <div className="page-container">
      <section className="product-surface-card">
        <div className="section-head-split">
          <div>
            <div className="brand-dot-pill">
              <span className="dot-violet" />
              <span className="tag-uta-micro">ADMINISTRACIÓN TÉCNICA · HU-02</span>
            </div>
            <h1 className="display-headline">Cuentas de Docentes</h1>
            <p className="lead-subtitle">
              Crea y administra el acceso de los docentes. Usa el <strong>mismo correo que el docente tiene en
              Moodle</strong>: la sincronización le asigna sus cursos por ese correo.
            </p>
          </div>
          <div className="stat-pill-counter">
            <span className="stat-number">{docentes.length}</span>
            <span className="stat-label">docentes</span>
          </div>
        </div>

        <div className="hairline-divider" />

        {error && <div className="banner-alert banner-alert-error">{error}</div>}
        {aviso && <div className="banner-alert banner-alert-ok" role="status">{aviso}</div>}
        {!cargando && sinCursos > 0 && (
          <div className="banner-alert banner-alert-info">
            {sinCursos === 1 ? '1 docente activo no tiene' : `${sinCursos} docentes activos no tienen`} cursos
            asignados. Revisa que su correo coincida con el de Moodle y vuelve a{' '}
            <Link to="/admin/moodle" className="text-link">sincronizar</Link>.
          </div>
        )}

        <div className="product-toolbar-row">
          <div className="toolbar-search-field">
            <IconSearch className="search-field-icon" size={16} />
            <input
              type="text"
              className="input-text-clean"
              placeholder="Buscar por nombre o correo..."
              value={filtro}
              onChange={e => setFiltro(e.target.value)}
              aria-label="Buscar docentes"
            />
          </div>
          <div className="toolbar-buttons-cluster">
            <button type="button" className="btn-pill btn-pill-dark" onClick={abrirCrear}>
              <IconPlus size={15} />
              <span>Nuevo docente</span>
            </button>
          </div>
        </div>

        {cargando ? (
          <div className="state-empty-box">
            <p className="text-muted">Cargando cuentas...</p>
          </div>
        ) : (
          <div className="table-scroll-container">
            <table className="product-data-table">
              <thead>
                <tr>
                  <th>Docente</th>
                  <th>Estado</th>
                  <th>Cursos</th>
                  <th>Último acceso</th>
                  <th style={{ textAlign: 'right' }}>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {filtrados.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="cell-empty-message">
                      {docentes.length === 0
                        ? 'Aún no hay cuentas de docentes. Crea la primera con “Nuevo docente”.'
                        : `No hay docentes que coincidan con “${filtro}”.`}
                    </td>
                  </tr>
                ) : (
                  filtrados.map(d => {
                    const ocupado = procesandoId === d.id
                    return (
                      <tr key={d.id}>
                        <td>
                          <div className="cell-student-name">{d.nombre}</div>
                          <div className="text-muted text-xs">{d.correo}</div>
                        </td>
                        <td>
                          {!d.activo ? (
                            <span className="state-chip state-chip-neutral">Inactivo</span>
                          ) : d.bloqueado ? (
                            <span className="state-chip state-chip-danger" title={`Hasta ${fechaHora(d.bloqueadoHasta)}`}>
                              Bloqueado
                            </span>
                          ) : (
                            <span className="state-chip state-chip-ok">Activo</span>
                          )}
                        </td>
                        <td>
                          {d.cursosAsignados > 0 ? (
                            d.cursosAsignados
                          ) : (
                            <span className="state-chip state-chip-warn" title="Su correo no coincide con ningún profesor de Moodle sincronizado">
                              Sin cursos
                            </span>
                          )}
                        </td>
                        <td className="text-muted text-sm">{d.ultimoAcceso ? fechaHora(d.ultimoAcceso) : 'Nunca'}</td>
                        <td style={{ textAlign: 'right' }}>
                          <div className="table-actions-row">
                            {d.bloqueado && d.activo && (
                              <button
                                type="button"
                                className="btn-pill btn-pill-dark btn-sm"
                                onClick={() => desbloquear(d)}
                                disabled={ocupado}
                              >
                                Desbloquear
                              </button>
                            )}
                            <button
                              type="button"
                              className="btn-pill btn-pill-ghost btn-sm"
                              onClick={() => abrirEditar(d)}
                              disabled={ocupado}
                              title="Editar nombre y correo"
                              aria-label={`Editar a ${d.nombre}`}
                            >
                              <IconEdit size={14} />
                            </button>
                            <button
                              type="button"
                              className="btn-pill btn-pill-ghost btn-sm"
                              onClick={() => abrirClave(d)}
                              disabled={ocupado}
                            >
                              Restablecer clave
                            </button>
                            <button
                              type="button"
                              className={`btn-pill btn-sm ${d.activo ? 'btn-desmatr-danger' : 'btn-pill-ghost'}`}
                              onClick={() => alternarEstado(d)}
                              disabled={ocupado}
                            >
                              {d.activo ? 'Desactivar' : 'Activar'}
                            </button>
                          </div>
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>
        )}

        <div className="footnote-meta">
          <span>
            Las cuentas no se eliminan para conservar sus cursos y el historial: desactívalas. Tras cinco intentos
            fallidos de inicio de sesión la cuenta se bloquea unos minutos.
          </span>
        </div>
      </section>

      {/* ============ MODAL: CREAR / EDITAR DOCENTE ============ */}
      {modal && (
        <div
          className="modal-backdrop-blur"
          role="dialog"
          aria-modal="true"
          aria-labelledby="titulo-docente"
          onClick={e => e.target === e.currentTarget && !guardando && cerrarModales()}
        >
          <div className="modal-surface-card">
            <div className="modal-head-row">
              <div>
                <span className="modal-eyebrow">{modal.modo === 'crear' ? 'Nueva cuenta' : 'Edición'}</span>
                <h3 className="modal-title" id="titulo-docente">
                  {modal.modo === 'crear' ? 'Registrar docente' : 'Editar docente'}
                </h3>
              </div>
              <button type="button" className="modal-close-icon-btn" onClick={cerrarModales} aria-label="Cerrar">
                <IconClose size={16} />
              </button>
            </div>

            {errorModal && <div className="banner-alert banner-alert-error">{errorModal}</div>}

            <form onSubmit={guardar} className="form-clean">
              <div className="form-control-item">
                <label htmlFor="doc-nombre">Nombres y apellidos (*)</label>
                <input
                  id="doc-nombre"
                  type="text"
                  required
                  minLength={2}
                  maxLength={150}
                  value={form.nombre}
                  onChange={e => setForm({ ...form, nombre: e.target.value })}
                  placeholder="Ej. María Fernanda López"
                  autoFocus
                />
              </div>
              <div className="form-control-item">
                <label htmlFor="doc-correo">Correo (*)</label>
                <input
                  id="doc-correo"
                  type="email"
                  required
                  maxLength={254}
                  value={form.correo}
                  onChange={e => setForm({ ...form, correo: e.target.value })}
                  placeholder="El mismo correo que usa en Moodle"
                />
                <span className="form-hint-note">
                  Debe coincidir con el correo del profesor en Moodle para que se le asignen sus cursos.
                </span>
              </div>
              {modal.modo === 'crear' && (
                <div className="form-control-item">
                  <label htmlFor="doc-clave">Clave temporal (*)</label>
                  <div className="table-actions-row">
                    <input
                      id="doc-clave"
                      type="text"
                      required
                      minLength={8}
                      maxLength={128}
                      autoComplete="off"
                      value={form.claveTemporal}
                      onChange={e => setForm({ ...form, claveTemporal: e.target.value })}
                    />
                    <button
                      type="button"
                      className="btn-pill btn-pill-ghost btn-sm"
                      onClick={() => setForm({ ...form, claveTemporal: generarClave() })}
                    >
                      Generar
                    </button>
                  </div>
                  <span className="form-hint-note">Mínimo 8 caracteres, con letras y números.</span>
                </div>
              )}

              <div className="modal-footer-actions">
                <button type="button" className="btn-pill btn-pill-ghost" onClick={cerrarModales} disabled={guardando}>
                  Cancelar
                </button>
                <button type="submit" className="btn-pill btn-pill-dark" disabled={guardando}>
                  {guardando ? 'Guardando...' : modal.modo === 'crear' ? 'Crear cuenta' : 'Guardar cambios'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ============ MODAL: RESTABLECER CLAVE ============ */}
      {claveDe && (
        <div
          className="modal-backdrop-blur"
          role="dialog"
          aria-modal="true"
          aria-labelledby="titulo-clave"
          onClick={e => e.target === e.currentTarget && !guardando && cerrarModales()}
        >
          <div className="modal-surface-card">
            <div className="modal-head-row">
              <div>
                <span className="modal-eyebrow">Acceso</span>
                <h3 className="modal-title" id="titulo-clave">Restablecer clave</h3>
                <p className="modal-subtitle">
                  {claveDe.nombre} · {claveDe.correo}
                </p>
              </div>
              <button type="button" className="modal-close-icon-btn" onClick={cerrarModales} aria-label="Cerrar">
                <IconClose size={16} />
              </button>
            </div>

            {errorModal && <div className="banner-alert banner-alert-error">{errorModal}</div>}

            <form onSubmit={guardarClave} className="form-clean">
              <div className="form-control-item">
                <label htmlFor="nueva-clave">Nueva clave temporal (*)</label>
                <div className="table-actions-row">
                  <input
                    id="nueva-clave"
                    type="text"
                    required
                    minLength={8}
                    maxLength={128}
                    autoComplete="off"
                    value={nuevaClave}
                    onChange={e => setNuevaClave(e.target.value)}
                    autoFocus
                  />
                  <button type="button" className="btn-pill btn-pill-ghost btn-sm" onClick={() => setNuevaClave(generarClave())}>
                    Generar
                  </button>
                </div>
                <span className="form-hint-note">
                  La clave anterior deja de funcionar y la cuenta se desbloquea.
                </span>
              </div>

              <div className="modal-footer-actions">
                <button type="button" className="btn-pill btn-pill-ghost" onClick={cerrarModales} disabled={guardando}>
                  Cancelar
                </button>
                <button type="submit" className="btn-pill btn-pill-dark" disabled={guardando}>
                  {guardando ? 'Guardando...' : 'Restablecer clave'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
