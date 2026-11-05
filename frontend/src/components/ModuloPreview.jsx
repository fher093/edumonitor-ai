import { Link } from 'react-router-dom'

const INFO_MODULOS = {
  dashboard: {
    categoria: 'ANALÍTICA DEL CURSO',
    titulo: 'Panel de Rendimiento Académico',
    descripcion:
      'Consulta el promedio del curso, la distribución de calificaciones y el avance de las actividades evaluadas en una sola vista.',
    detalles: [
      { titulo: 'Promedio del curso:', texto: 'Cálculo ponderado según los componentes de evaluación.' },
      { titulo: 'Distribución de notas:', texto: 'Visualiza la dispersión y la desviación del rendimiento.' },
      { titulo: 'Filtros por parcial:', texto: 'Segmenta la información por período o estado de actividades.' },
    ],
    accion: 'Gestionar matrícula',
    rutaAccion: '/matricula',
  },
  alertas: {
    categoria: 'ALERTAS TEMPRANAS',
    titulo: 'Seguimiento Oportuno del Estudiante',
    descripcion:
      'Identifica de forma temprana a los estudiantes que requieren acompañamiento y revisa la evidencia que respalda cada alerta.',
    detalles: [
      { titulo: 'Descenso continuo:', texto: 'Detecta tres calificaciones consecutivas con caída significativa.' },
      { titulo: 'Entregas tardías:', texto: 'Señala a estudiantes con dos o más entregas fuera de fecha.' },
      { titulo: 'Baja finalización:', texto: 'Advierte cuando la participación cae por debajo del 60 %.' },
    ],
    accion: 'Ver nómina del curso',
    rutaAccion: '/matricula',
  },
  analisis: {
    categoria: 'ANÁLISIS GRUPAL',
    titulo: 'Diagnóstico por Actividad y Tendencias',
    descripcion:
      'Analiza el desempeño del grupo por actividad y detecta dificultades estructurales del curso antes de que afecten al estudiantado.',
    detalles: [
      { titulo: 'Brechas por actividad:', texto: 'Identifica evaluaciones con promedio bajo el umbral de riesgo.' },
      { titulo: 'Diagnóstico del grupo:', texto: 'Diferencia dificultades del tema de casos individuales.' },
      { titulo: 'Evolución temporal:', texto: 'Compara el desempeño entre el primero y el segundo parcial.' },
    ],
    accion: 'Volver a mis cursos',
    rutaAccion: '/cursos',
  },
  informe: {
    categoria: 'INFORMES ACADÉMICOS',
    titulo: 'Reportes de Seguimiento del Período',
    descripcion:
      'Genera informes con métricas, hallazgos y acciones de seguimiento para documentar el avance académico del curso.',
    detalles: [
      { titulo: 'Reporte consolidado:', texto: 'Métricas, hallazgos y acciones de seguimiento del período.' },
      { titulo: 'Exportación:', texto: 'Descarga el informe en PDF para el consejo de facultad.' },
      { titulo: 'Trazabilidad:', texto: 'Conserva el registro de alertas y decisiones pedagógicas.' },
    ],
    accion: 'Ir a matrícula',
    rutaAccion: '/matricula',
  },
  agente: {
    categoria: 'ASISTENTE IA',
    titulo: 'Consultas Académicas Asistidas',
    descripcion:
      'Pregunta por tus cursos, calificaciones y avance del estudiantado en lenguaje natural, con respuestas basadas en tus datos autorizados.',
    detalles: [
      { titulo: 'Lenguaje natural:', texto: 'Consulta cursos, notas y progreso sin escribir filtros complejos.' },
      { titulo: 'Respuestas con evidencia:', texto: 'Distingue datos, inferencias y recomendaciones.' },
      { titulo: 'Acceso controlado:', texto: 'Solo consulta la información académica permitida para tu rol.' },
    ],
    accion: 'Gestionar mis cursos',
    rutaAccion: '/cursos',
  },
  auditoria: {
    categoria: 'ADMINISTRACIÓN TÉCNICA',
    titulo: 'Trazabilidad de Cambios Académicos',
    descripcion:
      'Revisa el historial de cambios académicos, consultas y accesos por rol para mantener la trazabilidad del sistema.',
    detalles: [
      { titulo: 'Registro de cambios:', texto: 'Historial de matrículas y modificaciones de calificaciones.' },
      { titulo: 'Control de accesos:', texto: 'Trazabilidad de consultas por rol y herramienta utilizada.' },
      { titulo: 'Seguridad:', texto: 'Sin exposición de credenciales ni datos sensibles.' },
    ],
    accion: 'Volver a cursos',
    rutaAccion: '/admin',
  },
}

const MODULO_GENERICO = {
  categoria: 'MÓDULO ACADÉMICO',
  titulo: 'Sección en preparación',
  descripcion: 'Esta sección estará disponible próximamente dentro de la plataforma.',
  detalles: [],
  accion: 'Volver al inicio',
  rutaAccion: '/cursos',
}

export default function ModuloPreview({ tipo, cursoActivo }) {
  const mod = INFO_MODULOS[tipo] || MODULO_GENERICO

  return (
    <div className="product-surface-card preview-card-clickup">
      <div className="brand-dot-pill">
        <span className="dot-violet" />
        <span className="tag-uta-micro">{mod.categoria}</span>
      </div>

      <h1 className="display-headline preview-headline">{mod.titulo}</h1>
      <p className="lead-subtitle preview-lead">{mod.descripcion}</p>

      {cursoActivo && (
        <div className="preview-context-tag-pill">
          <span className="context-indicator-dot" />
          <span>Curso activo:</span>
          <strong>
            {cursoActivo.nombre} ({cursoActivo.codigo})
          </strong>
          <span className="text-muted">• {cursoActivo.periodo}</span>
        </div>
      )}

      <div className="preview-checklist-container">
        {mod.detalles.length > 0 && (
          <>
            <h4 className="preview-features-heading">Funciones del módulo</h4>
            <ul className="clickup-checklist">
              {mod.detalles.map((d, i) => (
                <li key={i} className="clickup-check-item">
                  <span className="clickup-check-icon" aria-hidden="true">✓</span>
                  <div className="check-text-wrapper">
                    <strong className="check-lead-text">{d.titulo} </strong>
                    <span className="check-body-text">{d.texto}</span>
                  </div>
                </li>
              ))}
            </ul>
          </>
        )}

        <div className="preview-callout-banner">
          <span className="callout-badge-violet">Vista previa</span>
          <p className="callout-text">
            Este módulo se encuentra en preparación. Mientras tanto, puedes gestionar tus asignaturas y la
            matrícula del estudiantado desde las secciones disponibles.
          </p>
        </div>

        <div className="preview-cta-row">
          <Link to={mod.rutaAccion} className="btn-pill btn-pill-dark">
            {mod.accion}
          </Link>
        </div>
      </div>
    </div>
  )
}
