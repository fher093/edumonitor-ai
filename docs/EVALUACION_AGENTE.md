# Evaluación del agente académico (TT-11)

**Sprint:** 3 · **Historias:** HU-12 a HU-15 · **Requisitos:** RF-09, RF-10, RNF-08 · **Reglas:** RN-08, RN-09, RN-11, RN-12

Este documento registra cómo se comporta el agente con un **modelo de lenguaje real** (Ollama local) en tres escenarios académicos y ante fallos de consulta, qué problemas aparecieron y cómo se corrigieron.

---

## 1. Método

| Elemento | Detalle |
| --- | --- |
| Entorno | Backend con datos de demostración (curso SW-401, 5 estudiantes, 8 actividades) + AIService + Ollama 0.32 en el equipo del equipo de desarrollo. |
| Hardware | GPU NVIDIA RTX 4050 Laptop (6 GB). El modelo de 7B ocupa ~5 GB: ~75 % corre en GPU y ~25 % en CPU. |
| Script | `docs/evaluacion/evaluar-agente.mjs` (repetible): inicia sesión como docente, hace las preguntas y guarda la respuesta completa con su traza. |
| Resultados | `docs/evaluacion/resultado-<modelo>.json` |
| Criterio de éxito | Estado esperado (`ok` o `sin_acceso`), al menos 3 herramientas en preguntas generales (HU-13), hechos con cifras de las herramientas (RN-09), recomendaciones justificadas (HU-15), sin datos inventados (RN-08). |

Ejecución:

```powershell
node docs/evaluacion/evaluar-agente.mjs docente@test.local "<clave>" 1 docs/evaluacion/resultado-qwen2.5-coder-7b.json
```

### Escenarios

| Id | Tipo | Pregunta | Resultado esperado |
| --- | --- | --- | --- |
| E1 | Escenario | “Dame un resumen del curso” | `ok`, 3 herramientas, hechos y recomendaciones |
| E2 | Escenario | “¿Qué estudiantes necesitan apoyo esta semana?” | `ok`, estudiantes con alertas y su evidencia |
| E3 | Escenario | “¿Qué actividades debo reforzar con el grupo?” | `ok`, actividades bajo el 60 % |
| F1 | Fallo | “Muéstrame las alertas del curso con id 9999” | `sin_acceso`, sin datos de otro curso |
| F2 | Fallo | Backend o herramienta caída | `error_herramientas`, sin respuesta de memoria |
| F3 | Fallo | Ollama apagado | HTTP 503 con mensaje claro |

F2 y F3 se verifican con pruebas automáticas (`AIService.Tests/AgenteTests.cs` y `ChatEndpointTests.cs`), porque provocar el fallo a mano durante la evaluación no aporta más información.

---

## 2. Resultados con `qwen2.5-coder:7b` (22-nov-2026)

### 2.1 Primera ejecución: problemas encontrados

| Id | Estado | Herramientas | Tiempo | Observación |
| --- | --- | --- | --- | --- |
| E1–E3 | `ok` | resumen + alertas (automáticas) | ~55 s | **El modelo nunca pidió herramientas.** Respondió gracias a la consulta mínima del orquestador. |
| F1 | `ok` ❌ | resumen + alertas del curso **activo** | ~54 s | **Error:** respondió con datos del curso 1 en vez de decir “sin acceso”. |

Causas:

1. **`qwen2.5-coder` escribe la llamada a la herramienta como texto JSON** (`{"name": "identificar_alertas", "arguments": {...}}`) en lugar de usar el campo `tool_calls` de Ollama. El agente no la reconocía.
2. La consulta mínima automática usaba siempre el curso activo, aunque el docente hubiera nombrado otro curso.

### 2.2 Correcciones aplicadas

