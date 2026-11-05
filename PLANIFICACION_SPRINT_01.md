**UNIVERSIDAD TÉCNICA DE AMBATO**

**FACULTAD DE INGENIERÍA EN SISTEMAS ELECTRÓNICA E INDUSTRIAL CARRERA DE SOFTWARE**

# PLANIFICACIÓN DEL PROYECTOScrum + XP y Sprint 1

**Tema:**

EduMonitor AI: Sistema inteligente de seguimiento y análisis del rendimiento académico con módulo académico propio (réplica simplificada de Moodle)

**Asignatura:** METODOLOGÍAS ÁGILES

**Nivel y Paralelo:** Cuarto - A **Docente:** Ing. Hernán Naranjo, Mgs.

**Integrantes:**

Llerena Urrutia Fernando David

Fueres Perugachi Daky Fernando

Poaquiza Ayme Gilberth Joel

Medina Chico Darlyn Monserrath

**Ciclo Académico:** JULIO – DICIEMBRE 2026 Ambato – Ecuador

**Índice**

Contenido

[1 Introducción 3](#_Toc243850122)

[1.1 Descripción del proyecto 3](#_Toc243850123)

[1.2 Alcance y Restricciones 3](#_Toc243850124)

[2 Objetivos 4](#_Toc243850125)

[2.1 Objetivo General 4](#_Toc243850126)

[2.2 Objetivos Específicos 4](#_Toc243850127)

[3 Usuarios y flujo de trabajo 4](#_Toc243850128)

[4 Requisitos 5](#_Toc243850129)

[4.1 Requerimientos funcionales 5](#_Toc243850130)

[4.2 Requerimientos no funcionales 5](#_Toc243850131)

[4.3 Criterios de aceptación del MVP 6](#_Toc243850132)

[5 Reglas de negocio y casos especiales (Marco normativo) 6](#_Toc243850133)

[5.1 Marco normativo y ponderaciones institucionales (UTA) 6](#_Toc243850134)

[5.2 Reglas de negocio (RN-01 a RN-12) y fórmulas de indicadores 7](#_Toc243850135)

[5.3 Indicadores (Cálculo reproducible) 7](#_Toc243850136)

[6 Metodología Scrum + XP 8](#_Toc243850137)

[6.1 Roles del Equipo 8](#_Toc243850138)

[6.2 Eventos y artefactos Scrum 8](#_Toc243850139)

[6.3 Prácticas XP Aplicadas 9](#_Toc243850140)

[6.4 Definición de Hecho (DoD) 9](#_Toc243850141)

[6.5 Control de versiones y calidad 10](#_Toc243850142)

[7 Arquitectura del Sistema 10](#_Toc243850143)

[7.1 Descripción general 10](#_Toc243850144)

[7.2 Componentes 11](#_Toc243850145)

[7.3 Herramientas del agente 11](#_Toc243850146)

[7.4 Módulo académico local (réplica simplificada de Moodle) 11](#_Toc243850147)

[7.5 Verificación previa del modelo académico local 12](#_Toc243850148)

[8 Tecnologías y recursos 12](#_Toc243850149)

[8.1 Tecnologías 12](#_Toc243850150)

[8.2 Recursos. 12](#_Toc243850151)

[9 Estructura de datos 13](#_Toc243850152)

[10 Product Backlog (Basado en RF) 13](#_Toc243850153)

[10.1 Historias de usuario 14](#_Toc243850154)

[10.2 Backlog técnico (tareas técnicas) 14](#_Toc243850155)

[10.3 Totales y velocidad 15](#_Toc243850156)

[11 Trazabilidad de requisitos 15](#_Toc243850157)

[12 Hoja de ruta y análisis de capacidad 16](#_Toc243850158)

[13 Capacidad del equipo 16](#_Toc243850159)

[14 Cronograma de Sprints 17](#_Toc243850160)

[15 Planificación del Sprint 1 (19 – 29 de octubre de 2026) 17](#_Toc243850161)

[15.1 Objetivo y entregable del Sprint 17](#_Toc243850162)

[15.2 Sprint 1 Backlog 17](#_Toc243850163)

[15.3 Historias de usuario y tareas del Sprint 1 19](#_Toc243850164)

[Historia de usuario 1 19](#_Toc243850165)

[Historia de usuario 2 21](#_Toc243850166)

[Historia de usuario 3 23](#_Toc243850167)

[Historia de usuario 4 25](#_Toc243850168)

[15.4 Tareas técnicas del Sprint 1 27](#_Toc243850169)

[16 Plan de trabajo, riesgos y cierre del sprint 40](#_Toc243850170)

[16.1 Orden de elaboración y dependencias 40](#_Toc243850171)

[16.2 Riesgos y Mitigación 41](#_Toc243850172)

[16.3 Comunicación y seguimiento 41](#_Toc243850173)

[16.4 Pruebas de aceptación y cierre del Sprint 1 41](#_Toc243850174)

[17 Conclusiones y Recomendaciones 42](#_Toc243850175)

[17.1 Conclusiones 42](#_Toc243850176)

[17.2 Recomendaciones 42](#_Toc243850177)

# Introducción

## Descripción del proyecto

Sistema web con un módulo académico propio (réplica simplificada de Moodle) que automatiza el seguimiento analítico, estadístico y pedagógico del rendimiento de los estudiantes. El docente registra en el sistema sus cursos, estudiantes, actividades y calificaciones, y el sistema, a partir de esa información académica (calificaciones, progreso, estados de actividad), utiliza un agente de inteligencia artificial para interpretar los resultados y proponer intervenciones pedagógicas oportunas.

El sistema calcula indicadores verificables y emite alertas tempranas con evidencias claras. Existen dos flujos principales, ambos resueltos con un agente de IA local (Ollama) y herramientas de consulta controladas:

- Seguimiento Analítico: Cálculo determinista de promedios, distribución de notas y progreso del curso.
- Análisis con Agente IA: El agente interpreta los datos, distingue hechos de inferencias y genera recomendaciones de apoyo justificadas.

El sistema sugiere; la decisión es del docente. Las recomendaciones son orientativas y las alertas no constituyen diagnósticos ni determinaciones definitivas sobre la capacidad del estudiante (Documento de especificación, secc. 17). El sistema no envía mensajes automáticos a los estudiantes

## Alcance y Restricciones

El proyecto consiste en desarrollar un MVP (Producto Mínimo Viable) funcional en un plazo de 3 sprints (del 19 de octubre al 26 de noviembre de 2026). El sistema permitirá gestionar la autenticación de docentes, la gestión académica local (cursos, estudiantes, matrículas, actividades y calificaciones), la visualización de dashboards y la interacción con un agente de IA.

Lo que incluye el proyecto:

- Un docente autenticado con acceso a uno o varios cursos autorizados.
- Gestión local de cursos, estudiantes, matrículas, actividades y calificaciones (réplica simplificada de Moodle).
- Dashboard de indicadores con promedios y distribución de calificaciones.
- Ficha individual de seguimiento de estudiantes (evolución y actividades pendientes).
- Reglas transparentes para generar alertas académicas (descensos, atrasos, baja finalización).
- Agente de IA con herramientas de consulta (Tools) para responder preguntas del docente y generar recomendaciones.
- Generación de un informe consultable o descargable con hallazgos y recomendaciones.

Lo que NO incluye (Fuera del alcance):

- Envío de calificaciones o notificaciones a plataformas externas.
- Mensajería automática o notificaciones hacia los estudiantes.
- Entrenamiento de modelos propios de Inteligencia Artificial.
- Predicción clínica o psicológica de los estudiantes.
- Uso de arquitecturas multiagentes o microservicios obligatorios (se sugiere arquitectura cliente-servidor con servicio independiente de IA).
- Acceso a datos mediante mecanismos no autorizados por la institución.
- Conexión o sincronización con cualquier Moodle (institucional o propio): el MVP trabaja con su propio módulo académico.
- Registro y cálculo automático de la asistencia (se trata como dato manual del docente).

La aplicación se diseñará para incorporar información de diversas materias de la FISEI. La cantidad de documentos y datos procesados por asignatura que se utilizarán para validar el producto será delimitada según la disponibilidad.

# Objetivos

## Objetivo General

Implementar un prototipo funcional de analítica académica con un módulo académico propio (réplica simplificada de Moodle) y un agente de IA que consulte esa información y emita recomendaciones fundamentadas.

## Objetivos Específicos

- Gestionar cursos, estudiantes matriculados, actividades y calificaciones en un módulo académico propio.
- Construir un panel de seguimiento individual y grupal con indicadores calculados de forma reproducible.
- Identificar situaciones que requieran atención docente mediante reglas transparentes y configurables.
- Implementar un agente con herramientas de consulta, análisis y generación de recomendaciones.
- Verificar seguridad, calidad de datos, trazabilidad y funcionamiento mediante pruebas.

# Usuarios y flujo de trabajo

Los usuarios del sistema son dos. El estudiante no usa el sistema: sus datos los registra el docente.

|     |     |
| --- | --- |
| **Rol** | **Qué puede hacer** |
| **Docente** | Inicia sesión, crea sus cursos, registra estudiantes y calificaciones, consulta panel, ficha individual, alertas y análisis grupal, conversa con el agente y descarga informes. Solo ve cursos y estudiantes autorizados. |
| **Administrador técnico** | Gestiona usuarios y consulta la auditoría. |

**Flujo de Trabajo Principal:**

1.  **Inicio de Sesión y Autorización**

- El docente ingresa al sistema con sus credenciales.
- La plataforma valida su rol.
- Se despliega únicamente la información de las asignaturas que tiene a su cargo.

1.  **Gestión Académica**

- El docente crea sus cursos, registra estudiantes y los matricula.
- Se importan matriculados, actividades, ítems de calificación y notas recientes.
- El sistema registra la fecha del proceso.

1.  **Monitoreo en el Dashboard**

- El docente accede al panel de control.
- El sistema procesa datos numéricos de forma determinista.
- Se muestran promedios y distribución de calificaciones.
- Se aplican reglas de alertas académicas (atrasos, baja finalización, caída en rendimiento).

1.  **Asistencia del Agente de IA**

- El docente formula consultas al chat del agente sobre rendimiento grupal o individual.
- El agente consulta datos mediante herramientas controladas.
- Explica resultados y propone acciones de apoyo.
- No toma decisiones académicas ni inventa calificaciones.

1.  **Generación de Informes**

- El docente analiza métricas y revisa propuestas pedagógicas del agente.
- Genera un resumen descargable o consultable.
- El informe consolida hallazgos, métricas del período e intervenciones recomendadas.

# Requisitos

## Requerimientos funcionales

|     |     |     |
| --- | --- | --- |
| **ID** | **Requisito** | **Criterio verificable** |
| **RF-01** | Autenticación y autorización | Acceso con credenciales del sistema; roles docente y administrador técnico; cada docente ve solo cursos y estudiantes autorizados. |
| **RF-02** | Gestión de cursos | Crear, editar, eliminar y listar los cursos del docente (nombre, código, período y escala), con acceso solo a los propios. |
| **RF-03** | Estudiantes, matrículas y calificaciones | Registrar estudiantes, matricularlos en los cursos y registrar actividades, calificaciones y estados (nota cero, sin entrega, pendiente de calificar, completada). |
| **RF-04** | Gestión de cursos | Seleccionar curso, período y listado de estudiantes, con búsqueda y filtros. |
| **RF-05** | Panel de indicadores | Promedio con escala explícita, distribución de calificaciones, actividades evaluadas y progreso disponible. |
| **RF-06** | Seguimiento individual | Evolución de notas; actividades sin entrega, pendientes de calificar y completadas, diferenciando sus estados. |
| **RF-07** | Alertas académicas | Reglas configurables (descenso en últimas evaluaciones, atrasos, baja finalización); mostrar evidencia y evitar etiquetas definitivas. |
| **RF-08** | Análisis grupal | Identificar actividades con resultados bajos y tendencias de rendimiento del curso. |
| **RF-09** | Agente de IA | Responder preguntas del docente con datos obtenidos mediante herramientas controladas; distinguir hechos, inferencias y recomendaciones. |
| **RF-10** | Recomendaciones | Acciones de apoyo concretas, justificadas por indicadores, sin automatizar decisiones académicas. |
| **RF-11** | Informe | Resumen consultable o descargable con período, métricas, hallazgos, recomendaciones y fecha de actualización. |
| **RF-12** | Auditoría | Registrar cambios académicos, consultas y herramientas usadas por el agente sin exponer secretos |

## Requerimientos no funcionales

- RNF-01 Seguridad: HTTPS cuando se despliegue, secretos en variables de entorno, control de acceso por curso y mínimo privilegio.
- RNF-02 Privacidad: datos mínimos, acceso restringido, anonimización para pruebas y prohibición de enviar información identificable a servicios externos sin autorización.
- RNF-03 Confiabilidad: manejo de errores, datos ausentes y registros incompletos sin presentar métricas engañosas.
- RNF-04 Usabilidad: interfaz clara, filtros comprensibles, indicadores con unidades y mensajes de error útiles.
- RNF-05 Mantenibilidad: repositorio Git, estructura modular, documentación de instalación y configuración.
- RNF-06 Calidad: pruebas unitarias de indicadores, integración de API y pruebas de permisos; resultados reproducibles.
- RNF-07 Rendimiento: consultas paginadas o limitadas y respuestas razonables para un curso de prueba; documentar tiempos medidos.
- RNF-08 Transparencia: mostrar fuente, fecha de registro y justificación de alertas; las respuestas de IA deben reconocer incertidumbre.

## Criterios de aceptación del MVP

- El docente inicia sesión y solo visualiza sus cursos autorizados.
- Los cursos, matrículas y calificaciones registrados coinciden con los datos de prueba del módulo académico.
- El dashboard distingue nota cero, sin entrega y pendiente de calificación.
- Una regla de alerta se activa con un caso de prueba y se explica con evidencias.
- El agente responde tres solicitudes distintas invocando herramientas reales y no inventa datos si la consulta falla.
- El docente recibe recomendaciones relacionadas con los hallazgos, no respuestas genéricas.
- Se demuestra que un usuario no autorizado no accede a otro curso.
- El proyecto se instala siguiendo el README y dispone de pruebas ejecutables

# Reglas de negocio y casos especiales (Marco normativo)

El sistema transformará los criterios de evaluación y seguimiento académico institucionales en reglas configurables y deterministas para identificar situaciones que requieran atención docente. Estas reglas toman como fuente de referencia el marco normativo y académico de la Universidad Técnica de Ambato (UTA) y los lineamientos de evaluación de la FISEI.

## Marco normativo y ponderaciones institucionales (UTA)

Para que el cálculo de los indicadores y el análisis del rendimiento académico en EduMonitor AI reflejen la realidad reglamentaria de la Universidad Técnica de Ambato (UTA), el sistema integra los lineamientos establecidos en el _Instructivo para el Sistema de Evaluación Estudiantil de Grado_.

#### A. Ponderaciones por Parcial

En cada parcial, la nota final se calcula ponderando obligatoriamente tres componentes institucionales:

|     |     |     |
| --- | --- | --- |
| **Componente de Aprendizaje** | **Porcentaje** | **Actividades Típicas Evaluadas** |
| **Aprendizaje en contacto con el docente** | 40 % | Lecciones, controles de lectura, participación en clase, evaluaciones cortas (quizzes) y trabajos en aula. |
| **Aprendizaje práctico-experimental** | 30 % | Talleres, resolución de ejercicios o problemas, laboratorios/prácticas, análisis de casos y uso de software o simulaciones. |
| **Aprendizaje autónomo** | 30 % | Tareas, informes, portafolios, ensayos o investigación documental, presentaciones y proyectos. |

#### B. Criterios de Acreditación y Aprobación

- **Nota Parcial:** La calificación de cada parcial se obtiene de la sumatoria ponderada de los tres componentes anteriores (escala sobre 10 puntos por parcial).
- **Nota Final de la Asignatura:** Corresponde a la suma de las calificaciones obtenidas en los dos parciales del periodo académico (nota final sobre 20 puntos).
- **Condición de Aprobación:** Para aprobar la asignatura, el estudiante requiere un puntaje mínimo acumulado de **14 puntos** sobre 20 en dicha sumatoria, además de cumplir con el porcentaje de asistencia reglamentaria (generalmente >= 70 %). En el MVP la asistencia no se registra en el sistema: se trata como un dato manual del docente y queda fuera del cálculo automático de indicadores y alertas.

_Nota de integración en el sistema:_ El backend de EduMonitor AI utilizará estos porcentajes y componentes mapeados desde las actividades y calificaciones registradas en el módulo académico para estructurar el panel de indicadores y detectar de forma determinista qué componente está afectando el rendimiento del estudiante (por ejemplo, si falla en el componente práctico-experimental o en el autónomo).

## Reglas de negocio (RN-01 a RN-12) y fórmulas de indicadores

Los valores numéricos marcados como inicial son propuestas del equipo, configurables y por validar con el docente.

|     |     |     |
| --- | --- | --- |
| **ID** | **Regla** | **Origen** |
| **RN-01** | Todo promedio se muestra con su escala explícita (por ejemplo, sobre 10 por parcial o sobre 20 en la nota final) y con el número de actividades calificadas que lo sustentan. | RF-05 |
| **RN-02** | Cada actividad se clasifica en un estado único: calificada con nota, nota cero, sin entrega, pendiente de calificar o completada sin nota. Una nota cero no es lo mismo que una actividad sin entrega. | RF-06 |
| **RN-03** | Los promedios y las alertas se calculan en código determinista, nunca con el modelo de lenguaje. | Sec. 7 |
| **RN-04** | Alerta de descenso: el promedio de las últimas k evaluaciones es menor que el de las anteriores en más de Δ (inicial: k=3, Δ=1.0 punto). | RF-07 |
| **RN-05** | Alerta de atraso: el estudiante tiene al menos n actividades vencidas sin entrega (inicial: n=2). | RF-07 |
| **RN-06** | Alerta de baja finalización: el porcentaje de actividades completadas sobre las vencidas es menor que p (inicial: p=60%). | RF-07 |
| **RN-07** | Toda alerta muestra su evidencia y se presenta como requiere atención, nunca como diagnóstico ni etiqueta definitiva. | RF-07, RNF-08 |
| **RN-08** | Si los registros están incompletos o faltan datos, se muestra "datos insuficientes" en lugar de una métrica engañosa. | RNF-03 |
| **RN-09** | El agente solo usa datos devueltos por sus herramientas, separa hechos, inferencias y recomendaciones, reconoce incertidumbre y no inventa notas ni estudiantes. | RF-09 |
| **RN-10** | El sistema no envía mensajes automáticos a estudiantes; la decisión final es del docente. | Alcance |
| **RN-11** | No se envía información identificable a servicios externos sin autorización; el modelo de lenguaje se ejecuta de forma local (Ollama) cuando el hardware lo permite. | RNF-02 |
| **RN-12** | Cada docente accede solo a los cursos y estudiantes de sus propios cursos (mínimo privilegio). | RF-01 |

## Indicadores (Cálculo reproducible)

Para garantizar que los promedios y alertas se calculen en código determinista, se aplicarán las siguientes fórmulas, donde son las notas normalizadas a la escala explícita del curso y es el número de actividades calificadas:

# Metodología Scrum + XP

El proyecto se desarrollará utilizando una metodología ágil híbrida que combina la gestión de ciclos de Scrum con las prácticas de excelencia técnica de Extreme Programming (XP), garantizando la entrega de un MVP funcional y de alta calidad en el plazo de 3 sprints (del 19 de octubre al 26 de noviembre de 2026).

## Roles del Equipo

|     |     |     |
| --- | --- | --- |
| **Responsable** | **Rol Scrum** | **Responsabilidad Principal** |
| Daky Fueres | Product Owner / Frontend | Pantallas de login, dashboard, ficha individual, chat; integración con backend;pruebas de interfaz |
| Joel Poaquiza | Scrum Master / Backend | API REST, autorización, módulo académico (cursos, estudiantes y matrículas) y manejo de errores. |
| Monserrath Medina | Dev Team / Datos | Modelo SQL Server, persistencia, indicadores, re-glas de alertas y pruebas unitarias. |
| Fernando Llerena | Dev Team / IA | Servicio de agente (Python/FastAPI), herramientas, prompts y evaluación de respuestas. |
| Docente (Mgs. Hernán Naranjo) | Stakeholder | Valida los entregables en cada Sprint Review. |

Los cuatro integrantes desarrollan. Son responsabilidad compartida la planificación, la integración continua, la revisión de código, la documentación, la seguridad y la demostración; las tareas individuales no eximen de comprender el sistema completo.

## Eventos y artefactos Scrum

Se trabajará con 3 sprints, alineados con la planificación del proyecto (Sprint 1: del 19 al 29 de octubre, con presentación el 29; Sprint 2: hasta el 12 de noviembre; Sprint 3: hasta el 26 de noviembre de 2026). La gestión iterativa del proyecto se realizará mediante los eventos de Scrum, aplicando _timeboxing_ para asegurar un seguimiento continuo del avance y la entrega incremental del MVP.

|     |     |     |     |
| --- | --- | --- | --- |
| **Evento** | **Propósito / Actividad** | **Duración** | **Participantes** |
| **Sprint Planning** | Seleccionar las historias de usuario del Product Backlog (RF-01 al RF-12) y definir las tareas técnicas a desarrollar durante el sprint. | 1 - 2 horas | Product Owner, Scrum Master, Dev Team |
| **Daily Scrum** | Sincronización diaria rápida para responder: ¿Qué hice ayer? ¿Qué haré hoy? ¿Hay algún impedimento técnico? | 15 minutos (Diario) | Scrum Master, Dev Team |
| **Sprint Review** | Presentar el incremento de software funcional (MVP) del sprint para recibir retroalimentación y validar los criterios de aceptación. | 30 - 45 minutos | Todo el equipo de desarrollo, Stakeholder (Docente) |
| **Sprint Retrospective** | Analizar el proceso del sprint para identificar mejoras técnicas (prácticas XP) y optimizar el trabajo en equipo para el siguiente Sprint. | 45 minutos | Scrum Master, Product Owner, Dev Team |

**Artefactos Scrum:**

- **Product Backlog:** Lista priorizada de todos los requerimientos funcionales y no funcionales que debe cumplir el sistema EduMonitor AI.
- **Sprint Backlog:** Conjunto de tareas técnicas asignadas a cada integrante para cumplir el objetivo del sprint.
- **Incremento:** El software funcional, integrado y probado que se entrega al final de cada Sprint (ej. cursos y estudiantes registrados y verificables).

## Prácticas XP Aplicadas

|     |     |
| --- | --- |
| **Práctica XP** | **Cómo se aplica** |
| **Planning Game** | Historias estimadas en puntos de historia mediante Planning Poker. |
| **Test-Driven Development (TDD)** | Indicadores y reglas de alerta se escriben primero como pruebas unitarias; validación determinista de cálculos matemáticos (promedios, desviaciones). |
| **Programación en parejas** | Parejas rotativas en integraciones críticas (módulo académico, backend y agente, interfaz y backend). |
| **Integración continua (CI)** | Compilación y pruebas automáticas en cada push y pull request; validación en repositorio Git para evitar rupturas en la aplicación. |
| **Pequeños lanzamientos** | Un incremento demostrable por sprint, asegurando entregas frecuentes y validables. |
| **Diseño simple** | Resolver la necesidad actual sin sobreingeniería; arquitectura modular enfocada en cumplir el MVP de 3 sprints, evitando microservicios innecesarios. |
| **Refactorización** | Se mejora continuamente el código durante los Sprints, manteniendo pruebas en verde y un diseño limpio y optimizado. |
| **Propiedad colectiva del código** | Revisión cruzada de pull requests; convención de código por tecnología; todo el equipo puede revisar y documentar cualquier componente. |
| **Estándares de código** | Definición de convenciones claras por lenguaje/tecnología para mantener uniformidad y facilitar la mantenibilidad. |
| **Ritmo sostenible** | La carga de trabajo se calcula según la capacidad real del equipo, evitando sobrecarga y asegurando continuidad. |
| **Metáfora del sistema** | Uso de una metáfora compartida para explicar la arquitectura y facilitar la comunicación entre desarrolladores y stakeholders. |
| **Cliente presente** | El docente/usuario clave participa en las revisiones de cada sprint, asegurando retroalimentación inmediata y validación de las funcionalidades entregadas. |

## Definición de Hecho (DoD)

- **Integración y Revisión (Code Review):** Código integrado en la rama **develop** mediante un _Pull Request_ revisado y aprobado por al menos otro integrante del equipo.
- **Pruebas en verde:** Pruebas unitarias y de integración ejecutadas exitosamente, incluidas las pruebas existentes para garantizar que no se haya roto funcionalidad anterior (cero regresiones).
- **Despliegue reproducible:** El sistema se ejecuta correctamente siguiendo las instrucciones del README, levantando todos los servicios mediante el comando **docker compose up**.
- **Seguridad y API:** Endpoints correctamente documentados (OpenAPI/Swagger) y estricta verificación de que no existen credenciales, tokens ni secretos subidos al repositorio (uso de variables de entorno).
- **Comportamiento del Agente IA:** Si la funcionalidad involucra la inteligencia artificial, el agente maneja correctamente las herramientas, reconoce la incertidumbre y no inventa datos, estudiantes ni calificaciones.
- **Deuda técnica cero:** Documentación técnica y del Sprint actualizada, sin arrastrar tareas pendientes para el siguiente ciclo.
- **Validación de Negocio (Product Owner):** Una vez superadas las pruebas técnicas anteriores, los criterios de aceptación de la historia de usuario han sido verificados y aprobados por el Product Owner.

## Control de versiones y calidad

Git con GitFlow: ramas main, develop y feature/HU-xx-nombre; commits descriptivos (el historial de commits forma parte de la entrega); todo cambio entra por pull request con revisión cruzada y la integración continua en verde. Se lleva un registro de contribuciones por integrante

# Arquitectura del Sistema

## Descripción general

Arquitectura modular de tres componentes desplegables: frontend, backend (API) y servicio de agente, con una base de datos SQL Server. Los microservicios no son obligatorios, por lo que el backend se organiza como un monolito modular. El agente nunca accede directamente a credenciales ni a tablas: solo invoca herramientas expuestas por el backend, que aplican autorización por curso.

Figura 1: Arquitectura de EduMonitor AI

## Componentes

- **Frontend (React):** Gestión de inicio de sesión, visualización y selección de cursos, dashboard general, ficha individual del estudiante, visualización de alertas académicas y módulo de chat con el agente.
- **Backend (ASP.NET Core Web API):** Exposición de la API REST, autenticación y autorización por curso, módulo académico local (cursos, estudiantes, matrículas, actividades y calificaciones), cálculo determinista de indicadores, evaluación de reglas de alerta, generación de informes y auditoría del sistema.
- **Base de datos (SQL Server):** Persistencia relacional de usuarios, cursos, calificaciones, estados de actividad, historiales de cambios, alertas generadas y registros de auditoría.
- **Servicio de agente (Python, FastAPI, LangGraph):** Orquestación del modelo local, selección e invocación de herramientas expuestas por el backend, validación de parámetros y registro de trazas de ejecución.
- **Modelo local (Ollama):** Interpretación y explicación de resultados, y redacción de recomendaciones pedagógicas fundamentadas sin enviar datos identificables fuera del entorno local.

## Herramientas del agente

Para garantizar la precisión y evitar alucinaciones, el agente de IA no calcula directamente métricas ni accede sin control a las bases de datos. En su lugar, el backend procesa los datos deterministamente y el modelo solo interpreta, explica y recomienda utilizando herramientas controladas:

|     |     |
| --- | --- |
| **Herramienta** | **Función** |
| **listar_cursos_autorizados()** | Cursos que el docente puede consultar. |
| **obtener_calificaciones(curso)** | Notas y estados de actividad del curso. |
| **obtener_progreso(curso)** | Avance y finalización de actividades. |
| **identificar_alertas(curso)** | Alertas calculadas con las reglas RN-04 a RN-12, con evidencia. |
| **generar_resumen(curso)** | Resumen del curso para el informe. |

_Nota técnica:_ Cada invocación registra explícitamente la herramienta utilizada, los parámetros validados, el resultado obtenido y el manejo de fallos (RF-12).

## Módulo académico local (réplica simplificada de Moodle)

Los datos académicos se gestionan en un módulo local de EduMonitor AI que replica de forma simplificada el modelo de Moodle (cursos, estudiantes, matrículas, actividades, calificaciones y estados). El sistema no se conecta a ningún Moodle, institucional ni propio; Moodle se toma solo como referencia del modelo para poder agregar un conector en el futuro. Los recursos de la API local son:

|     |     |
| --- | --- |
| **Recurso de la API local** | **Uso principal** |
| **/api/auth/login** | Autenticar al usuario y emitir el token con su rol. |
| **/api/cursos** | Crear, editar, eliminar y listar los cursos del docente (referencia: core_enrol_get_users_courses). |
| **/api/cursos/{id}/matriculas** | Matricular, desmatricular y consultar los estudiantes de un curso (referencia: core_enrol_get_enrolled_users). |
| **/api/actividades/{id}/calificaciones** | Registrar y consultar las notas y los estados por estudiante (referencia: gradereport_user_get_grade_items). |
| **/api/estudiantes** | Registrar y buscar estudiantes (nombre, correo e identificación). |
| **/api/cursos/{id}/actividades** | Crear actividades con fecha de entrega y puntaje máximo (referencia: mod_assign_get_assignments). |
| **/api/cursos/{id}/finalizacion** | Consultar el estado de finalización de las actividades por estudiante (referencia: mod_assign_get_submissions). |

## Verificación previa del modelo académico local

Antes de desarrollar los módulos de cursos, estudiantes y calificaciones, el equipo valida con el Product Owner los puntos críticos del modelo local (con evidencia a entregar en el Sprint 1):

|     |     |
| --- | --- |
| **Aspecto** | **Estado** |
| **Entidades y relaciones del modelo académico (curso, estudiante, matrícula, actividad y calificación)** | Pendiente / Validado |
| **Recursos de la API local y sus contratos (OpenAPI)** | Pendiente / Validado |
| **Permisos del docente solo sobre sus propios cursos** | Pendiente / Validado |
| **Escalas de calificación utilizadas por los cursos** | Pendiente / Validado |
| **Estados de actividad definidos (nota cero, sin entrega, pendiente de calificar, completada)** | Pendiente / Validado |

**Referencia a Moodle:** El modelo local se inspira en Moodle pero no depende de él: los datos de prueba son ficticios y cubren los casos de prueba críticos (estudiantes con nota cero, tareas sin entrega y actividades pendientes de calificar).

# Tecnologías y recursos

## Tecnologías

|     |     |
| --- | --- |
| **Capa / Área** | **Tecnología propuesta** |
| **Frontend** | React (interfaz de usuario interactiva y responsiva). |
| **Backend** | ASP.NET Core Web API (C#) con Entity Framework Core (mapeo de entidades; el esquema se crea con el script DDL de TT-03.2, sin migraciones) y autenticación JWT. |
| **Base de datos** | SQL Server (ejecutado en contenedor Docker para desarrollo). |
| **Agente de IA** | Python con FastAPI y LangGraph. |
| **Modelo de lenguaje** | Ollama ejecutando un modelo local con soporte de herramientas (function calling): se parte de Qwen 2.5 Coder y se compara con un modelo instruct general (p. ej., Qwen 2.5 Instruct) en function calling y en español (TT-06.4), ajustado al hardware disponible. |
| **Pruebas** | xUnit (backend), pytest (servicio de agente) y Vitest o Playwright (frontend). |
| **DevOps** | Docker Compose, GitHub y GitHub Actions (CI/CD). |
| **Documentación** | OpenAPI/Swagger y diagramas de arquitectura/UML. |
| **Gestión del proyecto** | Tablero Scrum (Jira, Trello o Azure DevOps). |

## Recursos.

- **Humanos:** Equipo de desarrollo compuesto por 4 integrantes (roles técnicos distribuidos) y el docente como _stakeholder_.
- **Hardware:** Equipos de cómputo personales con especificaciones de memoria RAM y capacidad de procesamiento suficientes para levantar los contenedores de Docker y ejecutar de forma local el modelo de lenguaje mediante Ollama.
- **Software:** Visual Studio o Rider, Visual Studio Code, Node.js, Python, Docker Desktop, Postman y Git.
- **Información:** Conjuntos de datos académicos ficticios del módulo local (cursos, estudiantes, actividades y calificaciones) para validar el registro de datos y los cálculos estadísticos.
- **Tiempo:** 3 sprints con cierre el 29 de octubre, el 12 de noviembre y el 26 de noviembre de 2026.

# Estructura de datos

|     |     |
| --- | --- |
| **Entidad** | **Campo principales** |
| **Usuario y rol** | Nombre, correo, hash de contraseña, rol (docente o administrador técnico). |
| **Matrícula** | Curso y estudiante (combinación única), fecha de matrícula y estado (activa o retirada). |
| **Curso** | Docente propietario, nombre, código, período y escala de calificación. |
| **Estudiante** | Nombre, correo e identificación (datos mínimos; los datos de prueba son ficticios y, con datos reales, el nombre solo lo ve el docente autorizado del curso). |
| **Actividad** | Curso, nombre, tipo, fecha de entrega, puntaje máximo, finalización configurada |
| **Calificación y estado** | Estudiante, actividad, nota, nota máxima, estado (_calificada_, _nota cero_, _sin entrega_, _pendiente de calificar_, _completada_), fecha. |
| **Auditoría** | Usuario, acción, recurso afectado, fecha y resultado (sin secretos). |
| **Regla de alerta** | Tipo (descenso, atraso, baja finalización), parámetros configurables, estado (activa/inactiva). |
| **Alerta** | Estudiante, curso, regla asociada, evidencia del cálculo, fecha de cálculo. |
| **Recomendación e informe** | Curso, período, métricas, hallazgos, recomendaciones pedagógicas, fecha de actualización. |
| **Registro de auditoría** | Fecha, usuario, acción realizada o herramienta invocada, parámetros validados, resultado (sin datos confidenciales). |

# Product Backlog (Basado en RF)

El Product Backlog contiene únicamente **historias de usuario reales**: funcionalidades con valor directo para un usuario del sistema (docente o administrador técnico), escritas como «Como \[rol\], quiero \[funcionalidad\], para \[beneficio\]».

El trabajo de soporte que no entrega valor directo a un usuario (documentación, prototipos, modelo de datos, contratos de API, repositorio y entorno, pruebas, demostración) **no es una historia de usuario**: se registra como tarea técnica en el Backlog técnico (sección 10.2).

Estimación: puntos de historia (PH) para las historias y horas para las tareas técnicas. Prioridad: A (alta) o M (media). El proyecto se organiza en 3 sprints: Sprint 1 (19 al 29 de octubre), Sprint 2 (30 de octubre al 12 de noviembre) y Sprint 3 (13 al 26 de noviembre de 2026).

## Historias de usuario

|     |     |     |     |     |     |
| --- | --- | --- | --- | --- | --- |
| **ID** | **Historia de usuario** | **Requisito** | **Pr.** | **PH** | **Spr.** |
| HU-01 | Como docente o administrador técnico, quiero iniciar sesión con mi rol, para acceder solo a lo que me corresponde. | RF-01 | A   | 5   | 1   |
| HU-02 | Como docente, quiero crear y administrar mis cursos (nombre, período y escala de calificación), para organizar mi trabajo. | RF-02 | A   | 5   | 1   |
| HU-03 | Como docente, quiero registrar estudiantes y matricularlos en mis cursos, para gestionar mi clase. | RF-03 | A   | 5   | 1   |
| HU-04 | Como docente, quiero ver el listado de mis cursos autorizados y seleccionar uno, para elegir el curso a analizar. | RF-04 | A   | 3   | 1   |
| HU-05 | Como docente, quiero crear actividades (fecha de entrega y puntaje máximo) y registrar calificaciones y estados por estudiante, para ver el rendimiento de mi curso. | RF-03, RNF-03 | A   | 8   | 2   |
| HU-06 | Como docente, quiero ver únicamente mis cursos y estudiantes autorizados, para proteger la privacidad de los datos. | RF-01, RNF-01 | A   | 5   | 2   |
| HU-07 | Como docente, quiero un panel con promedio (escala explícita), distribución, actividades evaluadas y progreso, para entender el estado del curso. | RF-05 | A   | 5   | 2   |
| HU-08 | Como docente, quiero la ficha individual con evolución de notas y estados de actividad, para seguir de cerca a cada estudiante. | RF-06 | A   | 5   | 2   |
| HU-09 | Como docente, quiero seleccionar el período y buscar y filtrar estudiantes, para encontrar rápido a quien necesito. | RF-04, RNF-04 | M   | 3   | 2   |
| HU-10 | Como docente, quiero alertas con reglas configurables y evidencia, para saber qué estudiantes requieren atención. | RF-07, RNF-08 | A   | 8   | 2   |
| HU-11 | Como docente, quiero el análisis grupal con actividades de resultados bajos y tendencias, para detectar problemas del curso. | RF-08 | M   | 5   | 2   |
| HU-12 | Como docente, quiero que el agente consulte mis cursos y calificaciones mediante herramientas controladas, para obtener respuestas basadas en datos. | RF-09 | A   | 5   | 3   |
| HU-13 | Como docente, quiero que el agente use al menos tres herramientas (progreso, alertas y resumen), para recibir análisis completos. | RF-09 | A   | 8   | 3   |
| HU-14 | Como docente, quiero un chat que distinga hechos, inferencias y recomendaciones, para conversar con el agente con transparencia. | RF-09, RNF-08 | A   | 3   | 3   |
| HU-15 | Como docente, quiero recomendaciones concretas justificadas con indicadores, para apoyar a mis estudiantes. | RF-10, RNF-08 | A   | 5   | 3   |
| HU-16 | Como docente, quiero un informe consultable o descargable con período, métricas, hallazgos y fecha, para documentar el seguimiento. | RF-11 | M   | 5   | 3   |
| HU-17 | Como administrador técnico, quiero auditoría de cambios académicos, consultas y herramientas del agente, para tener trazabilidad sin exponer secretos. | RF-12 | M   | 5   | 3   |

## Backlog técnico (tareas técnicas)

Estas tareas no son historias de usuario: no entregan una funcionalidad directa al usuario, pero son necesarias para construirla y verificarla. Se estiman en horas.

|     |     |     |     |     |
| --- | --- | --- | --- | --- |
| **ID** | **Tarea técnica** | **Aporta a** | **Horas** | **Spr.** |
| TT-01 | Especificación de requisitos, reglas de negocio e indicadores | RF y RNF | 7   | 1   |
| TT-02 | Prototipos de baja fidelidad de las pantallas del MVP (TT-02.3, prototipo del chat e informe, 2 h, pasa al Sprint 2) | RNF-04 | 10  | 1 y 2 |
| TT-03 | Modelo entidad-relación, esquema SQL Server y datos de prueba | RF-03, RF-12 | 10  | 1   |
| TT-04 | Arquitectura, contratos OpenAPI y contratos de las herramientas del agente | RF-09 | 9   | 1   |
| TT-05 | Diseño de la réplica académica local y datos de prueba | RF-02, RF-03 | 10  | 1   |
| TT-06 | Repositorio GitFlow, tablero Scrum, Docker/CI y verificación de Ollama | RNF-05 | 7   | 1   |
| TT-07 | Documentación del sprint, Sprint Review y retrospectiva | Proceso Scrum | 2   | 1   |
| TT-08 | Pruebas unitarias de indicadores y reglas de alerta (TDD) con datos anonimizados | RNF-02, RNF-06 | 8\* | 2   |
| TT-09 | Servicio de agente (FastAPI, LangGraph y Ollama) con trazas de ejecución | RF-09, RNF-02 | 12\* | 3   |
| TT-10 | Pruebas de permisos, de integración con la API y de rendimiento | RNF-01, RNF-06, RNF-07 | 8\* | 3   |
| TT-11 | Evaluación de las respuestas del agente en tres escenarios y ante fallos de consulta | RF-09 | 4\* | 3   |
| TT-12 | Documentación final y matriz de cumplimiento RF/RNF | RNF-05 | 8\* | 3   |
| TT-13 | Preparación de la demostración en vivo y video corto | Entrega | 4\* | 3   |
| TT-14 | Estabilización y corrección de errores | RNF-03, RNF-06 | 8\* | 3   |

_\* Estimación inicial: se ajustará en el Sprint Planning de cada sprint. Las horas del Sprint 1 corresponden a las tareas planificadas en la sección 15._

## Totales y velocidad

**Totales por sprint:** Sprint 1 = 18 PH (4 historias) y 53 h de tareas técnicas; Sprint 2 = 39 PH (7 historias) y 10 h; Sprint 3 = 31 PH (6 historias) y 44 h.

**Total del proyecto: 88 puntos de historia (PH) en 17 historias de usuario, más 107 h de tareas técnicas.** La planificación inicial se ajustará según la velocidad real del equipo obtenida al finalizar el Sprint 1, permitiendo recalibrar la carga de los sprints posteriores.

**Refinamiento del Product Backlog (26 de octubre de 2026):** el equipo decide no conectar el sistema a ningún Moodle (ni institucional ni propio) y construir un módulo académico local que replica de forma simplificada su modelo. Cambian RF-02, RF-03, HU-02, HU-03, HU-05, TT-05 y las secciones 7.4 y 7.5; los demás requisitos no cambian. El cambio queda pendiente de validación del Product Owner (TT-05.3).

# Trazabilidad de requisitos

|     |     |     |     |     |     |
| --- | --- | --- | --- | --- | --- |
| **Código** | **Requisito** | **Tipo** | **HU / TT asociadas** | **Sprint** | **Componente / Módulo** |
| RF-01 | Autenticación y autorización (roles docente y administrador) | Funcional | HU-01, HU-06 | 1, 2 | Backend (Autenticación) / JWT |
| RF-02 | Gestión de cursos del docente | Funcional | HU-02, TT-05 | 1   | Backend (módulo académico) |
| RF-03 | Gestión de estudiantes, matrículas, actividades y calificaciones | Funcional | HU-03, HU-05, TT-03, TT-05 | 1, 2 | Backend (módulo académico) / SQL Server |
| RF-04 | Gestión de cursos (selección, búsqueda y filtros) | Funcional | HU-04, HU-09 | 1, 2 | Frontend (React) / Backend |
| RF-05 | Panel de indicadores (promedios, distribución y progreso) | Funcional | HU-07 | 2   | Backend (Analítica) / Frontend |
| RF-06 | Seguimiento individual (evolución de notas y estados) | Funcional | HU-08 | 2   | Backend (Analítica) / Frontend |
| RF-07 | Alertas académicas configurables con evidencia | Funcional | HU-10 | 2   | Backend (Módulo de analítica) |
| RF-08 | Análisis grupal (tendencias y resultados bajos) | Funcional | HU-11 | 2   | Backend (Módulo de analítica) |
| RF-09 | Agente de IA con consultas controladas y herramientas | Funcional | HU-12, HU-13, HU-14, TT-04, TT-09, TT-11 | 1, 3 | Servicio de agente (FastAPI / Ollama) / Backend |
| RF-10 | Recomendaciones pedagógicas justificadas | Funcional | HU-15 | 3   | Servicio de agente |
| RF-11 | Informe consultable o descargable | Funcional | HU-16 | 3   | Backend / Frontend |
| RF-12 | Auditoría de cambios académicos, consultas y herramientas de IA | Funcional | HU-17, TT-03 | 1, 3 | SQL Server / Backend |
| RNF-01 | Seguridad (HTTPS, variables de entorno, control por curso) | No funcional | HU-06, TT-10 | 2, 3 | Backend (JWT) / Docker |
| RNF-02 | Privacidad (datos mínimos, anonimización y restricción de IA) | No funcional | TT-08, TT-09 | 2, 3 | SQL Server / Servicio de agente |
| RNF-03 | Confiabilidad (manejo de errores de API y datos ausentes) | No funcional | HU-03, HU-05, TT-14 | 1, 2, 3 | Backend (Clientes HTTP) |
| RNF-04 | Usabilidad (interfaz clara, filtros y mensajes de error) | No funcional | HU-07, HU-08, HU-09, TT-02 | 1, 2 | Frontend (React) |
| RNF-05 | Mantenibilidad (repositorio GitFlow, Docker y documentación) | No funcional | TT-06, TT-12 | 1, 3 | Git / Docker / CI |
| RNF-06 | Calidad (pruebas unitarias, de integración y reproducibilidad) | No funcional | TT-08, TT-10, TT-14 | 2, 3 | xUnit / pytest / Vitest |
| RNF-07 | Rendimiento (consultas paginadas y tiempos estables) | No funcional | TT-10 | 3   | Backend / SQL Server |
| RNF-08 | Transparencia (fuentes, fechas e incertidumbre en la IA) | No funcional | HU-10, HU-14, HU-15 | 2, 3 | Servicio de agente / Frontend |

# Hoja de ruta y análisis de capacidad

|     |     |     |     |
| --- | --- | --- | --- |
| **Sprint** | **Fechas** | **Actividades principales** | **Entregable verificable** |
| Sprint 1 | 19 oct – 29 oct (Sprint Review: 29 oct) | Definición de requisitos, prototipos, modelo de datos, contratos de API, modelo académico local, repositorio, tablero y Docker/CI; HU-01 a HU-04: autenticación por roles, gestión de cursos, estudiantes y matrícula, y listado de cursos. | Documento de especificación, arquitectura inicial, tablero Scrum, repositorio con Docker/CI, autenticación funcional, gestión de cursos, estudiantes y matrícula funcionando y listado de cursos. |

# Capacidad del equipo

Capacidad efectiva = 4 integrantes × horas por día × días hábiles × 0,8 (factor de enfoque). Se planifica con 4 horas diarias de lunes a viernes (valor referencial que cada equipo ajusta); el factor 0,8 descuenta reuniones, bloqueos e imprevistos.

|     |     |     |     |     |     |
| --- | --- | --- | --- | --- | --- |
| **Sprint** | **Fechas** | **Días hábiles** | **Cálculo** | **Capacidad total** | **Capacidad por integrante** |
| Sprint 1 | 19 oct – 29 oct | 9   | 4 integrantes × 4 h × 9 días × 0,8 | 115,2 h | 28,8 h |
| Sprint 2 | 30 oct – 12 nov | 8   | 4 integrantes × 4 h × 8 días × 0,8 | 102,4 h | 25,6 h |
| **Total** | —   | **17** | —   | **217,6 h** | **54,4 h** |

Nota: La planificación inicial se estimó en 88 puntos de historia (PH) distribuidos en 17 historias de usuario y 107 horas de tareas técnicas. La capacidad mostrada corresponde a los Sprints 1 y 2. La distribución definitiva del trabajo se recalibrará al finalizar el Sprint 1 con base en la velocidad real observada del equipo.

# Cronograma de Sprints

|     |     |     |     |
| --- | --- | --- | --- |
| **Sprint** | **Fechas** | **Duración** | **Objetivo principal / Entregable** |
| Sprint 1 | 19 oct – 29 oct 2026 (se presenta el 29 oct) | 9 días hábiles | Planificación, requisitos, arquitectura, repositorio, modelo académico local, autenticación y gestión inicial de cursos y estudiantes. |
| Sprint 2 | 30 oct – 12 nov 2026 | 8 días hábiles | Registro de actividades y notas, permisos por curso, dashboard, seguimiento individual, alertas y análisis grupal. |
| Sprint 3 | 13 nov – 26 nov 2026 | 10 días hábiles | Agente de IA, recomendaciones, informes, auditoría, pruebas y documentación final. |

# Planificación del Sprint 1 (19 – 29 de octubre de 2026)

## Objetivo y entregable del Sprint

**Objetivo del Sprint:** Dejar definida la base del proyecto (documento de requisitos, prototipos, modelo de datos, contratos de API y de las herramientas del agente, repositorio y tablero), contar con el modelo académico local validado y con datos de prueba, y entregar las primeras historias de usuario funcionales: inicio de sesión por roles, gestión de cursos, registro de estudiantes y matrícula, y listado de cursos autorizados.

**Entregable del sprint:** documento de requisitos y planificación, tablero de trabajo, prototipos, esquema de base de datos, contratos de API, modelo académico local validado con el Product Owner e incremento funcional de las historias HU-01 a HU-04, que se presenta en la Sprint Review del 29 de octubre de 2026.

## Sprint 1 Backlog

**Objetivo:** establecer la base funcional y técnica del proyecto mediante la definición de requisitos, el diseño de la solución, la configuración del entorno de desarrollo y la definición del modelo académico local, entregando la autenticación por roles y la gestión básica de cursos y estudiantes.

**Capacidad total del Sprint:** 115,2 horas (4 integrantes × 4 horas diarias × 9 días hábiles, del 19 al 29 de octubre, × 0,8 de factor de enfoque).

**Historias de usuario del sprint (18 PH):**

|     |     |     |     |     |
| --- | --- | --- | --- | --- |
| **ID** | **Historia de usuario** | **Requisito** | **PH** | **Responsables** |
| HU-01 | Como docente o administrador técnico, quiero iniciar sesión con mi rol, para acceder solo a lo que me corresponde. | RF-01 | 5   | Joel Poaquiza, Daky Fueres, Monserrath Medina |
| HU-02 | Como docente, quiero crear y administrar mis cursos (nombre, período y escala de calificación), para organizar mi trabajo. | RF-02 | 5   | Fernando Llerena, Daky Fueres |
| HU-03 | Como docente, quiero registrar estudiantes y matricularlos en mis cursos, para gestionar mi clase. | RF-03 | 5   | Joel Poaquiza, Monserrath Medina |
| HU-04 | Como docente, quiero ver el listado de mis cursos autorizados y seleccionar uno, para elegir el curso a analizar. | RF-04 | 3   | Monserrath Medina, Daky Fueres |

**Tareas del sprint (87 h):** las tareas T-xx.y pertenecen a una historia de usuario; las tareas TT-xx.y pertenecen a una tarea técnica del backlog técnico.

|     |     |     |     |     |     |
| --- | --- | --- | --- | --- | --- |
| **Tarea ID** | **Historia / Tarea técnica** | **Descripción de la tarea** | **Responsable** | **Fechas** | **Horas** |
| T-01.1 | HU-01 | Endpoint de autenticación con JWT y roles | Joel Poaquiza | 26–28 oct | 5   |
| T-01.2 | HU-01 | Pantalla de inicio de sesión y manejo de sesión en React | Daky Fueres | 27–29 oct | 4   |
| T-01.3 | HU-01 | Pruebas unitarias de autenticación y roles | Monserrath Medina | 27–28 oct | 2   |
| T-02.1 | HU-02 | API de cursos (crear, editar y eliminar) con validaciones y control de propietario | Fernando Llerena | 27–29 oct | 4   |
| T-02.2 | HU-02 | Pantalla de creación y edición de cursos (docente) | Daky Fueres | 28–29 oct | 2   |
| T-03.1 | HU-03 | API de estudiantes y de matrícula por curso | Joel Poaquiza | 27–29 oct | 5   |
| T-03.2 | HU-03 | Persistencia de estudiantes y matrículas en SQL Server | Monserrath Medina | 28–29 oct | 3   |
| T-03.3 | HU-03 | Pruebas unitarias de cursos, estudiantes y matrículas | Joel Poaquiza | 27–28 oct | 3   |
| T-03.4 | HU-03 | Pantalla de estudiantes y matrícula en React | Daky Fueres | 28–29 oct | 2   |
| T-04.1 | HU-04 | Endpoint GET /api/cursos filtrado por docente | Monserrath Medina | 28–29 oct | 2   |
| T-04.2 | HU-04 | Pantalla de listado y selección de cursos | Daky Fueres | 28–29 oct | 2   |
| TT-01.1 | TT-01 | Consolidación de RF, RNF, alcance y criterios de aceptación del MVP | Joel Poaquiza | 19–21 oct | 3   |
| TT-01.2 | TT-01 | Formalización de las reglas de negocio institucionales UTA y umbrales de alertas | Monserrath Medina | 20–21 oct | 2   |
| TT-01.3 | TT-01 | Definición de las fórmulas deterministas de indicadores | Monserrath Medina | 22–23 oct | 2   |
| TT-02.1 | TT-02 | Prototipo de login, selector de cursos y dashboard general | Daky Fueres | 19–22 oct | 5   |
| TT-02.2 | TT-02 | Prototipo de ficha individual de estudiante y panel de alertas | Daky Fueres | 22–26 oct | 3   |
| TT-03.1 | TT-03 | Diseño conceptual y lógico del modelo entidad-relación | Monserrath Medina | 21–23 oct | 4   |
| TT-03.2 | TT-03 | Script DDL en T-SQL del esquema de SQL Server | Monserrath Medina | 23–26 oct | 3   |
| TT-03.3 | TT-03 | Revisión cruzada del modelo y del diagrama de base de datos | Fernando Llerena | 26 oct | 1   |
| TT-03.4 | TT-03 | Dataset ficticio y anonimizado para casos críticos | Fernando Llerena | 26–27 oct | 2   |
| TT-04.1 | TT-04 | Diagrama de arquitectura y flujo seguro de consulta del agente | Fernando Llerena | 20–22 oct | 2   |
| TT-04.2 | TT-04 | Contrato OpenAPI (Swagger 3.0) de la API REST | Joel Poaquiza | 21–23 oct | 4   |
| TT-04.3 | TT-04 | Contratos de las herramientas del agente (entradas, salidas y errores) | Fernando Llerena | 23–26 oct | 3   |
| TT-05.1 | TT-05 | Especificación del modelo académico local (entidades, atributos y estados) | Joel Poaquiza | 19–21 oct | 3   |
| TT-05.2 | TT-05 | Script de datos semilla: usuarios, cursos, estudiantes y matrículas ficticios | Fernando Llerena | 22–26 oct | 4   |
| TT-05.3 | TT-05 | Validación del modelo local y registro del cambio de alcance con el Product Owner | Joel Poaquiza | 22–23 oct | 1   |
| TT-05.4 | TT-05 | Datos de prueba de casos críticos (nota cero, sin entrega, pendiente de calificar, completada) | Fernando Llerena | 27–28 oct | 2   |
| TT-06.1 | TT-06 | Configuración del repositorio Git y políticas de ramas | Daky Fueres | 19–20 oct | 2   |
| TT-06.2 | TT-06 | Carga del Product Backlog y la Definición de Hecho en el tablero Scrum | Daky Fueres | 20–21 oct | 1   |
| TT-06.3 | TT-06 | docker-compose base con SQL Server, variables de entorno y CI mínimo | Monserrath Medina | 23–26 oct | 2   |
| TT-06.4 | TT-06 | Verificación de Ollama y comparación de Qwen 2.5 Coder con un modelo instruct general (function calling y español) en el hardware del equipo | Fernando Llerena | 26–28 oct | 2   |
| TT-07.1 | TT-07 | Documentación del sprint, preparación de la Sprint Review y retrospectiva | Daky Fueres | 29 oct | 2   |

**Distribución de carga por integrante (capacidad por miembro: 28,8 h):**

- **Daky Fueres (Frontend / Product Owner):** T-01.2, T-02.2, T-03.4, T-04.2, TT-02.1, TT-02.2, TT-06.1, TT-06.2, TT-07.1 (Total: 23 h, 80 % de su capacidad).
- **Joel Poaquiza (Backend / Scrum Master):** T-01.1, T-03.1, T-03.3, TT-01.1, TT-04.2, TT-05.1, TT-05.3 (Total: 24 h, 83 % de su capacidad).
- **Monserrath Medina (Datos):** T-01.3, T-03.2, T-04.1, TT-01.2, TT-01.3, TT-03.1, TT-03.2, TT-06.3 (Total: 20 h, 69 % de su capacidad).
- **Fernando Llerena (Agente IA / Infraestructura):** T-02.1, TT-03.3, TT-03.4, TT-04.1, TT-04.3, TT-05.2, TT-05.4, TT-06.4 (Total: 20 h, 69 % de su capacidad).

**Nota:** Las estimaciones corresponden únicamente a las tareas principales del backlog (87 h, 76 % de la capacidad de 115,2 h). La capacidad restante se reserva para reuniones Scrum, programación en parejas (XP), revisiones de código, documentación, pruebas, integración continua y soporte técnico.

## Historias de usuario y tareas del Sprint 1

### Historia de usuario 1

|     |     |
| --- | --- |
| **Historia de usuario** |     |
| **Número:** HU-01 | **Usuario:** Docente y Administrador técnico |
| **Nombre historia:** Inicio de sesión por rol (Docente / Administrador técnico) |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Medio |
| **Puntos asignados:** 5 | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Joel Poaquiza (Backend / SM), Daky Fueres (Frontend) y Monserrath Medina (Datos) |     |
| **Descripción:** Como docente o administrador técnico, quiero iniciar sesión con mis credenciales y mi rol, para acceder únicamente a las funciones que me corresponden. |     |
| **Criterios de aceptación (validación):**<br><br>1\. Con correo y contraseña válidos, el usuario ingresa y recibe un token JWT que incluye su rol (docente o administrador técnico).<br><br>2\. Con credenciales inválidas, el sistema rechaza el acceso (HTTP 401) con un mensaje claro que no revela cuál dato falló.<br><br>3\. Las rutas validan el token y el rol: un docente no puede abrir las pantallas del administrador técnico.<br><br>4\. Las contraseñas se almacenan con hash y no existen credenciales ni secretos en el repositorio. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-01.1 | **Número de historia:** HU-01 |
| **Nombre de tarea:** Endpoint de autenticación con JWT y roles |     |
| **Tipo de tarea:** Desarrollo Backend | **Horas estimadas:** 5 h |
| **Fecha inicio:** 26/10/2026 | **Fecha fin:** 28/10/2026 |
| **Programador responsable:** Joel Poaquiza |     |
| **Descripción:** Implementar en ASP.NET Core el endpoint /api/auth/login con validación de credenciales, comparación contra el hash de la contraseña, emisión de un token JWT que incluya el rol y políticas de autorización por rol (Docente y Administrador técnico). Incluye un script de datos semilla (seed) con un usuario administrador técnico y un docente de prueba, con contraseña almacenada con hash, para poder demostrar la historia. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-01.2 | **Número de historia:** HU-01 |
| **Nombre de tarea:** Pantalla de inicio de sesión y manejo de sesión en React |     |
| **Tipo de tarea:** Desarrollo Frontend | **Horas estimadas:** 4 h |
| **Fecha inicio:** 27/10/2026 | **Fecha fin:** 29/10/2026 |
| **Programador responsable:** Daky Fueres |     |
| **Descripción:** Construir la pantalla de login según el prototipo TT-02.1, consumir el endpoint de autenticación, guardar el token de forma segura, redirigir al usuario según su rol y mostrar mensajes de error claros. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-01.3 | **Número de historia:** HU-01 |
| **Nombre de tarea:** Pruebas unitarias de autenticación y roles |     |
| **Tipo de tarea:** Pruebas | **Horas estimadas:** 2 h |
| **Fecha inicio:** 27/10/2026 | **Fecha fin:** 28/10/2026 |
| **Programador responsable:** Monserrath Medina |     |
| **Descripción:** Escribir pruebas unitarias con xUnit (enfoque TDD) para el servicio de autenticación: credenciales válidas e inválidas, expiración del token y restricción de acceso según el rol. |     |

Mockup: Inicio de sesión por rol (Docente / Administrador técnico)

### Historia de usuario 2

|     |     |
| --- | --- |
| **Historia de usuario** |     |
| **Número:** HU-02 | **Usuario:** Administrador técnico (y Docente) |
| **Nombre historia:** Creación y administración de cursos |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Alto |
| **Puntos asignados:** 5 | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Fernando Llerena (IA / Infraestructura, en pareja con Joel Poaquiza) y Daky Fueres (Frontend) |     |
| **Descripción:** Como administrador técnico, quiero crear y administrar los cursos (nombre, código, período, escala de calificación, docente responsable y paralelo), para organizar la estructura académica del sistema. El docente solo puede registrar y matricular estudiantes en sus cursos asignados. |     |
| **Criterios de aceptación (validación):**<br><br>1\. El administrador técnico crea un curso con nombre, código, período, escala de calificación, docente responsable y paralelo (opcional), con validación de campos obligatorios y de código único por docente y período.<br><br>2\. Solo el administrador técnico puede crear, editar y eliminar cursos; los docentes acceden únicamente en modo de consulta a sus cursos asignados y a la gestión de sus estudiantes.<br><br>3\. Ante un error de validación o permisos se muestra un mensaje claro y no se guarda el curso.<br><br>4\. Cada curso queda asociado al docente seleccionado y se persiste directamente en la base de datos SQL Server. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-02.1 | **Número de historia:** HU-02 |
| **Nombre de tarea:** API de cursos (crear, editar y eliminar) con validaciones y control de propietario |     |
| **Tipo de tarea:** Desarrollo Backend | **Horas estimadas:** 4 h |
| **Fecha inicio:** 27/10/2026 | **Fecha fin:** 29/10/2026 |
| **Programador responsable:** Fernando Llerena |     |
| **Descripción:** Desarrollar los endpoints para crear, editar y eliminar cursos con validaciones (campos obligatorios y código único por docente y período), asignando el curso al docente correspondiente y restringiendo la creación/edición exclusivamente al administrador técnico. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-02.2 | **Número de historia:** HU-02 |
| **Nombre de tarea:** Pantalla de creación y edición de cursos (administrador técnico) |     |
| **Tipo de tarea:** Desarrollo Frontend | **Horas estimadas:** 2 h |
| **Fecha inicio:** 28/10/2026 | **Fecha fin:** 29/10/2026 |
| **Programador responsable:** Daky Fueres |     |
| **Descripción:** Crear el formulario para crear y editar cursos (nombre, código, período, escala, docente responsable y paralelo), con validaciones, mensajes de error y persistencia en SQL Server según el wireframe HU-02. |     |

#### Mockup HU-02: Formulario de Creación y Edición de Cursos

**1. Identificación de la Historia de Usuario**

| Parámetro | Detalle |
| :--- | :--- |
| **Número:** | HU-02 |
| **Nombre:** | Creación y administración de cursos |
| **Usuario:** | Administrador técnico |
| **Iteración:** | Sprint 1 |
| **Responsables:** | Fernando Llerena (IA/Infraestructura), Joel Poaquiza, Daky Fueres (Frontend) |
| **Pantalla:** | Formulario de creación y edición de cursos |

**2. Diagrama Wireframe (Balsamiq Style - Vista Gráfica ASCII)**

```
+----------------------------------------------------------------------------------------------------+
| [=] [<] [>] [x] [^] | Administracion de Cursos | https://asistente-academico.local/cursos/nuevo  [ (Q) ]  |
+----------------------------------------------------------------------------------------------------+
| || | Menu             |                                                                            |
| || | ---------------- |  Crear / Editar Curso                                       [HU-02 | UTA]  |
| || |                  |  Formulario de administracion de asignatura                                 |
| || | > Cursos         |  ------------------------------------------------------------------------  |
| || |   Dashboard      |                                                                            |
| || |   Alertas        |  Nombre de la asignatura / curso: (*)                                      |
| || |   Analisis grupal|  +----------------------------------------------------------------------+  |
| || |   Informe        |  | Bases de Datos                                                       |  |
| || |   Agente IA      |  +----------------------------------------------------------------------+  |
| || |                  |                                                                            |
| || |                  |  Codigo de curso: (*)                         Periodo academico: (*)       |
| || |                  |  +-------------------------+                  +-------------------------+  |
| || |                  |  | BD-301                  |                  | Periodo 2026-A      [v] |  |
| || |                  |  +-------------------------+                  +-------------------------+  |
| || |                  |  * Unico por docente y periodo                                             |
| || |                  |                                                                            |
| || |                  |  Escala de calificacion: (*)                                               |
| || |                  |  +----------------------------------------------------------------------+  |
| || |                  |  | Escala UTA (0 a 20 puntos) - Aprobacion >= 14                    [v] |  |
| || |                  |  +----------------------------------------------------------------------+  |
| || |                  |                                                                            |
| || |                  |  Docente responsable:                                                      |
| || |                  |  +----------------------------------------------------------------------+  |
| || |                  |  | Fernando Llerena (Docente autenticado)        [Asignado automaticamente] |
| || |                  |  +----------------------------------------------------------------------+  |
| || |                  |                                                                            |
| || |                  |  Paralelo / Descripcion breve: (Opcional)                                  |
| || |                  |  +----------------------------------------------------------------------+  |
| || |                  |  | Paralelo "A" - Tercer Semestre                                       |  |
| || |                  |  +----------------------------------------------------------------------+  |
| || |                  |                                                                            |
| || |                  |  +---------------------+   +---------------------+                         |
| || |                  |  | [ Guardar curso ]   |   | [ Cancelar ]        |                         |
| || |                  |  +---------------------+   +---------------------+                         |
| || |                  |                                                                            |
| || |                  |  ------------------------------------------------------------------------  |
| || | [Cerrar sesion]  |  * Persistencia directa en base de datos SQL Server                        |
+----+------------------+----------------------------------------------------------------------------+
| HU-02: Formulario Cursos                                                                       /// |
+----------------------------------------------------------------------------------------------------+
```

### Historia de usuario 3

|     |     |
| --- | --- |
| **Historia de usuario** |     |
| **Número:** HU-03 | **Usuario:** Docente |
| **Nombre historia:** Registro de estudiantes y matrícula en cursos |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Alto |
| **Puntos asignados:** 5 | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Joel Poaquiza (Backend / SM) y Monserrath Medina (Datos) |     |
| **Descripción:** Como docente, quiero registrar estudiantes y matricularlos en mis cursos, para gestionar mi clase. |     |
| **Criterios de aceptación (validación):**<br><br>1\. El docente registra estudiantes (nombre, correo e identificación) con validaciones, sin duplicar la identificación, y puede buscarlos por nombre o identificación.<br><br>2\. El docente matricula y desmatricula estudiantes en sus cursos; un estudiante no puede matricularse dos veces en el mismo curso.<br><br>3\. Solo se puede matricular en cursos propios; ante un intento sobre un curso ajeno se rechaza la operación.<br><br>4\. Los cambios se guardan en SQL Server y la lista de matriculados los refleja de inmediato; ante un error el sistema informa y no deja datos a medias (RNF-03). |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-03.1 | **Número de historia:** HU-03 |
| **Nombre de tarea:** API de estudiantes y de matrícula por curso |     |
| **Tipo de tarea:** Desarrollo Backend | **Horas estimadas:** 5 h |
| **Fecha inicio:** 27/10/2026 | **Fecha fin:** 29/10/2026 |
| **Programador responsable:** Joel Poaquiza |     |
| **Descripción:** Implementar los endpoints para registrar y buscar estudiantes y para matricular y desmatricular estudiantes en un curso, validando la identificación única, la matrícula única por curso y que el curso pertenezca al docente (RNF-03). |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-03.2 | **Número de historia:** HU-03 |
| **Nombre de tarea:** Persistencia de estudiantes y matrículas en SQL Server |     |
| **Tipo de tarea:** Base de datos | **Horas estimadas:** 3 h |
| **Fecha inicio:** 28/10/2026 | **Fecha fin:** 29/10/2026 |
| **Programador responsable:** Monserrath Medina |     |
| **Descripción:** Crear las tablas y el acceso a datos de estudiantes y matrículas, con índice único por identificación del estudiante y por curso y estudiante, para evitar duplicados. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-03.3 | **Número de historia:** HU-03 |
| **Nombre de tarea:** Pruebas unitarias de cursos, estudiantes y matrículas |     |
| **Tipo de tarea:** Pruebas | **Horas estimadas:** 3 h |
| **Fecha inicio:** 27/10/2026 | **Fecha fin:** 28/10/2026 |
| **Programador responsable:** Joel Poaquiza |     |
| **Descripción:** Escribir pruebas unitarias de las reglas de cursos y matrículas: código único por docente y período, identificación única, matrícula única por curso y acceso solo a cursos propios. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-03.4 | **Número de historia:** HU-03 |
| **Nombre de tarea:** Pantalla de estudiantes y matrícula en React |     |
| **Tipo de tarea:** Desarrollo Frontend | **Horas estimadas:** 2 h |
| **Fecha inicio:** 28/10/2026 | **Fecha fin:** 29/10/2026 |
| **Programador responsable:** Daky Fueres |     |
| **Descripción:** Crear la pantalla para registrar y buscar estudiantes y para matricular y desmatricular en el curso seleccionado, mostrando los errores de validación. |     |

Mockup: Registro de estudiantes y matrícula en cursos

_\[Mockup pendiente: pantalla de registro de estudiantes y de matrícula en un curso.\]_

### Historia de usuario 4

|     |     |
| --- | --- |
| **Historia de usuario** |     |
| **Número:** HU-04 | **Usuario:** Docente |
| **Nombre historia:** Listado y selección de cursos autorizados |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Bajo |
| **Puntos asignados:** 3 | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Monserrath Medina (Datos) y Daky Fueres (Frontend) |     |
| **Descripción:** Como docente, quiero ver el listado de mis cursos autorizados y seleccionar uno, para elegir el curso que deseo analizar. |     |
| **Criterios de aceptación (validación):**<br><br>1\. El listado muestra solo los cursos que pertenecen al docente autenticado.<br><br>2\. Cada curso muestra nombre, código, período y número de estudiantes matriculados.<br><br>3\. Al seleccionar un curso, este queda como curso activo para las siguientes pantallas.<br><br>4\. Si el docente aún no tiene cursos, se muestra un mensaje que invita a crear el primero. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-04.1 | **Número de historia:** HU-04 |
| **Nombre de tarea:** Endpoint GET /api/cursos filtrado por docente |     |
| **Tipo de tarea:** Desarrollo Backend | **Horas estimadas:** 2 h |
| **Fecha inicio:** 28/10/2026 | **Fecha fin:** 29/10/2026 |
| **Programador responsable:** Monserrath Medina |     |
| **Descripción:** Exponer el endpoint que devuelve únicamente los cursos del docente autenticado, con nombre, código, período y número de estudiantes matriculados. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** T-04.2 | **Número de historia:** HU-04 |
| **Nombre de tarea:** Pantalla de listado y selección de cursos |     |
| **Tipo de tarea:** Desarrollo Frontend | **Horas estimadas:** 2 h |
| **Fecha inicio:** 28/10/2026 | **Fecha fin:** 29/10/2026 |
| **Programador responsable:** Daky Fueres |     |
| **Descripción:** Construir la pantalla con el listado de cursos del docente, la selección del curso activo y el mensaje que invita a crear el primer curso cuando no existen cursos. |     |

Mockup: Listado y selección de cursos autorizados

## Tareas técnicas del Sprint 1

Las siguientes tareas técnicas no son historias de usuario: no entregan una funcionalidad directa al usuario, pero son necesarias para construirla y verificarla. Cada una se estima en horas y se vincula a los requisitos que sustenta.

|     |     |
| --- | --- |
| **Tarea técnica (no es una historia de usuario)** |     |
| **Número:** TT-01 | **Usuario beneficiado:** Equipo de desarrollo y docente (stakeholder) |
| **Nombre de la tarea técnica:** Especificación de requisitos, reglas de negocio e indicadores |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Medio |
| **Horas estimadas:** 7 h | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Joel Poaquiza (Backend / SM) y Monserrath Medina (Datos) |     |
| **Descripción:** Documentar el alcance del MVP, los requerimientos funcionales y no funcionales, los criterios de aceptación y las reglas de negocio institucionales de la UTA, para contar con una base común, determinista y verificable. |     |
| **Criterios de validación:**<br><br>1\. El documento describe RF-01 a RF-12 y RNF-01 a RNF-08 con su criterio verificable.<br><br>2\. Están definidas las reglas RN-01 a RN-12 (descenso k=3 y Δ=1.0; atraso n=2; baja finalización p=60 %) y el marco UTA (40 %, 30 % y 30 %; escala sobre 10 por parcial y sobre 20 en la nota final; aprobación con 14 de 20 puntos).<br><br>3\. El docente revisa y aprueba el documento en la Sprint Review. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-01.1 | **Tarea técnica:** TT-01 |
| **Nombre de tarea:** Consolidación de RF, RNF, alcance y criterios de aceptación del MVP |     |
| **Tipo de tarea:** Análisis y documentación | **Horas estimadas:** 3 h |
| **Fecha inicio:** 19/10/2026 | **Fecha fin:** 21/10/2026 |
| **Programador responsable:** Joel Poaquiza |     |
| **Descripción:** Redactar las secciones de alcance del MVP (qué incluye y qué excluye) y detallar el criterio verificable de cada uno de los 12 requerimientos funcionales y 8 no funcionales, garantizando su trazabilidad con las historias de usuario. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-01.2 | **Tarea técnica:** TT-01 |
| **Nombre de tarea:** Formalización de las reglas de negocio institucionales UTA y umbrales de alertas |     |
| **Tipo de tarea:** Análisis normativo | **Horas estimadas:** 2 h |
| **Fecha inicio:** 20/10/2026 | **Fecha fin:** 21/10/2026 |
| **Programador responsable:** Monserrath Medina |     |
| **Descripción:** Estructurar las reglas RN-01 a RN-12 con los tres componentes institucionales (40 %, 30 % y 30 %), la nota mínima de aprobación de 14 puntos y la asistencia de 70 % (dato manual, fuera del cálculo automático), y validar los umbrales de las alertas con el docente. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-01.3 | **Tarea técnica:** TT-01 |
| **Nombre de tarea:** Definición de las fórmulas deterministas de indicadores |     |
| **Tipo de tarea:** Modelado matemático | **Horas estimadas:** 2 h |
| **Fecha inicio:** 22/10/2026 | **Fecha fin:** 23/10/2026 |
| **Programador responsable:** Monserrath Medina |     |
| **Descripción:** Formular el promedio normalizado a la escala explícita, la varianza, la desviación estándar y el porcentaje de finalización de actividades, con los parámetros configurables para las alertas de descenso, atraso y baja finalización. |     |

|     |     |
| --- | --- |
| **Tarea técnica (no es una historia de usuario)** |     |
| **Número:** TT-02 | **Usuario beneficiado:** Docente |
| **Nombre de la tarea técnica:** Prototipos de baja fidelidad de las pantallas del MVP |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Bajo |
| **Horas estimadas:** 10 h | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Daky Fueres (Product Owner / Frontend) |     |
| **Descripción:** Diseñar prototipos de las pantallas del MVP para que el docente valide el flujo de navegación y la claridad de las métricas antes de construir el frontend (RNF-04). |     |
| **Criterios de validación:**<br><br>1\. Hay prototipos de login, cursos, dashboard, ficha individual, alertas, análisis grupal, informe y chat con el agente.<br><br>2\. El dashboard muestra la escala explícita (sobre 20) y la distribución de calificaciones.<br><br>3\. El chat diferencia hechos, inferencias y recomendaciones.<br><br>4\. El Product Owner y el docente validan los prototipos. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-02.1 | **Tarea técnica:** TT-02 |
| **Nombre de tarea:** Prototipo de login, selector de cursos y dashboard general |     |
| **Tipo de tarea:** Diseño UI/UX | **Horas estimadas:** 5 h |
| **Fecha inicio:** 19/10/2026 | **Fecha fin:** 22/10/2026 |
| **Programador responsable:** Daky Fueres |     |
| **Descripción:** Diseñar la interfaz de autenticación, la vista de cursos asignados al docente con buscador y filtros, y el panel de control con promedio, escala explícita y distribución de calificaciones. |     |

  

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-02.2 | **Tarea técnica:** TT-02 |
| **Nombre de tarea:** Prototipo de ficha individual de estudiante y panel de alertas |     |
| **Tipo de tarea:** Diseño UI/UX | **Horas estimadas:** 3 h |
| **Fecha inicio:** 22/10/2026 | **Fecha fin:** 26/10/2026 |
| **Programador responsable:** Daky Fueres |     |
| **Descripción:** Diseñar la vista de detalle de cada estudiante con la línea de tiempo de notas, la tabla de actividades con sus estados (calificada, nota cero, sin entrega, pendiente) y la tarjeta de alertas con su evidencia. |     |

|     |     |
| --- | --- |
| **Tarea técnica (no es una historia de usuario)** |     |
| **Número:** TT-03 | **Usuario beneficiado:** Equipo de desarrollo |
| **Nombre de la tarea técnica:** Modelo entidad-relación, esquema SQL Server y datos de prueba |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Medio |
| **Horas estimadas:** 10 h | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Monserrath Medina (Datos) y Fernando Llerena (IA / Infraestructura) |     |
| **Descripción:** Diseñar el modelo entidad-relación y el script DDL en SQL Server, junto con datos ficticios y anonimizados, para persistir cursos, estudiantes, actividades, notas, alertas y auditoría de forma consistente y auditable (RF-03 y RF-12). |     |
| **Criterios de validación:**<br><br>1\. El diagrama modela usuarios y roles, cursos, estudiantes, matrículas, actividades, calificaciones, reglas de alerta, alertas, informes y auditoría.<br><br>2\. El script DDL se ejecuta en SQL Server 2022 con claves primarias, foráneas e índices.<br><br>3\. Los datos de prueba cubren los estados de actividad de la regla RN-02 (calificada, nota cero, sin entrega, pendiente de calificar y completada). |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-03.1 | **Tarea técnica:** TT-03 |
| **Nombre de tarea:** Diseño conceptual y lógico del modelo entidad-relación |     |
| **Tipo de tarea:** Diseño de base de datos | **Horas estimadas:** 4 h |
| **Fecha inicio:** 21/10/2026 | **Fecha fin:** 23/10/2026 |
| **Programador responsable:** Monserrath Medina |     |
| **Descripción:** Elaborar el diagrama entidad-relación en tercera forma normal (3FN) con usuarios, cursos, actividades, calificaciones, alertas y auditoría, definiendo los tipos de datos de las notas y de los estados de actividad. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-03.2 | **Tarea técnica:** TT-03 |
| **Nombre de tarea:** Script DDL en T-SQL del esquema de SQL Server |     |
| **Tipo de tarea:** Base de datos | **Horas estimadas:** 3 h |
| **Fecha inicio:** 23/10/2026 | **Fecha fin:** 26/10/2026 |
| **Programador responsable:** Monserrath Medina |     |
| **Descripción:** Escribir el script para crear la base de datos, las tablas, las claves primarias y foráneas, los índices y los campos de auditoría (fechas de creación y actualización). |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-03.3 | **Tarea técnica:** TT-03 |
| **Nombre de tarea:** Revisión cruzada del modelo y del diagrama de base de datos |     |
| **Tipo de tarea:** Revisión de calidad | **Horas estimadas:** 1 h |
| **Fecha inicio:** 26/10/2026 | **Fecha fin:** 26/10/2026 |
| **Programador responsable:** Fernando Llerena |     |
| **Descripción:** Revisar el modelo entidad-relación y el script DDL contra las historias de usuario y las herramientas del agente, y registrar las observaciones para corregir antes de construir los endpoints. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-03.4 | **Tarea técnica:** TT-03 |
| **Nombre de tarea:** Dataset ficticio y anonimizado para casos críticos |     |
| **Tipo de tarea:** Generación de datos | **Horas estimadas:** 2 h |
| **Fecha inicio:** 26/10/2026 | **Fecha fin:** 27/10/2026 |
| **Programador responsable:** Fernando Llerena |     |
| **Descripción:** Generar datos sintéticos de 3 asignaturas y 40 estudiantes ficticios que cubran estudiantes de alto rendimiento, con caída de notas, con tareas pendientes y con nota cero. |     |

|     |     |
| --- | --- |
| **Tarea técnica (no es una historia de usuario)** |     |
| **Número:** TT-04 | **Usuario beneficiado:** Equipo de desarrollo |
| **Nombre de la tarea técnica:** Arquitectura, contratos OpenAPI y contratos de las herramientas del agente |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Medio |
| **Horas estimadas:** 9 h | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Joel Poaquiza (Backend / SM) y Fernando Llerena (IA / Infraestructura) |     |
| **Descripción:** Documentar la arquitectura, la especificación OpenAPI del backend y los contratos de las herramientas del agente, para desarrollar frontend, backend y agente en paralelo sin bloqueos (RF-09). |     |
| **Criterios de validación:**<br><br>1\. El archivo openapi.yaml describe /api/auth/login, /api/cursos, /api/estudiantes, /api/cursos/{id}/matriculas, /api/indicadores/curso/{id}, /api/alertas y /api/informes.<br><br>2\. Están definidos los contratos de las cinco herramientas: listar_cursos_autorizados, obtener_calificaciones, obtener_progreso, identificar_alertas y generar_resumen.<br><br>3\. El diagrama de arquitectura refleja la comunicación entre React, la API ASP.NET Core, SQL Server, FastAPI y Ollama local. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-04.1 | **Tarea técnica:** TT-04 |
| **Nombre de tarea:** Diagrama de arquitectura y flujo seguro de consulta del agente |     |
| **Tipo de tarea:** Arquitectura de software | **Horas estimadas:** 2 h |
| **Fecha inicio:** 20/10/2026 | **Fecha fin:** 22/10/2026 |
| **Programador responsable:** Fernando Llerena |     |
| **Descripción:** Crear los diagramas de componentes y de secuencia que muestran que el modelo de IA nunca accede directamente a SQL Server ni a las credenciales del sistema, y que solo usa las herramientas expuestas por el backend. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-04.2 | **Tarea técnica:** TT-04 |
| **Nombre de tarea:** Contrato OpenAPI (Swagger 3.0) de la API REST |     |
| **Tipo de tarea:** Arquitectura de API | **Horas estimadas:** 4 h |
| **Fecha inicio:** 21/10/2026 | **Fecha fin:** 23/10/2026 |
| **Programador responsable:** Joel Poaquiza |     |
| **Descripción:** Definir rutas, métodos HTTP, DTOs de solicitud y respuesta, códigos de estado (200, 400, 401, 403 y 500) y el esquema de autenticación Bearer JWT de todos los endpoints del backend. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-04.3 | **Tarea técnica:** TT-04 |
| **Nombre de tarea:** Contratos de las herramientas del agente (entradas, salidas y errores) |     |
| **Tipo de tarea:** Diseño de agente IA | **Horas estimadas:** 3 h |
| **Fecha inicio:** 23/10/2026 | **Fecha fin:** 26/10/2026 |
| **Programador responsable:** Fernando Llerena |     |
| **Descripción:** Definir en JSON Schema las firmas de las cinco herramientas, los parámetros de filtrado y el mensaje de respaldo (“datos insuficientes”) cuando una consulta devuelva vacío, junto con el esquema de trazas. |     |

|     |     |
| --- | --- |
| **Tarea técnica (no es una historia de usuario)** |     |
| **Número:** TT-05 | **Usuario beneficiado:** Equipo de desarrollo y administrador técnico |
| **Nombre de la tarea técnica:** Diseño de la réplica académica local y datos de prueba |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Alto |
| **Horas estimadas:** 10 h | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Fernando Llerena (IA / Infraestructura) y Joel Poaquiza (Backend / SM) |     |
| **Descripción:** Definir el modelo académico local que replica de forma simplificada a Moodle (cursos, estudiantes, matrículas, actividades, calificaciones y estados), validarlo con el Product Owner y preparar los datos de prueba, de modo que el sistema no dependa de ningún Moodle externo (RF-02 y RF-03). |     |
| **Criterios de validación:**<br><br>1\. El modelo local define entidades, atributos, relaciones y estados de actividad, con su equivalencia con Moodle.<br><br>2\. Un script de semillas crea usuarios, cursos, estudiantes y matrículas ficticios y se ejecuta en cualquier equipo.<br><br>3\. El Product Owner valida por escrito el cambio de alcance y el modelo local. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-05.1 | **Tarea técnica:** TT-05 |
| **Nombre de tarea:** Especificación del modelo académico local (entidades, atributos y estados) |     |
| **Tipo de tarea:** Análisis y diseño | **Horas estimadas:** 3 h |
| **Fecha inicio:** 19/10/2026 | **Fecha fin:** 21/10/2026 |
| **Programador responsable:** Joel Poaquiza |     |
| **Descripción:** Definir las entidades del módulo académico local (curso, estudiante, matrícula, actividad y calificación), sus atributos, relaciones y estados de actividad, tomando el modelo de Moodle como referencia y documentando la equivalencia. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-05.2 | **Tarea técnica:** TT-05 |
| **Nombre de tarea:** Script de datos semilla: usuarios, cursos, estudiantes y matrículas ficticios |     |
| **Tipo de tarea:** Base de datos | **Horas estimadas:** 4 h |
| **Fecha inicio:** 22/10/2026 | **Fecha fin:** 26/10/2026 |
| **Programador responsable:** Fernando Llerena |     |
| **Descripción:** Crear el script de datos semilla (seed) que genera un usuario administrador técnico y un docente con contraseña almacenada con hash, además de cursos, estudiantes y matrículas ficticios, y que se ejecuta igual en el equipo de cada integrante. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-05.3 | **Tarea técnica:** TT-05 |
| **Nombre de tarea:** Validación del modelo local y registro del cambio de alcance con el Product Owner |     |
| **Tipo de tarea:** Gestión ágil | **Horas estimadas:** 1 h |
| **Fecha inicio:** 22/10/2026 | **Fecha fin:** 23/10/2026 |
| **Programador responsable:** Joel Poaquiza |     |
| **Descripción:** Presentar el modelo académico local al Product Owner, obtener su validación y dejar registrado el cambio de alcance (sin conexión a Moodle) en el Product Backlog. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-05.4 | **Tarea técnica:** TT-05 |
| **Nombre de tarea:** Datos de prueba de casos críticos (nota cero, sin entrega, pendiente de calificar, completada) |     |
| **Tipo de tarea:** Configuración de pruebas | **Horas estimadas:** 2 h |
| **Fecha inicio:** 27/10/2026 | **Fecha fin:** 28/10/2026 |
| **Programador responsable:** Fernando Llerena |     |
| **Descripción:** Agregar al script de semillas actividades calificadas, vencidas sin entrega, con nota cero y pendientes de calificar, para reflejar la casuística de evaluación de la FISEI y usarlas en las pruebas de indicadores del Sprint 2. |     |

|     |     |
| --- | --- |
| **Tarea técnica (no es una historia de usuario)** |     |
| **Número:** TT-06 | **Usuario beneficiado:** Equipo de desarrollo |
| **Nombre de la tarea técnica:** Repositorio GitFlow, tablero Scrum, Docker/CI y verificación de Ollama |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Bajo |
| **Horas estimadas:** 7 h | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Daky Fueres (PO), Monserrath Medina (Datos) y Fernando Llerena (IA) |     |
| **Descripción:** Preparar un ambiente de trabajo estandarizado y reproducible: repositorio con GitFlow, tablero con las historias y tareas, docker-compose con SQL Server e integración continua mínima, y comprobar que el modelo local corre en el hardware del equipo (RNF-05). |     |
| **Criterios de validación:**<br><br>1\. El repositorio en GitHub tiene las ramas main y develop protegidas y Pull Requests con al menos una aprobación.<br><br>2\. El tablero tiene las columnas Backlog, To Do, In Progress, Review / Testing y Done, con la Definición de Hecho visible.<br><br>3\. docker compose levanta SQL Server 2022 y existe el archivo .env.example.<br><br>4\. Ollama ejecuta el modelo local en el hardware del equipo con latencia y memoria medidas. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-06.1 | **Tarea técnica:** TT-06 |
| **Nombre de tarea:** Configuración del repositorio Git y políticas de ramas |     |
| **Tipo de tarea:** DevOps | **Horas estimadas:** 2 h |
| **Fecha inicio:** 19/10/2026 | **Fecha fin:** 20/10/2026 |
| **Programador responsable:** Daky Fueres |     |
| **Descripción:** Estructurar las carpetas /frontend, /backend, /agente-ia y /docs, crear el README con la guía de contribución, proteger las ramas main y develop y definir la plantilla de Pull Request. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-06.2 | **Tarea técnica:** TT-06 |
| **Nombre de tarea:** Carga del Product Backlog y la Definición de Hecho en el tablero Scrum |     |
| **Tipo de tarea:** Gestión ágil | **Horas estimadas:** 1 h |
| **Fecha inicio:** 20/10/2026 | **Fecha fin:** 21/10/2026 |
| **Programador responsable:** Daky Fueres |     |
| **Descripción:** Configurar el tablero (Jira, Trello o GitHub Projects) con las columnas acordadas, registrar las historias de usuario con sus puntos, las tareas técnicas del Sprint 1 y la Definición de Hecho. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-06.3 | **Tarea técnica:** TT-06 |
| **Nombre de tarea:** docker-compose base con SQL Server, variables de entorno y CI mínimo |     |
| **Tipo de tarea:** DevOps | **Horas estimadas:** 2 h |
| **Fecha inicio:** 23/10/2026 | **Fecha fin:** 26/10/2026 |
| **Programador responsable:** Monserrath Medina |     |
| **Descripción:** Crear el docker-compose que levanta SQL Server 2022, el archivo .env.example y un flujo mínimo de integración continua con GitHub Actions que compile y ejecute las pruebas. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-06.4 | **Tarea técnica:** TT-06 |
| **Nombre de tarea:** Verificación de Ollama y comparación de Qwen 2.5 Coder con un modelo instruct general (function calling y español) en el hardware del equipo |     |
| **Tipo de tarea:** Investigación y DevOps | **Horas estimadas:** 2 h |
| **Fecha inicio:** 26/10/2026 | **Fecha fin:** 28/10/2026 |
| **Programador responsable:** Fernando Llerena |     |
| **Descripción:** Ejecutar pruebas de inferencia local con Ollama y prompts de analítica académica para medir la latencia (segundos) y el consumo de memoria RAM, y elegir el modelo definitivo. |     |

|     |     |
| --- | --- |
| **Tarea técnica (no es una historia de usuario)** |     |
| **Número:** TT-07 | **Usuario beneficiado:** Todo el equipo |
| **Nombre de la tarea técnica:** Documentación del sprint, Sprint Review y retrospectiva |     |
| **Prioridad en negocio:** Alta | **Riesgo en desarrollo:** Bajo |
| **Horas estimadas:** 2 h | **Iteración asignada:** Sprint 1 |
| **Programador responsable:** Daky Fueres (PO) con todo el equipo |     |
| **Descripción:** Cerrar el Sprint 1 con la documentación actualizada, la presentación del incremento el 29 de octubre y la retrospectiva. |     |
| **Criterios de validación:**<br><br>1\. La documentación del sprint está actualizada.<br><br>2\. En la Sprint Review se demuestra el incremento de HU-01 a HU-04 y se presentan los entregables técnicos.<br><br>3\. La retrospectiva queda registrada con acciones de mejora para el Sprint 2. |     |

|     |     |
| --- | --- |
| **Tarea de ingeniería** |     |
| **Número de tarea:** TT-07.1 | **Tarea técnica:** TT-07 |
| **Nombre de tarea:** Documentación del sprint, preparación de la Sprint Review y retrospectiva |     |
| **Tipo de tarea:** Gestión ágil | **Horas estimadas:** 2 h |
| **Fecha inicio:** 29/10/2026 | **Fecha fin:** 29/10/2026 |
| **Programador responsable:** Daky Fueres |     |
| **Descripción:** Actualizar la documentación del sprint, preparar la demostración del incremento para la Sprint Review del 29 de octubre y registrar la retrospectiva con las acciones de mejora para el Sprint 2. |     |

# Plan de trabajo, riesgos y cierre del sprint

## Orden de elaboración y dependencias

- TT-06 (repositorio y tablero) y TT-05 (especificación del modelo académico local) arrancan el primer día, el 19 de octubre: HU-02, HU-03 y HU-04 dependen de que el modelo local esté definido y validado por el Product Owner.
- TT-01 (requisitos) y TT-02 (prototipos) avanzan en paralelo y se retroalimentan.
- TT-03 (modelo de datos) usa las reglas de negocio de TT-01 y el modelo académico local definido en TT-05.
- TT-04 (arquitectura y contratos) se apoya en TT-01 y TT-03 y habilita el desarrollo de las historias.
- Entre el 26 y el 29 de octubre se construyen las historias en este orden: HU-01 (login), HU-02 (cursos), HU-03 (estudiantes y matrícula) y HU-04 (listado de cursos), porque cada una depende de la anterior.
- La verificación de Ollama (TT-06.4) se hace pronto para no bloquear el Sprint 3.

## Riesgos y Mitigación

|     |     |
| --- | --- |
| **Riesgo** | **Mitigación** |
| Que el modelo académico local no cubra los casos que hoy resuelve Moodle (roles, escalas, estados de actividad) o cambie el alcance sin validación. | Definirlo y validarlo con el Product Owner (TT-05.1 y TT-05.3), tomar Moodle como referencia y cubrir los casos críticos con datos de prueba (TT-05.4). |
| Latencia alta del agente IA (Ollama). | Usar modelos ligeros, límites de tiempo (timeouts) y comunicación asíncrona. |
| Datos incompletos o inconsistentes en el módulo académico (notas o actividades sin registrar). | Diseñar reglas que eviten métricas engañosas y reporten “datos insuficientes”. |
| El agente IA “alucina” datos. | El agente **debe** usar herramientas (function calling) conectadas al backend. Si falla, debe decir que no puede consultar. |
| Falta de tiempo (3 sprints, hasta el 26 de noviembre). | Priorizar el MVP obligatorio y no salirse del alcance establecido. |
| Sprint 1 corto (9 días hábiles) con muchas tareas de base y cuatro historias. | Priorizar HU-01 a HU-03; si el tiempo no alcanza, HU-04 pasa al Sprint 2 y se recalibra la velocidad en la retrospectiva. |

## Comunicación y seguimiento

- Daily de 15 minutos (puede ser asíncrono por el grupo de mensajería del equipo).
- Tablero Scrum con el Product Backlog y el Sprint Backlog actualizados a diario.
- Repositorio en GitHub con ramas por historia y pull requests revisados.
- Review y Retrospectiva al cierre de cada sprint (29 de octubre, 12 de noviembre y 26 de noviembre de 2026), con avance frente a los entregables del cronograma.

## Pruebas de aceptación y cierre del Sprint 1

La Sprint Review del **29 de octubre de 2026** presenta el incremento del Sprint 1. Las pruebas de aceptación se realizan de forma manual siguiendo los criterios de validación de cada historia y tarea técnica.

|     |     |     |     |
| --- | --- | --- | --- |
| **Responsable** | **Módulo** | **Prueba** | **Estado** |
| Joel Poaquiza | Backend – Autenticación | Login con credenciales válidas e inválidas; el token incluye el rol (HU-01). | Planificada |
| Daky Fueres | Frontend – Login | La pantalla redirige según el rol y muestra errores claros (HU-01). | Planificada |
| Fernando Llerena | Backend – Gestión de cursos | Crear, editar y eliminar cursos; no se puede acceder a cursos de otro docente (HU-02). | Planificada |
| Joel Poaquiza | Backend – Estudiantes y matrícula | Registro de estudiantes y matrícula sin duplicados y solo en cursos propios (HU-03). | Planificada |
| Monserrath Medina | Base de datos | El script DDL se ejecuta en SQL Server 2022 y el dataset cubre los estados de actividad (TT-03). | Planificada |
| Monserrath Medina | Backend – Cursos | GET /api/cursos devuelve solo los cursos del docente autenticado (HU-04). | Planificada |
| Fernando Llerena | Agente IA – Ollama | El modelo local responde en el hardware del equipo con latencia y memoria medidas (TT-06.4). | Planificada |
| Todo el equipo | Entorno Docker | docker compose levanta SQL Server 2022 siguiendo el README (TT-06.3). | Planificada |

# Conclusiones y Recomendaciones

## Conclusiones

- La planificación estructura el proyecto en 3 sprints (del 19 de octubre al 26 de noviembre de 2026), asegurando que el MVP sea entregable y evaluable.
- La separación del agente (Python) y el backend (.NET) permite un desarrollo paralelo y desacoplado.
- El enfoque en reglas deterministas garantiza la transparencia y trazabilidad de las alertas, evitando que la IA invente diagnósticos.

## Recomendaciones

- Documentar en el README los pasos para levantar el entorno local (Docker, Ollama, variables de entorno).
- Aplicar TDD estrictamente en el cálculo de promedios y distribución de notas.
- Realizar pruebas de penetración básicas para asegurar que un docente no acceda a los cursos de otro.