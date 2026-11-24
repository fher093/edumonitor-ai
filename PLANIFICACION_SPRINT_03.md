# PLANIFICACIÓN DEL SPRINT 3

**UNIVERSIDAD TÉCNICA DE AMBATO**

**FACULTAD DE INGENIERÍA EN SISTEMAS ELECTRÓNICA E INDUSTRIAL — CARRERA DE SOFTWARE**

**Proyecto:** EduMonitor AI — Sistema inteligente de seguimiento y análisis del rendimiento académico integrado con Moodle.

**Asignatura:** Metodologías Ágiles · **Docente:** Ing. Hernán Naranjo, Mgs.

**Integrantes:** Fernando Llerena, Daky Fueres, Joel Poaquiza, Monserrath Medina.

**Sprint:** 3 (último) · **Fechas:** 13 al 26 de noviembre de 2026 · **Duración:** 10 días hábiles · **Sprint Review final:** 26 de noviembre de 2026.

---

## 1. Objetivo del sprint

Completar el MVP con el **agente académico de IA**: un asistente que responde preguntas del docente usando **solo** datos obtenidos mediante herramientas controladas, separa hechos, inferencias y recomendaciones, y justifica cada recomendación con indicadores. Además, generar el **informe del período** descargable en PDF, registrar la **auditoría** de consultas, cambios y uso del agente, y cerrar el proyecto con pruebas, evaluación del agente y documentación final.

**Resultado esperado (incremento):** un docente autenticado abre **Asistente IA**, pregunta “¿Qué estudiantes necesitan apoyo?” y recibe una respuesta con hechos (cifras de las herramientas), inferencias y recomendaciones justificadas, con las fuentes y la fecha de corte al pie. Desde **Informe** descarga el PDF del curso. El administrador técnico revisa en **Auditoría** quién consultó qué, qué herramientas usó el agente y qué cambios se hicieron, sin ver contraseñas, tokens ni notas.

---

## 2. Alcance comprometido

### 2.1 Historias de usuario

| ID | Historia | Requisito | Prioridad | PH |
| --- | --- | --- | --- | --- |
| HU-12 | Como docente, quiero que el agente consulte mis cursos y calificaciones mediante herramientas controladas, para obtener respuestas basadas en datos. | RF-09 | A | 5 |
| HU-13 | Como docente, quiero que el agente use al menos tres herramientas (progreso, alertas y resumen), para recibir análisis completos. | RF-09 | A | 8 |
| HU-14 | Como docente, quiero un chat que distinga hechos, inferencias y recomendaciones, para conversar con el agente con transparencia. | RF-09, RNF-08 | A | 3 |
| HU-15 | Como docente, quiero recomendaciones concretas justificadas con indicadores, para apoyar a mis estudiantes. | RF-10, RNF-08 | A | 5 |
| HU-16 | Como docente, quiero un informe consultable o descargable con período, métricas, hallazgos y fecha, para documentar el seguimiento. | RF-11 | M | 5 |
| HU-17 | Como administrador técnico, quiero auditoría de cambios académicos, consultas y herramientas del agente, para tener trazabilidad sin exponer secretos. | RF-12 | M | 5 |

**Total: 31 PH.**

### 2.2 Tareas técnicas

| ID | Tarea | Requisito | Horas | Entregable |
| --- | --- | --- | --- | --- |
| TT-09 | Servicio del agente con Ollama y trazas de ejecución | RF-09, RNF-02 | 12 | `AIService/` (orquestador, herramientas, cliente de Ollama, traza) |
| TT-10 | Pruebas de permisos, de integración con la API y de rendimiento | RNF-01, RNF-06, RNF-07 | 8 | `backend.Tests/Sprint3EndpointsTests.cs`, `RendimientoTests.cs`, `AIService.Tests/` |
| TT-11 | Evaluación del agente en tres escenarios y ante fallos de consulta | RF-09 | 4 | `docs/EVALUACION_AGENTE.md` |
| TT-12 | Documentación final y matriz de cumplimiento RF/RNF | RNF-05 | 8 | `docs/MATRIZ_CUMPLIMIENTO.md`, README, `docs/openapi.yaml`, `docs/sql/07-auditoria.sql` |
| TT-13 | Preparación de la demostración en vivo y video corto | Entrega | 4 | Guion de la sección 9 y video de respaldo |
| TT-14 | Estabilización y corrección de errores | RNF-03, RNF-06 | 8 | Correcciones y `dotnet test` / `npm run build` en verde |

