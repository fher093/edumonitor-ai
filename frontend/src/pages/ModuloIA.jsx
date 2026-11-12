import { useCursoActivo } from '../cursos/CursoContext.jsx'
import { PageHeader } from '../components/ui.jsx'
import { IconAgente, IconInforme } from '../components/Icons.jsx'

/**
 * Vista previa de los módulos del Sprint 3 (HU-12 a HU-16): muestra el prototipo de baja fidelidad
 * documentado en docs/PROTOTIPO_CHAT_INFORME.md (TT-02.3). Los datos son de ejemplo.
 */
export default function ModuloIA({ tipo }) {
  const { cursoActivo } = useCursoActivo()
  const esAgente = tipo === 'agente'
  const curso = cursoActivo ? `${cursoActivo.nombre} (${cursoActivo.codigo})` : 'tu curso'

  return (
    <div className="ws-page ws-page-scroll">
      <PageHeader
        eyebrow="Sprint 3 · prototipo de baja fidelidad"
        titulo={esAgente ? 'Asistente académico' : 'Informe del período'}
        descripcion={
          esAgente
            ? 'Pregunta en lenguaje natural. Las respuestas usan solo datos de tus herramientas y separan hechos, inferencias y recomendaciones (RN-09).'
            : 'Resumen consultable y descargable con fecha de corte, métricas, hallazgos, alertas y recomendaciones (RF-11).'
        }
      >
        <span className="ws-pill ws-pill-brand">Vista previa</span>
      </PageHeader>

      <div className="ws-preview">
        <div className="ws-preview-frame">
          <div className="ws-preview-bar">
            {esAgente ? <IconAgente size={15} /> : <IconInforme size={15} />}
            <span>{curso}</span>
            <span className="ws-preview-sample">Datos de ejemplo</span>
          </div>
          {esAgente ? <ChatPrototipo /> : <InformePrototipo curso={curso} />}
        </div>

        <aside className="ws-preview-notes">
          <span className="ws-eyebrow">Decisiones del prototipo</span>
          <ul className="ws-findings">
            {esAgente ? (
              <>
                <li>Cada respuesta tiene tres secciones: hechos, inferencias y recomendaciones.</li>
                <li>Las fuentes (herramientas) y la fecha de corte van al pie de la respuesta.</li>
                <li>Usa las herramientas de <code>docs/agent-tools-contracts.json</code>, que reutilizan la misma analítica del Sprint 2.</li>
                <li>Si faltan datos, responde “datos insuficientes”; nunca inventa notas (RN-08).</li>
                <li>Modelo local (Ollama) cuando el hardware lo permite (RN-11).</li>
              </>
            ) : (
              <>
                <li>No recalcula nada: compone los endpoints de indicadores, análisis y alertas.</li>
                <li>Encabezado con curso, período, fuente y fecha de corte (RNF-08).</li>
                <li>Las alertas se presentan como señales de seguimiento (RN-07).</li>
                <li>Exportación a PDF para el consejo de facultad.</li>
              </>
            )}
          </ul>
          <p className="ws-muted ws-small">Detalle completo en <code>docs/PROTOTIPO_CHAT_INFORME.md</code>.</p>
        </aside>
      </div>
    </div>
  )
}

function ChatPrototipo() {
  return (
    <div className="ws-chat">
      <div className="ws-chat-msg ws-chat-user">¿Qué estudiantes necesitan apoyo esta semana?</div>
      <div className="ws-chat-msg ws-chat-bot">
        <section>
          <span className="ws-chat-tag ws-chat-tag-fact">Hechos</span>
          <ul>
            <li>3 de 8 estudiantes tienen alertas activas.</li>
            <li>Pedro Castillo: 2 actividades vencidas sin entrega.</li>
            <li>María Gómez: promedio de las últimas 3 = 4,2/10 (antes 8,0).</li>
          </ul>
        </section>
        <section>
          <span className="ws-chat-tag ws-chat-tag-inference">Inferencias</span>
          <ul>
            <li>El descenso de María se concentra en el parcial 2.</li>
          </ul>
        </section>
        <section>
          <span className="ws-chat-tag ws-chat-tag-reco">Recomendaciones</span>
          <ul>
            <li>Contactar a Pedro antes del cierre del parcial 2.</li>
            <li>Revisar con María los temas de la Lección 3.</li>
          </ul>
        </section>
        <footer>Fuentes: identificar_alertas · obtener_progreso · corte 16:20 · señales, no diagnósticos</footer>
      </div>
      <div className="ws-chat-suggest">
        <span>Resumen del curso</span>
        <span>¿Quién no entregó?</span>
        <span>Actividades bajas</span>
      </div>
      <div className="ws-chat-input" aria-disabled="true">
        Escribe tu pregunta... <span className="ws-btn ws-btn-dark ws-btn-sm">Enviar</span>
      </div>
    </div>
  )
}

function InformePrototipo({ curso }) {
  return (
    <div className="ws-report">
      <header>
        <strong>{curso}</strong>
        <span className="ws-muted ws-small">Fecha de corte: 10/11/2026 16:20 · Fuente: Moodle (sincronización automática)</span>
      </header>
      <section>
        <h3>1. Métricas</h3>
        <p>Promedio 13,4/20 (σ 3,1) · 7/8 actividades evaluadas · Finalización 78 %</p>
      </section>
      <section>
        <h3>2. Hallazgos</h3>
        <p>La Lección 3 tiene un logro del 52 % (bajo el umbral del 60 %). Tendencia del grupo: descenso entre parciales.</p>
      </section>
      <section>
        <h3>3. Alertas (señales de seguimiento)</h3>
        <p>María Gómez · Descenso (k=3): 8,0 → 4,2/10. Pedro Castillo · Atraso (n=2): 2 vencidas sin entrega.</p>
      </section>
      <section>
        <h3>4. Recomendaciones</h3>
        <p>Tutoría grupal sobre los temas de la Lección 3.</p>
      </section>
      <footer className="ws-muted ws-small">Generado por EduMonitor AI. No sustituye el criterio del docente.</footer>
    </div>
  )
}
