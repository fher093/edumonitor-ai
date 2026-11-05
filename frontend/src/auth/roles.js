// Los valores deben coincidir con Backend.Domain.RolNombres
export const ROLES = {
  DOCENTE: 'Docente',
  ADMIN: 'AdministradorTecnico',
}

export const ETIQUETA_ROL = {
  [ROLES.DOCENTE]: 'Docente',
  [ROLES.ADMIN]: 'Administrador técnico',
}

// Pantalla a la que se redirige a cada rol después de iniciar sesión
export function rutaInicial(rol) {
  return rol === ROLES.ADMIN ? '/admin' : '/matricula'
}