**Total: 44 h.**

### 2.3 Refinamiento del 22 de noviembre de 2026: tecnología del agente

La planificación inicial proponía el agente en Python (FastAPI + LangGraph). El equipo decide construirlo en **C# / ASP.NET Core** dentro del microservicio `AIService`, que ya existía desde el Sprint 1. Motivos:

1. **Una sola pila tecnológica:** se reutilizan la validación del JWT, xUnit, el pipeline de CI y los Dockerfiles existentes.
2. **Menor riesgo en el último sprint:** no hace falta instalar ni mantener un segundo entorno (Python, pytest y dependencias de LangGraph).
3. **Mismo comportamiento:** el ciclo del agente (el modelo decide la herramienta → se ejecuta → se devuelve el resultado al modelo → respuesta final) se implementa directamente sobre la API de *function calling* de Ollama, sin necesidad de un framework de grafos.

El modelo elegido es **`qwen2.5:7b`** (instruct general), comparado con `qwen2.5-coder:7b` en la evaluación TT-11 (español y uso de herramientas). Corre localmente en una GPU de 6 GB, de modo que **ningún dato sale del equipo** (RN-11). Se mantienen los puntos y las horas. Pendiente de validación del Product Owner.

### 2.4 Ajustes tras la evaluación con el modelo real (22-nov-2026)

La primera evaluación con `qwen2.5-coder:7b` (ver `docs/EVALUACION_AGENTE.md`) encontró defectos que las pruebas con modelo simulado no podían detectar. Se corrigieron dentro del sprint, cada uno con su prueba automática:

| Hallazgo | Ajuste |
| --- | --- |
| El modelo escribe la llamada a la herramienta como texto JSON | El cliente de Ollama reconoce esas llamadas (solo herramientas conocidas). |
| Una pregunta sobre un curso ajeno se respondía con el curso activo | La consulta automática usa el curso que el docente nombra; el backend responde 403/404. |
| El modelo usaba una sola herramienta | El orquestador completa resumen, alertas y progreso (HU-13); la traza indica el origen de cada consulta. |
| Una recomendación sin respaldo (contactar a los padres) | Instrucciones más estrictas: solo acciones del docente, justificadas con datos. |
| Modelo local lento para la demostración (15–35 s) | Selector **Local / Gemini** en el chat (24-nov): Gemini opcional con nombres seudonimizados (RN-11) y respaldo automático a Ollama; 2–8 s por respuesta. Ollama sigue siendo el modelo por defecto. |
| Respuestas de hasta 150 s | Tope de tokens y respuestas más breves (40–65 s); luego ruta rápida con una sola llamada al modelo, recomendaciones elegidas por número y modelo siempre cargado: **13–19 s** (23-nov). Se descartó una API de IA de pago para mantener RN-11. |

---

## 3. Reglas de negocio que gobiernan el sprint

| Regla | Cómo se cumple en el Sprint 3 |
| --- | --- |
| **RN-03** Cálculos deterministas, nunca con el modelo | El agente no calcula: las cifras salen de `AnaliticaService` a través de las herramientas. Las recomendaciones base también son deterministas (`CalcularRecomendaciones`). |
| **RN-07** Toda alerta muestra evidencia | Las herramientas devuelven la evidencia y la regla; el chat y el informe la muestran. |
| **RN-08** Datos insuficientes, nunca métricas engañosas | Las herramientas devuelven “Datos insuficientes: …” y el agente lo repite sin inventar. |
| **RN-09** El agente solo usa datos de sus herramientas | Respuesta en JSON estructurado (hechos / inferencias / recomendaciones). Un **verificador** descarta los hechos con cifras que no aparecen en los datos de las herramientas. Si el modelo no consulta herramientas, el orquestador consulta el resumen y las alertas antes de responder. |
| **RN-10** Sin mensajes automáticos a estudiantes | El agente solo sugiere; la decisión es del docente. |
| **RN-11** Datos identificables no salen del equipo | Ollama local. Las herramientas envían al modelo solo nombre e id del estudiante (sin cédula ni correo: minimización de datos). |
| **RN-12** Mínimo privilegio | Las herramientas llaman a la API del backend **con el JWT del docente**, así que heredan exactamente los permisos de HU-06: un curso ajeno devuelve `403` y el agente responde “No tienes acceso a ese curso”. |