| Problema | Corrección | Prueba que la cubre |
| --- | --- | --- |
| Llamadas escritas como texto | `OllamaClient.ExtraerLlamadasDeTexto`: reconoce objetos, listas, bloques ```` ```json ```` y `<tool_call>`; solo acepta las 5 herramientas conocidas. | `CompatibilidadModeloTests` (8 casos) |
| F1 respondía con otro curso | `AgenteAcademico.CursoMencionado`: si el docente nombra un curso (“curso 9999”), la consulta automática usa ese curso y el backend responde 403/404. | `Consulta_automatica_sobre_otro_curso_no_usa_el_curso_activo` |
| Solo 1 herramienta por pregunta (HU-13) | Si el modelo analiza el curso con una herramienta base, el orquestador completa resumen, alertas y progreso. La traza marca cada paso como `modelo` u `orquestador`. | `Si_el_modelo_usa_una_sola_herramienta_base_se_completa_el_analisis_del_curso` |
| Recomendación inventada (“enviar un correo a los padres”) | Instrucción explícita: solo acciones del docente dentro del curso, sin terceros; cada justificación cita una cifra o regla de los datos. | Revisión manual (E2) |
| Respuestas de 50–150 s | Tope de tokens al elegir herramientas (300) y en la respuesta (900); respuestas más breves (5 hechos, 2 inferencias, 3 recomendaciones). | Medición (tabla 2.3) |
| Entrada no UTF-8 devolvía 500 | `ThrowOnBadRequest = false`: un JSON inválido es 400. | `Cuerpo_que_no_es_utf8_devuelve_400_y_no_500` |

### 2.3 Ejecución final

| Id | Estado | Herramientas (origen) | Tiempo | Hechos / Recs. | ¿Cumple? |
| --- | --- | --- | --- | --- | --- |
| E1 | `ok` | generar_resumen (modelo), identificar_alertas (orq.), obtener_progreso (orq.) | 63 s | 5 / 3 | ✅ |
| E2 | `ok` | identificar_alertas (modelo), generar_resumen (orq.), obtener_progreso (orq.) | 54 s | 5 / 3 | ⚠️ ver 2.4 |
| E3 | `ok` | las 3 por consulta automática | 43 s | 5 / 3 | ✅ |
| F1 | `sin_acceso` | identificar_alertas(curso 9999) → 404 | 8 s | 0 / 0 | ✅ |

Ejemplo (E3, “¿Qué actividades debo reforzar con el grupo?”):

- **Hechos:** “5 de 7 actividades evaluadas presentan un promedio inferior al 60 %.” · “La actividad con menor rendimiento es «Taller 1: Tablero Scrum» (50,0 % de logro).” · “El componente con menor promedio es Práctico-experimental del parcial 1 (5,00/10).”
- **Inferencias:** “Es importante revisar el componente práctico-experimental del parcial 1.”
- **Recomendaciones:** “Retroalimentar al grupo los contenidos de «Taller 1: Tablero Scrum»…” — *logro 50.0 % < 60 %* · “Revisar con el grupo la carga y la metodología de las últimas actividades.” — *tendencia -8.0 pp*

Todas las cifras coinciden con las pantallas **Análisis grupal** y **Alertas**, porque salen de la misma `AnaliticaService`.

### 2.4 Limitación conocida: errores de relación entre cifras correctas

En E2, el modelo escribió *“4 estudiantes no entregaron las actividades vencidas (Lección 3, Taller 2 y Tarea 2)”*. El 4 (estudiantes con alertas) y las actividades existen en los datos, pero **solo un estudiante** dejó esas entregas pendientes. El verificador comprueba que cada **cifra** exista en los datos, no que la **relación** entre cifras sea correcta, así que este hecho pasó.

Mitigaciones vigentes:

- Cada hecho muestra su **fuente** y la **traza** permite revisar los datos exactos.
- Las recomendaciones parten de las **recomendaciones deterministas** del backend (HU-15), que sí son exactas.
- El chat recuerda que son señales de seguimiento y que las pantallas de Alertas y Análisis son la referencia.

Mejora propuesta (fuera del MVP): que el modelo cite el identificador de la alerta o actividad en cada hecho para verificar también la relación.

### 2.5 Rendimiento: optimización del 23-nov-2026

Con 40–70 s por respuesta, el asistente no era usable en una demostración. Se midió cada llamada a Ollama (tokens y tiempos, registrados por `OllamaClient`):

| Fase (antes) | Tokens de entrada | Tokens generados | Tiempo |
| --- | --- | --- | --- |
| Elegir herramientas (1.ª llamada) | ~850 | 20–80 | 2–5 s |
| Elegir herramientas (2.ª llamada, texto que se descarta) | 1 600–2 000 | 140–300 | 9–19 s |
| Respuesta final | ~2 700 | 470–620 | 32–41 s |
| Carga del modelo (tras 5 min sin uso) | — | — | 11–17 s |

**Hallazgo:** el modelo **lee** ~1 000 tokens/s pero **genera** solo ~15 tokens/s (parte del modelo corre en CPU con 6 GB de VRAM). El tiempo depende casi solo de cuántos tokens escribe.

Cambios aplicados:

1. **Ruta rápida** (`Agente:RutaRapida`): en preguntas sobre un curso, el orquestador ejecuta directamente resumen, alertas y progreso (y calificaciones si la pregunta trata de estudiantes o notas) y el modelo se usa **una sola vez**, para redactar. Las preguntas sobre otros cursos o períodos siguen el ciclo en el que el modelo elige las herramientas. La traza lo indica (`origen: orquestador`).
2. **Recomendaciones por número:** el modelo ya no redacta las recomendaciones; elige hasta 3 de las recomendaciones deterministas del backend por su `id`. Escribe la mitad de tokens y las recomendaciones quedan **exactas** (sin riesgo de inventar).
3. **Respuestas más breves** (4 hechos, 2 inferencias) y **datos compactos** (distribución sin rangos vacíos, código de la regla en vez del nombre completo).
4. **Modelo siempre cargado** (`keep_alive: 60m`) y contexto de 6 144 tokens.

| Id | Antes | Después (modelo cargado) |
| --- | --- | --- |
| E1 Resumen del curso | 70 s | **19 s** |
| E2 ¿Quién necesita apoyo? | 57 s | **13 s** |
| E3 ¿Qué reforzar? | 42 s | **16 s** |
| F1 Curso ajeno | 7 s | **0,03 s** (sin llamar al modelo) |

Resultados completos: `docs/evaluacion/resultado-qwen2.5-coder-7b-optimizado.json`. Los tres escenarios siguen cumpliendo y las recomendaciones de E3 pasaron a ser más pertinentes (actividad y componente en lugar de estudiantes). Pruebas: `AIService.Tests/RutaRapidaTests.cs`.

La primera pregunta tras arrancar el servicio sigue pagando la carga del modelo (~17 s): para la demostración, hacer una pregunta antes de empezar. Se descartó usar una API de IA de pago para mantener RN-11 (los datos no salen del equipo).

### 2.6 Respuestas que se adaptan a la pregunta (23-nov-2026)

Al probar en la web se vio que, tras la optimización, el agente daba casi la misma respuesta a preguntas distintas ("¿qué estudiantes necesitan apoyo?", "¿sobre qué sería la tutoría de Ana Torres?", "¿qué temas debe tener?"). Causas encontradas y corrección:

| Problema observado | Causa | Corrección |
| --- | --- | --- |
| Las preguntas de seguimiento no se entendían | La llamada final no recibía la conversación | Se envían las **preguntas anteriores del docente** (no las respuestas del asistente, que arrastraban errores de una respuesta a otra). |
| Sobre Ana Torres se hablaba con datos del grupo | Nunca se consultaba su ficha | El orquestador **reconoce al estudiante nombrado** (también en seguimientos, sin tildes o solo por nombre o apellido si es único) y consulta su **ficha individual**. |
| Le atribuía a Ana la evidencia de María Gómez | El modelo recibía las alertas de todos | En preguntas sobre un estudiante, el modelo recibe **solo sus datos** (sus alertas, entregas y recomendaciones). |
| "Temas de la tutoría" vagos | Los datos estaban pero el modelo no los usaba | Puntos débiles **calculados en código** (3 notas más bajas, entregas pendientes, promedio por componente) y una **recomendación de tutoría determinista**. |
| Siempre las mismas recomendaciones | Solo podía elegir de la lista del curso | Puede proponer hasta 2 **recomendaciones propias**, verificadas contra los datos. |
| Sin respuesta directa | El formato solo tenía hechos, inferencias y recomendaciones | Campo **respuesta** (1–3 frases), verificado como los hechos. |
| Hechos sin sujeto ("Sus notas bajaron…") | El modelo recortaba el nombre al copiar la evidencia | La evidencia incluye el nombre y, si el modelo lo quita, se repone en código. |

Ejemplo real (SW-402), "¿qué temas debe tener la tutoría para Ana Torres?": *"La tutoría para Ana Torres debe centrarse en Taller 2: Proyecto aplicado, Taller 1: Práctica guiada y Lección 3: Evaluación de unidad."* con la recomendación *"Preparar una tutoría con Ana Torres centrada en … y acordar la entrega de Tarea 3"* — justificada con sus notas (2/10, 3/10, 3/10). Coincide con su ficha. El sistema no tiene los temas del sílabo: los "temas" son las actividades y componentes con notas bajas.

Costo: la respuesta directa y el contexto adicional suben el tiempo a **15–35 s** (antes 13–19 s). Pruebas: `AIService.Tests/PreguntasEspecificasTests.cs`.

### 2.7 Material de apoyo generado (23-nov-2026)

Cuando el docente pide **crear** algo ("créame un deber de 5 preguntas de la Lección 2 para María Gómez", "genera 3 ejercicios del Taller 2"), el agente entra en **modo material**: el modelo genera el contenido a partir del nombre de la actividad y del curso. Es la única parte de la respuesta que el modelo inventa, así que:

- Se muestra **aparte**, con la etiqueta *Material sugerido por IA* y la nota *"revísalo y ajústalo antes de entregarlo"*, más un botón **Copiar**.
- **No pasa por el verificador de cifras** (es contenido nuevo, no datos), pero los **hechos que lo justifican sí** (p. ej., *"Nota en Lección 2: 3/10"*).
- El código respeta la **cantidad pedida** ("3 ejercicios" → 3) y quita la numeración que agrega el modelo.
- Como el sistema no tiene el sílabo, si la actividad tiene un nombre genérico ("Evaluación de unidad"), las preguntas tratan de contenidos típicos de la asignatura (HTML, CSS, HTTP, APIs en Ingeniería Web).

Ejemplo real (SW-402, 20 s): *"¿Qué es el método HTTP y cómo se utiliza en las peticiones web?"*, *"Explique los conceptos básicos de HTML5…"*, *"Realice un ejemplo sencillo de cómo crear un formulario HTML con validaciones básicas."* Pruebas: `PreguntasEspecificasTests` (modo material, cantidad y numeración).

### 2.8 Gemini como opción para la demostración (24-nov-2026)

Para que la demostración sea fluida, el chat tiene un selector **Local / Gemini**. Ollama sigue siendo el modelo por defecto y no cambió; Gemini (API de Google AI Studio, capa gratuita) es opcional:

- **Privacidad (RN-11):** antes de enviar, `Seudonimizador` reemplaza los nombres de los estudiantes por "Estudiante A, B…" (aunque se escriban sin tildes, o solo el nombre o el apellido si es único) y los repone al recibir la respuesta. Lo comprueba una prueba de punta a punta que inspecciona lo enviado a Google.
- **Respaldo:** ante un 503/429, reintenta y prueba `gemini-3.5-flash`; si Gemini sigue sin responder, la consulta continúa con Ollama y el chat lo avisa.
- **Mismo agente:** herramientas, permisos, verificador de cifras, modo material y auditoría no cambian.
- **Configuración:** `GEMINI_API_KEY` en `.env` (nunca en el repositorio); sin clave, la opción aparece deshabilitada.

| Pregunta (SW-402) | Local (qwen2.5-coder:7b) | Gemini (flash-lite) |
| --- | --- | --- |
| ¿Qué estudiantes necesitan apoyo? | 25 s | 3,3 s |
| Temas de la tutoría para Ana Torres | 15 s | 1,8 s |
| ¿Y por cuál empiezo? | 15 s | 7,7 s* |
| Deber de 5 preguntas para María Gómez | 20 s | 24,6 s* |

* El modelo rápido respondió 503 (saturado) y contestó `gemini-3.5-flash` tras el reintento. Con el modelo rápido disponible, las respuestas tardan 2–8 s. Además, Gemini razona mejor ("empieza por el Taller 2, su nota más baja: 2/10") y genera material más específico. Pruebas: `AIService.Tests/GeminiTests.cs` (19 pruebas).

---

## 3. Comparación con `qwen2.5:7b` (modelo elegido)

**Pendiente de ejecutar.** La descarga de `qwen2.5:7b` se interrumpió el 22-nov-2026 por falta de memoria del equipo. Para completarla:

```powershell
ollama pull qwen2.5:7b
# AIService con OLLAMA_MODELO=qwen2.5:7b (valor por defecto en docker-compose y appsettings.json)
node docs/evaluacion/evaluar-agente.mjs docente@edumonitor.local "<clave>" <cursoId> docs/evaluacion/resultado-qwen2.5-7b.json
```

| Criterio | qwen2.5-coder:7b | qwen2.5:7b |
| --- | --- | --- |
| Usa `tool_calls` nativo | No (escribe JSON como texto; se corrigió en el cliente) | Pendiente |
| Escenarios E1–E3 cumplidos | 3/3 (E2 con la limitación 2.4) | Pendiente |
| Fallo F1 (curso ajeno) | ✅ | Pendiente |
| Calidad del español | Correcta, algo literal | Pendiente |
| Tiempo medio (E1–E3) | 53 s → **16 s** tras la optimización (2.5) | Pendiente |

---

## 4. Conclusiones

1. El agente **no inventa datos de cursos ajenos ni responde sin datos**: los fallos F1, F2 y F3 terminan en mensajes claros.
2. Las respuestas usan **al menos tres herramientas** y separan hechos, inferencias y recomendaciones, con fuentes y fecha de corte.
3. La evaluación con el modelo real fue decisiva: encontró dos defectos (formato de llamadas y curso ajeno) que las pruebas con modelo simulado no podían detectar. Ambos tienen ahora pruebas automáticas.
4. Queda una limitación documentada (relación entre cifras) y la comparación con `qwen2.5:7b`.
