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

/**
 * La API guarda las fechas en UTC, pero SQL Server las devuelve sin zona horaria
 * ("2026-11-10T00:28:47"). Se interpretan como UTC para mostrarlas en hora local.
 */
export function aFecha(valor) {
  if (!valor) return null
  if (valor instanceof Date) return valor
  const texto = String(valor)
  const sinZona = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/.test(texto)
  const d = new Date(sinZona ? `${texto}Z` : texto)
  return Number.isNaN(d.getTime()) ? null : d
}

export function fecha(valor) {
  if (!valor) return 'Sin fecha'
  const d = aFecha(valor) ?? new Date(NaN)
  if (Number.isNaN(d.getTime())) return 'Sin fecha'
  return d.toLocaleDateString('es-EC', { year: 'numeric', month: 'short', day: '2-digit' })
}

export function fechaHora(valor) {
  if (!valor) return '—'
  const d = aFecha(valor) ?? new Date(NaN)
  if (Number.isNaN(d.getTime())) return '—'
  return d.toLocaleString('es-EC', {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

/** Siglas UTA de los componentes de evaluación (la etiqueta completa va en el title). */
export const SIGLA_COMPONENTE = {
  contacto_docente: 'CD',
  practico_experimental: 'PE',
  autonomo: 'AA',
}

export function siglaComponente(componente) {
  return SIGLA_COMPONENTE[componente] ?? componente
}

/** Etiqueta corta del tipo de alerta, para listas densas. */
export const TIPO_ALERTA_CORTO = {
  descenso: 'Descenso',
  atraso: 'Atraso',
  baja_finalizacion: 'Finalización',
}
