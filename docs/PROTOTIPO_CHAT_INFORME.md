# Prototipo de baja fidelidad: chat del agente e informe (TT-02.3)

**Sprint:** 2 · **Historias que prepara:** HU-12 a HU-16 (Sprint 3) · **Requisitos:** RF-09, RF-10, RF-11, RNF-04, RNF-08 · **Reglas:** RN-07, RN-09, RN-11

Este prototipo define la estructura y el comportamiento de las dos pantallas del Sprint 3 antes de programarlas. En el frontend ya existen como **vista previa** (`frontend/src/components/ModuloPreview.jsx`) en las rutas `/agente` e `/informe` del docente. El estilo visual sigue `DESIGN.md`.

---

## 1. Chat del agente académico (`/agente`)

### 1.1 Objetivo

El docente pregunta en lenguaje natural por sus cursos. El agente responde **solo con datos devueltos por sus herramientas** (RN-09) y separa en cada respuesta:

- **Hechos:** cifras tomadas de las herramientas.
- **Inferencias:** interpretaciones de esos datos.
- **Recomendaciones:** acciones concretas de apoyo.

### 1.2 Wireframe

```
┌───────────────────────────────────────────────────────────────────────────────┐
│  ASISTENTE IA                                   [Asignatura: SW-401 ▾] [2026-B]│
│  Consultas académicas asistidas         Datos: Moodle · sincronizado 10/11 16:20│
├───────────────────────────────────────────────────────────────────────────────┤
│                                                                               │
│                         ┌───────────────────────────────────────────────┐     │
│                         │ ¿Qué estudiantes necesitan apoyo esta semana? │ (docente)
│                         └───────────────────────────────────────────────┘     │
│  ┌─────────────────────────────────────────────────────────────────────┐      │
│  │ ● HECHOS                                                            │      │
│  │   · 3 de 8 estudiantes tienen alertas activas.                      │      │
│  │   · Pedro Castillo: 2 actividades vencidas sin entrega.             │      │
│  │   · María Gómez: promedio de las últimas 3 = 4,2/10 (antes 8,0).    │      │
│  │ ● INFERENCIAS                                                       │      │
│  │   · El descenso de María se concentra en el parcial 2.              │      │
│  │ ● RECOMENDACIONES                                                   │      │
│  │   · Contactar a Pedro antes del cierre del parcial 2.               │      │
│  │   · Revisar con María los temas de la Lección 3.                    │      │
│  │                                                                     │      │
│  │  Fuentes: identificar_alertas · obtener_progreso   ⓘ corte 16:20    │      │
│  │  Son señales de seguimiento, no diagnósticos.                       │      │
│  └─────────────────────────────────────────────────────────────────────┘      │
│                                                                               │
│  Sugerencias: [Resumen del curso] [¿Quién no entregó?] [Actividades bajas]    │
├───────────────────────────────────────────────────────────────────────────────┤
│  [ Escribe tu pregunta...                                         ] [Enviar ▶]│
└───────────────────────────────────────────────────────────────────────────────┘
```

### 1.3 Comportamiento

| Situación | Respuesta esperada |
| --- | --- |
| Pregunta sobre un curso propio | Usa las herramientas y muestra hechos, inferencias y recomendaciones, con las fuentes al pie. |
| Pregunta sobre un curso ajeno | “No tienes acceso a ese curso.” Las herramientas aplican el mismo control que la API (RN-12). |
| Datos insuficientes | “Datos insuficientes: …” con el mensaje de la herramienta. No inventa notas ni estudiantes (RN-08, RN-09). |
| Pregunta fuera del ámbito académico | Recuerda que solo responde sobre los cursos autorizados. |
| Herramienta caída | Avisa del error y sugiere reintentar. No responde de memoria. |

### 1.4 Herramientas que usa (contratos en `docs/agent-tools-contracts.json`)

`listar_cursos_autorizados`, `obtener_calificaciones`, `obtener_progreso`, `identificar_alertas`, `generar_resumen`. Todas reutilizan `AnaliticaService`, la misma lógica del dashboard y de las alertas del Sprint 2, de modo que el chat y las pantallas nunca se contradicen.

### 1.5 Privacidad (RN-11)

El modelo de lenguaje se ejecuta localmente (Ollama) cuando el hardware lo permite. A un servicio externo no se envían nombres ni cédulas sin autorización.

---

## 2. Informe del período (`/informe`)

### 2.1 Objetivo

Documento consultable en pantalla y descargable en PDF (RF-11) que resume el seguimiento de un curso con una **fecha de corte** explícita.

### 2.2 Wireframe

```
┌───────────────────────────────────────────────────────────────────────────────┐
│  INFORMES ACADÉMICOS                    [Asignatura: SW-401 ▾] [Período ▾]     │
│  Informe de seguimiento                       [Vista previa] [Descargar PDF ⤓] │
├───────────────────────────────────────────────────────────────────────────────┤
│  Metodologías Ágiles (SW-401) · 2026-B · Docente: Docente de prueba            │
│  Fecha de corte: 10/11/2026 16:20 · Fuente: Moodle (sincronización automática) │
│                                                                               │
│  1. MÉTRICAS                                                                  │
│     Promedio 13,4/20 (σ 3,1) · 7/8 actividades evaluadas · Finalización 78 %  │
│     Distribución: ██ 0–40 %  ███ 40–60 %  █████ 60–80 %  ████ 80–100 %        │
│                                                                               │
│  2. HALLAZGOS                                                                 │
│     · La Lección 3 tiene un logro del 52 % (bajo el umbral del 60 %).         │
│     · Tendencia del grupo: descenso entre el parcial 1 y el parcial 2.        │
│                                                                               │
│  3. ALERTAS (señales de seguimiento)                                          │
│     Estudiante        Regla              Evidencia                            │
│     María Gómez       Descenso (k=3)     8,0 → 4,2 /10 (Δ 3,8 > 1,0)          │
│     Pedro Castillo    Atraso (n=2)       2 vencidas sin entrega               │
│                                                                               │
│  4. RECOMENDACIONES                                                           │
│     · Tutoría grupal sobre los temas de la Lección 3.                         │
│                                                                               │
│  Generado por EduMonitor AI. No sustituye el criterio del docente.            │
└───────────────────────────────────────────────────────────────────────────────┘
```

### 2.3 Contenido mínimo (RF-11)

| Sección | Fuente en el sistema |
| --- | --- |
| Encabezado: curso, período, docente, **fecha de corte** y fuente de los datos | `CursoDto`, `EstadoSincronizacionDto` (RNF-08) |
| Métricas | `GET /api/indicadores/curso/{id}` (HU-07) |
| Hallazgos y tendencia | `GET /api/analisis/curso/{id}` (HU-11) |
| Alertas con evidencia | `GET /api/alertas/curso/{id}` (HU-10, RN-07) |
| Recomendaciones | Agente (HU-15), marcadas como sugerencias |

---

## 3. Decisiones tomadas en el prototipo

1. **Mismo selector de curso y período** que las demás pantallas (`SelectorCurso`), con la fuente y la fecha de los datos visibles (RNF-08).
2. **Las respuestas del chat siempre tienen las tres secciones**, aunque alguna quede vacía (“Sin recomendaciones con los datos actuales”).
3. **Las fuentes y la fecha de corte van al pie de cada respuesta.** Es la evidencia que pide RN-07.
4. **El informe no recalcula nada:** compone los mismos endpoints del Sprint 2, de modo que coincide con el dashboard.
5. **Validación con el Product Owner** en la Sprint Review del 12-nov-2026, antes de implementar en el Sprint 3.