---

## 4. Arquitectura del incremento

```
 Frontend /agente ── POST /api/ia/chat (JWT) ──▶ AIService (C#)
                                                  │  1. Ollama /api/chat con herramientas (qwen2.5:7b, local)
                                                  │  2. Ejecuta las herramientas pedidas por el modelo
                                                  ▼
                       Backend (/api/cursos, /api/indicadores, /api/alertas, /api/analisis)
                       con el JWT del docente + cabecera X-Herramienta-Agente
                                                  │
                                                  ▼
                                    Auditoría (consulta + herramienta usada)

 Frontend /informe ── GET /api/informes/curso/{id} ──▶ Backend (compone dashboard, análisis, alertas y recomendaciones)
 Frontend /admin/auditoria ── GET /api/auditoria ──▶ Backend (solo administrador técnico)
```

### 4.1 Servicio del agente (`AIService/`)

| Archivo | Responsabilidad |
| --- | --- |
| `Agente/AgenteAcademico.cs` | Orquestador: ciclo de herramientas (máx. 4 rondas), respuesta estructurada, verificación de hechos y traza. |
| `Agente/Herramientas.cs` | Definición de las 5 herramientas (contratos de `docs/agent-tools-contracts.json`) y su ejecución contra el backend. |
| `Agente/VerificadorHechos.cs` | Descarta hechos con cifras que no aparecen en los resultados de las herramientas (RN-09). |
| `Agente/Prompts.cs` | Instrucciones del sistema: reglas RN-09 y RN-10, formato y tono. |
| `Clientes/OllamaClient.cs` | Cliente HTTP de Ollama (`/api/chat` con `tools` y `format`). |
| `Clientes/BackendClient.cs` | Cliente HTTP del backend que reenvía el JWT del docente. |
| `Program.cs` | `POST /api/ia/chat`, `GET /api/ia/estado` (estado de Ollama y modelo). |

### 4.2 Backend

| Archivo | Responsabilidad |
| --- | --- |
| `Services/AnaliticaService.cs` | `CalcularRecomendaciones`: recomendaciones deterministas con acción, justificación e indicador (HU-15). |
| `Controllers/AnalisisController.cs` | `GET /api/analisis/curso/{id}/recomendaciones`. |
| `Controllers/InformesController.cs` | `GET /api/informes/curso/{id}`: informe del período con fecha de corte y fuente (HU-16). |
| `Domain/Entidades.cs` · `Data/AppDbContext.cs` | Entidad `RegistroAuditoria`. |
| `Services/AuditoriaService.cs` | Registro de eventos sin secretos (HU-17). |
| `Controllers/AuditarAttribute.cs` | Filtro que registra las consultas académicas y marca las que vienen del agente. |
| `Controllers/AuditoriaController.cs` | `GET /api/auditoria` paginado y filtrable (solo administrador). |

### 4.3 Frontend

| Archivo | Responsabilidad |
| --- | --- |
| `pages/Agente.jsx` | Chat con las tres secciones, fuentes, fecha de corte, sugerencias y traza de herramientas (HU-14). |
| `pages/Informe.jsx` | Informe imprimible con botón **Descargar PDF** (impresión del navegador) (HU-16). |
| `pages/AdminAuditoria.jsx` | Tabla de auditoría con filtros y paginación (HU-17). |

---

## 5. Contratos de API del sprint

| Método | Ruta | Servicio | Rol | Descripción |
| --- | --- | --- | --- | --- |
| POST | `/api/ia/chat` | AIService | Docente | Pregunta al agente. Devuelve hechos, inferencias, recomendaciones, fuentes, fecha de corte, advertencias y traza. |
| GET | `/api/ia/estado` | AIService | Autenticado | Estado del servicio, de Ollama y modelo configurado. |
| GET | `/api/analisis/curso/{id}/recomendaciones` | Backend | Docente propietario | Recomendaciones deterministas justificadas. |
| GET | `/api/informes/curso/{id}` | Backend | Docente propietario | Informe del período (encabezado, métricas, hallazgos, alertas y recomendaciones). |
| GET | `/api/auditoria?accion=&usuario=&desde=&hasta=&pagina=&tamano=` | Backend | Admin | Registros de auditoría paginados. |

### 5.1 Herramientas del agente

