// Formato compartido por las vistas del Sprint 2.
export const ETIQUETAS_ESTADO = {
  calificada: 'Calificada',
  nota_cero: 'Nota cero',
  sin_entrega: 'Sin entrega',
  pendiente: 'Pendiente de calificar',
  completada: 'Completada sin nota',
}

export const ETIQUETAS_COMPONENTE = {
  contacto_docente: 'Contacto con el docente',
  practico_experimental: 'Práctico-experimental',
  autonomo: 'Autónomo',
}

export function etiquetaEstado(estado) {
  return ETIQUETAS_ESTADO[estado] ?? 'Sin estado'
}

export function claseEstado(estado) {
  switch (estado) {
    case 'calificada':
      return 'state-chip state-chip-ok'
    case 'nota_cero':
      return 'state-chip state-chip-danger'
    case 'sin_entrega':
      return 'state-chip state-chip-warn'
    case 'completada':
      return 'state-chip state-chip-info'
    default:
      return 'state-chip state-chip-neutral'
  }
}

export function numero(valor, decimales = 2) {
  if (valor === null || valor === undefined || Number.isNaN(Number(valor))) return '—'
  return Number(valor).toFixed(decimales)
}

export function porcentaje(valor, decimales = 1) {
  if (valor === null || valor === undefined) return '—'
  return `${Number(valor).toFixed(decimales)} %`
}

export function fecha(valor) {
  if (!valor) return 'Sin fecha'
  const d = new Date(valor)
  if (Number.isNaN(d.getTime())) return 'Sin fecha'
  return d.toLocaleDateString('es-EC', { year: 'numeric', month: 'short', day: '2-digit' })
}

export function fechaHora(valor) {
  if (!valor) return '—'
  const d = new Date(valor)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString('es-EC', {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** Valor para <input type="datetime-local"> a partir de un ISO de la API. */
export function paraInputFecha(valor) {
  if (!valor) return ''
  const d = new Date(valor)
  if (Number.isNaN(d.getTime())) return ''
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}