| Herramienta | Endpoint del backend que usa |
| --- | --- |
| `listar_cursos_autorizados` | `GET /api/cursos?periodo=` |
| `obtener_calificaciones` | `GET /api/indicadores/curso/{id}` o la ficha del estudiante |
| `obtener_progreso` | `GET /api/indicadores/curso/{id}` (progreso) o la ficha del estudiante |
| `identificar_alertas` | `GET /api/alertas/curso/{id}` |
| `generar_resumen` | `GET /api/indicadores/curso/{id}` + `GET /api/analisis/curso/{id}` + recomendaciones |

---

## 6. Estrategia de pruebas (TT-10 y TT-11)

**Backend (`Sprint3EndpointsTests`, `AnaliticaServiceTests`, `RendimientoTests`):**

- Recomendaciones deterministas: estudiante con atraso, actividad bajo el 60 %, descenso; ninguna recomendación sin datos.
- Informe: `200` para el propietario, `403` para otro docente y para el administrador, `401` sin token; incluye fecha de corte y fuente.
- Auditoría: solo el administrador (`403` al docente); registra consultas académicas, el uso de herramientas del agente y los cambios de reglas; nunca guarda contraseñas ni tokens; paginación.
- Rendimiento: el dashboard, las alertas y el informe responden en un tiempo medio acotado con la semilla de prueba.

**Agente (`AIService.Tests`)** con un modelo simulado y un backend simulado:

- Usa herramientas y devuelve las tres secciones con fuentes.
- Curso ajeno → “No tienes acceso a ese curso”.
- Herramienta caída → aviso, sin datos inventados.
- Hecho con una cifra inventada → descartado por el verificador.
- El modelo no llama herramientas → el orquestador consulta resumen y alertas.
- Ollama caído → `503` con mensaje claro.

**Evaluación con el modelo real (TT-11):** tres escenarios (resumen del curso, estudiantes que necesitan apoyo, actividades a reforzar) y dos fallos (curso ajeno, backend caído), comparando `qwen2.5:7b` y `qwen2.5-coder:7b`. Resultados en `docs/EVALUACION_AGENTE.md`.

---

## 7. Plan de trabajo

| Día | Actividad | Responsable |
| --- | --- | --- |
| 13 nov | Sprint Planning y refinamiento (tecnología del agente). | Todo el equipo |
| 13–17 nov | Verificación de Ollama, selección del modelo y prompts. | Fernando Llerena |
| 18–20 nov | Recomendaciones deterministas (TDD) e informe en el backend. | Joel Poaquiza |
| 18–20 nov | Auditoría: entidad, filtro, endpoint y script `07-auditoria.sql`. | Monserrath Medina / Joel Poaquiza |
| 20–23 nov | Orquestador del agente, herramientas, verificador y traza. | Fernando Llerena |
| 21–24 nov | Pantallas de chat, informe y auditoría. | Daky Fueres |
| 24 nov | Pruebas de permisos, integración y rendimiento; evaluación del agente. | Todo el equipo |
| 25 nov | Estabilización, matriz RF/RNF, README, OpenAPI y video de respaldo. | Todo el equipo |
| 26 nov | Sprint Review final, demostración y retrospectiva. | Todo el equipo |

### Dependencias

- Las recomendaciones deterministas habilitan HU-15 en el agente y en el informe.
- Las herramientas dependen de los endpoints del Sprint 2 (ya disponibles).
- La auditoría de herramientas depende de la cabecera `X-Herramienta-Agente` que envía el agente.
- La evaluación TT-11 depende del agente completo y de Ollama con el modelo descargado.

---

## 8. Criterios de aceptación

| Historia | Criterio de aceptación |
| --- | --- |
| HU-12 | El agente obtiene los cursos y calificaciones del docente solo a través de herramientas que llaman a la API con su JWT. Un curso ajeno devuelve “No tienes acceso a ese curso”. |
| HU-13 | Con una pregunta general, el agente usa al menos tres herramientas (progreso, alertas y resumen) y la traza lo muestra. |
| HU-14 | Cada respuesta del chat separa Hechos, Inferencias y Recomendaciones (se muestran las secciones con contenido), con las fuentes y la fecha de corte al pie. *Refinado el 24-nov por experiencia de usuario: las secciones vacías y los datos técnicos (modelo, tiempos, traza) ya no se muestran; ver `DESIGN.md`.* |
| HU-15 | Cada recomendación indica la acción y el indicador que la justifica (por ejemplo, “logro 52 % < 60 %”). No hay recomendaciones genéricas sin datos. |
| HU-16 | El informe muestra curso, período, docente, fecha de corte, fuente, métricas, hallazgos, alertas con evidencia y recomendaciones, y se descarga en PDF. |
| HU-17 | El administrador consulta registros con fecha, usuario, acción, recurso y resultado, filtrables y paginados, incluido el uso de herramientas del agente. Nunca aparecen contraseñas, tokens ni notas. |
| TT-10 | Las pruebas de permisos, integración y rendimiento pasan en CI. |
| TT-11 | La evaluación documenta los tres escenarios y los fallos con el modelo real. |

---

## 9. Demostración del incremento

0. Antes de empezar: hacer una pregunta al asistente para **precalentar el modelo** (la primera respuesta tarda más porque Ollama carga el modelo en memoria).
1. Iniciar sesión como docente y abrir **Asistente IA**. Pulsar la sugerencia **Resumen del curso**: mostrar hechos, inferencias y recomendaciones, y desplegar la traza para ver las herramientas usadas.
2. Preguntar “¿Qué estudiantes necesitan apoyo esta semana?”. Comparar las cifras con la pantalla **Alertas**: coinciden porque salen de la misma analítica.
3. Preguntar por un curso ajeno (por ejemplo, “Muéstrame el curso 999”): el agente responde que no hay acceso.
4. Abrir **Informe**, revisar la fecha de corte y la fuente, y pulsar **Descargar PDF**.
5. Iniciar sesión como administrador y abrir **Auditoría**: filtrar por “agente” y ver las herramientas usadas en el paso 1 con su usuario y fecha. Mostrar que no hay notas ni secretos.
6. Detener Ollama y preguntar de nuevo: el chat avisa que el asistente no está disponible, sin inventar datos.

---

## 10. Riesgos y mitigaciones

| Riesgo | Mitigación |
| --- | --- |
| El modelo inventa cifras (alucinación). | Datos solo desde herramientas, verificador de cifras y recomendaciones base deterministas. |
| El modelo de 7B no elige bien las herramientas. | Descripciones claras, temperatura baja y consulta mínima garantizada por el orquestador. |
| Respuestas lentas en la GPU de 6 GB. | Modelo de 7B cuantizado, máximo 4 rondas, tiempo límite e indicador de carga en el chat. |
| Ollama no disponible durante la demo. | Mensaje claro sin datos inventados y video de respaldo (TT-13). |
| Fuga de datos sensibles en la auditoría. | La auditoría guarda solo acción, recurso e ids; pruebas que verifican que no se registran contraseñas ni tokens. |
| Poco tiempo en el último sprint. | Prioridad A primero (HU-12 a HU-15); el informe reutiliza endpoints existentes. |

---

## 11. Definición de Hecho (DoD) del sprint

- [x] Historias HU-12 a HU-17 implementadas en backend, agente y frontend.
- [x] El agente usa al menos tres herramientas y respeta RN-09, RN-11 y RN-12.
- [x] Recomendaciones deterministas cubiertas por pruebas unitarias (`RecomendacionesTests`).
- [x] Pruebas de permisos, integración, rendimiento y del agente en verde: **213 pruebas** (88 backend, 104 agente, 21 MoodleService).
- [x] `dotnet build`, `dotnet test` y `npm run build` en verde.
- [ ] Evaluación del agente documentada (TT-11): completa con `qwen2.5-coder:7b`; falta la corrida con `qwen2.5:7b` (descarga pendiente).
- [x] `docs/openapi.yaml`, `docs/sql/07-auditoria.sql`, README y matriz RF/RNF actualizados.
- [ ] Demostración y video de respaldo listos para el 26 de noviembre de 2026.

---

## 12. Retrospectiva prevista

**¿Qué funcionó?**

- Haber separado la analítica en un servicio puro en el Sprint 2: el agente la reutiliza sin duplicar cálculos.
- Que las herramientas pasen por la API con el JWT del docente: los permisos del agente salen “gratis” y no se pueden saltar.

**¿Qué mejorar?**

- Empezar antes la integración con el modelo de lenguaje, que es la parte con más incertidumbre.
- Automatizar la evaluación del agente para repetirla en cada cambio del prompt.

**Trabajo futuro (fuera del MVP):**

- Historial de conversaciones por docente.
- Comparación entre períodos en el informe.
- Pruebas de componentes React con Vitest.
