# PLANIFICACIÓN DEL SPRINT 2

**UNIVERSIDAD TÉCNICA DE AMBATO**

**FACULTAD DE INGENIERÍA EN SISTEMAS ELECTRÓNICA E INDUSTRIAL — CARRERA DE SOFTWARE**

**Proyecto:** EduMonitor AI — Sistema inteligente de seguimiento y análisis del rendimiento académico integrado con Moodle.

**Asignatura:** Metodologías Ágiles · **Docente:** Ing. Hernán Naranjo, Mgs.

**Integrantes:** Fernando Llerena, Daky Fueres, Joel Poaquiza, Monserrath Medina.

**Sprint:** 2 · **Fechas:** 30 de octubre al 12 de noviembre de 2026 · **Duración:** 8 días hábiles.

---

## 1. Objetivo del sprint

Construir el núcleo académico y analítico del MVP: consultar las actividades y calificaciones sincronizadas desde Moodle con las reglas UTA, restringir el acceso a los cursos propios del docente, calcular indicadores deterministas del curso y del estudiante, generar alertas configurables con evidencia y producir un análisis grupal que permita detectar dificultades antes del cierre del período.

**Resultado esperado (incremento):** un docente autenticado puede seleccionar un período y un curso, consultar las actividades y calificaciones sincronizadas desde Moodle, revisar el dashboard, abrir la ficha individual, consultar alertas justificadas y analizar el rendimiento del grupo. Los datos llegan solos desde Moodle mediante una sincronización automática. El administrador técnico gestiona las cuentas de los docentes, supervisa la sincronización y configura los umbrales de las alertas.

---

## 2. Alcance comprometido

### 2.1 Historias de usuario

| ID | Historia | Requisito | PH |
| --- | --- | --- | --- |
| HU-05 | Consultar las actividades (componente, parcial, fecha de entrega y puntaje máximo) y las calificaciones y estados por estudiante sincronizados desde Moodle, con enlace a Moodle para calificar. | RF-03, RNF-03 | 8 |
| HU-06 | Ver únicamente los cursos y estudiantes autorizados del docente autenticado. | RF-01, RNF-01 | 5 |
| HU-07 | Panel con promedio (escala explícita), distribución, actividades evaluadas y progreso. | RF-05 | 5 |
| HU-08 | Ficha individual con evolución de notas y estados de actividad. | RF-06 | 5 |
| HU-09 | Seleccionar período y buscar/filtrar estudiantes y asignaturas. | RF-04, RNF-04 | 3 |
| HU-10 | Alertas con reglas configurables y evidencia verificable. | RF-07, RNF-08 | 8 |
| HU-11 | Análisis grupal con actividades de resultados bajos y tendencias. | RF-08 | 5 |

**Total: 39 PH.**

> **Refinamiento (10 de noviembre de 2026) — HU-05 reformulada.** La versión inicial decía “Crear actividades y registrar calificaciones” en EduMonitor. Con la sincronización de Moodle eso duplicaba el trabajo del docente, que ya crea y califica en Moodle, y generaba dos fuentes de verdad, porque los estudiantes solo ven Moodle. Ahora la historia es de **consulta**: EduMonitor muestra las actividades y notas sincronizadas, calcula el estado efectivo (RN-02) y ofrece enlaces “Abrir en Moodle” / “Calificar en Moodle”. Se eliminaron `POST/PUT/DELETE` de actividades y `POST` de calificaciones. Tampoco se registran estudiantes ni matrículas: vienen de Moodle. Se mantienen los 8 PH: el esfuerzo pasó a la sincronización y a los enlaces. Pendiente de validación del Product Owner.

### 2.2 Tareas técnicas del sprint

| ID | Tarea | Entregable |
| --- | --- | --- |
| TT-08 | Pruebas unitarias de indicadores y reglas de alerta (TDD). | `backend.Tests/AnaliticaServiceTests.cs` y `Sprint2EndpointsTests.cs` |
| TT-02.3 | Prototipo de baja fidelidad del chat e informe. | `docs/PROTOTIPO_CHAT_INFORME.md` (wireframes y comportamiento) y vista previa en `/agente` e `/informe` del frontend |
| TT-03.5 | Scripts SQL de actividades, calificaciones, reglas e integración con Moodle. | `docs/sql/04-actividades-calificaciones.sql`, `docs/sql/05-reglas-alerta.sql`, `docs/sql/06-integracion-moodle.sql` |
| TT-04.4 | Actualización de contratos OpenAPI. | `docs/openapi.yaml` (incluye sincronización y cuentas de docentes) |

### 2.3 Trabajo agregado por los refinamientos del 10-nov-2026

Al pasar a Moodle como fuente de verdad, se sumó al sprint, sin cambiar los puntos de historia:

| Trabajo | Motivo | Entregable |
| --- | --- | --- |
| Integración con Moodle: conexión (RF-02) y sincronización (RF-03) | Los datos académicos vienen de Moodle | `MoodleService/`, `backend/Services/SincronizacionMoodleService.cs`, `MoodleImportador.cs`, `frontend/src/pages/AdminMoodle.jsx` |
| Sincronización automática cada 15 min | Que el docente no dependa de que el administrador pulse un botón | `backend/Services/SincronizacionAutomaticaWorker.cs` |
| Enlaces “Abrir / Calificar en Moodle” | HU-05 reformulada a consulta | `backend/Services/EnlacesMoodle.cs` |
| Gestión de cuentas de docentes (HU-02 reformulada) | El administrador ya no crea cursos; crea las cuentas que la sincronización empareja por correo | `backend/Controllers/UsuariosController.cs`, `frontend/src/pages/AdminDocentes.jsx` |

---

## 3. Reglas de negocio implementadas

### 3.1 Evaluación UTA

- Cada parcial se compone de tres componentes con ponderación fija:
  - **Contacto con el docente:** 40 %.
  - **Aprendizaje práctico-experimental:** 30 %.
  - **Aprendizaje autónomo:** 30 %.
- La nota de cada componente se normaliza sobre **10 puntos**.
- La nota de cada parcial se calcula como `0,40·C + 0,30·P + 0,30·A` sobre 10.
- La **nota final** se calcula sobre **20 puntos** (`parcial 1 + parcial 2`).
- La aprobación proyectada exige **nota final ≥ 14/20** (RN-08).
- La nota final **solo se muestra cuando los tres componentes de ambos parciales tienen datos**. Si falta un componente, el resultado es “pendiente”, nunca cero.

### 3.2 Estados de actividad (RN-02)

| Estado | Significado | ¿Promedia? |
| --- | --- | --- |
| `calificada` | Entrega con nota válida. | Sí |
| `nota_cero` | Entrega evaluada con 0. | Sí (0) |
| `sin_entrega` | Actividad vencida sin registro. | No |
| `pendiente` | Actividad abierta sin calificar. | No |
| `completada` | Actividad entregada, aún sin nota. | No (cuenta como finalizada) |

Regla de resolución: si no existe registro explícito, una actividad vencida se interpreta como `sin_entrega` y una actividad futura como `pendiente`.

### 3.3 Alertas deterministas (RN-04 a RN-06)

| Tipo | Regla | Parámetros por defecto | Evidencia mostrada |
| --- | --- | --- | --- |
| `descenso` | Promedio de las últimas `k` evaluaciones cae más de `Δ` puntos sobre 10 respecto a las anteriores. | `k=3`, `Δ=1.0` | Promedios, caída calculada y umbral. |
| `atraso` | `n` o más actividades vencidas sin entrega. | `n=2` | Cantidad y nombres de actividades. |
| `baja_finalizacion` | Finalización de actividades vencidas menor que `p`. | `p=60 %` | Porcentaje, completadas/total y umbral. |

Ninguna alerta usa el modelo de lenguaje. Todas incluyen `regla`, `severidad`, `evidencia`, `fechaCalculo` y el identificador del estudiante. El frontend las presenta como señales de seguimiento, **no como diagnósticos definitivos**.

### 3.4 Permisos por curso (RN-12 / HU-06)

- Todo endpoint académico resuelve el curso y verifica la propiedad antes de devolver datos.
- Docente: solo cursos con `DocenteId` igual a su usuario autenticado.
- Administrador técnico: sin acceso a notas, estudiantes, indicadores, alertas ni análisis de los cursos (`403`). Solo ve los datos generales de los cursos para verificar la sincronización (mínimo privilegio; refinamiento del 10-nov-2026).
- Curso inexistente: `404`. Curso de otro docente: `403`. Sin token: `401`.
- La configuración de reglas de alerta es exclusiva del Administrador Técnico (`PUT /api/alertas/reglas/{id}`).

---

## 4. Arquitectura del incremento

### 4.1 Backend (.NET 10 / ASP.NET Core)

| Archivo | Responsabilidad |
| --- | --- |
| `backend/Domain/Entidades.cs` | `Actividad`, `Calificacion`, `ReglaAlerta`, componentes y estados. |
| `backend/Data/AppDbContext.cs` | Mapeo EF Core, índices únicos y cascadas. |
| `backend/Services/AnaliticaService.cs` | Cálculos deterministas: evaluaciones, dashboard, ficha, alertas y análisis. |
| `backend/Services/AnaliticaProvider.cs` | Carga coherente del contexto analítico del curso. |
| `backend/Controllers/ControladorAcademicoBase.cs` | Autorización reutilizable por curso. |
| `backend/Controllers/ActividadesController.cs` | Consulta de actividades (con enlace a Moodle) y finalización. |
| `backend/Controllers/CalificacionesController.cs` | Consulta de notas y estado efectivo por estudiante. |
| `backend/Controllers/IndicadoresController.cs` | Dashboard y ficha individual. |
| `backend/Controllers/AlertasController.cs` | Alertas y reglas configurables. |
| `backend/Controllers/AnalisisController.cs` | Análisis grupal. |
| `backend/Controllers/CursosController.cs` | Consulta de cursos con filtros por período y búsqueda, y nómina. |
| `backend/Controllers/SincronizacionController.cs` | Sincronización manual, historial y estado (fuente y fecha, RNF-08). |
| `backend/Services/SincronizacionAutomaticaWorker.cs` | Sincronización automática periódica. |
| `backend/Controllers/UsuariosController.cs` | Cuentas de docentes (solo administrador). |
| `backend/Data/DbInitializer.cs` | DDL, usuarios iniciales y reglas por defecto. Los datos locales de demostración solo se cargan en las pruebas. |
| `MoodleService/` | Microservicio que conoce Moodle: conexión cifrada y extracción vía servicios web REST. |

### 4.2 Frontend (React + Vite)

| Archivo | Responsabilidad |
| --- | --- |
| `frontend/src/cursos/CursoContext.jsx` | Curso activo compartido por todo el espacio del docente (con `useCursos.js`: período y búsqueda) y contador de alertas. |
| `frontend/src/components/CursoSwitcher.jsx` | Selector de asignatura y período en el menú lateral (HU-09). |
| `frontend/src/components/ui.jsx` | Componentes del espacio de trabajo: paneles, KPI, medidor de nota, avatar, chips, sparkline y modal. |
| `frontend/src/pages/Resumen.jsx` | Resumen del curso en una pantalla: indicadores, distribución, parciales, a quién atender y qué reforzar (HU-07). |
| `frontend/src/pages/Estudiantes.jsx` | Lista con búsqueda y filtros + ficha individual en pestañas (HU-06, HU-08, HU-09). |
| `frontend/src/pages/Actividades.jsx` | Lista por parcial + notas de la actividad, con “Calificar en Moodle” (HU-05). |
| `frontend/src/pages/Alertas.jsx` | Lista filtrable + evidencia y regla aplicada; reglas en modal (HU-10). |
| `frontend/src/pages/Analisis.jsx` | Logro por actividad frente al 60 %, hallazgos y componentes; detalle en modal (HU-11). |
| `frontend/src/pages/AdminReglas.jsx` | Configuración de umbrales (solo administrador). |
| `frontend/src/pages/AdminMoodle.jsx` | Conexión, sincronización e historial (solo administrador). |
| `frontend/src/pages/AdminDocentes.jsx` | Cuentas de docentes (solo administrador). |
| `frontend/src/utils/formato.js` | Formatos y etiquetas compartidas. |
| `frontend/src/workspace.css` | Estructura del espacio de trabajo: pantallas de alto fijo con paneles lista ↔ detalle. |
| `frontend/src/theme.css` | Tema minimalista (tipografía Geist, grises, un acento azul y rojo solo para atención). Ver `DESIGN.md`. |

---

## 5. Contratos de API del sprint

| Método | Ruta | Rol | Descripción |
| --- | --- | --- | --- |
| GET | `/api/cursos?periodo=&q=` | Docente / Admin | Cursos filtrados por período y texto (el admin ve todos, sin notas). |
| GET | `/api/cursos/periodos` | Docente / Admin | Períodos disponibles del usuario. |
| GET | `/api/cursos/{cursoId}/matriculas?q=&estado=` | Docente propietario | Nómina filtrable. |
| GET | `/api/cursos/{cursoId}/actividades` | Docente propietario | Actividades del curso. |
| GET | `/api/actividades/{id}` | Docente propietario | Detalle de actividad. |
| GET | `/api/actividades/{id}/calificaciones` | Docente propietario | Nómina con estado efectivo. |
| GET | `/api/cursos/{cursoId}/finalizacion` | Docente propietario | Finalización por estudiante. |
| GET | `/api/indicadores/curso/{cursoId}` | Docente propietario | Dashboard del curso. |
| GET | `/api/indicadores/curso/{cursoId}/estudiantes/{estudianteId}` | Docente propietario | Ficha individual. |
| GET | `/api/alertas/curso/{cursoId}` | Docente propietario | Alertas con evidencia. |
| GET | `/api/alertas/reglas` | Docente / Admin | Reglas configuradas. |
| PUT | `/api/alertas/reglas/{id}` | Admin | Actualizar parámetros/estado. |
| GET | `/api/analisis/curso/{cursoId}` | Docente propietario | Análisis grupal. |
| POST | `/api/sincronizacion/moodle` | Admin | Sincronizar ahora (también corre sola cada 15 min). |
| GET | `/api/sincronizacion/moodle/historial` | Admin | Historial con fecha, contadores y errores. |
| GET | `/api/sincronizacion/moodle/estado` | Docente / Admin | Fuente y fecha de los datos (RNF-08). |
| GET/POST/PUT | `/api/usuarios/docentes…` | Admin | Cuentas de docentes (HU-02 reformulada). |

El contrato completo, con esquemas y ejemplos, está en `docs/openapi.yaml`.

---

## 6. Estrategia de pruebas (TT-08)

### 6.1 Enfoque TDD

1. Se definieron primero los casos de cálculo (ponderación, estados, nota final, alertas y tendencias).
2. Se implementó `AnaliticaService` como función pura sobre `ContextoAnalitica`, sin dependencia de EF Core ni del modelo de lenguaje.
3. Se cubrieron los endpoints con pruebas de integración sobre `WebApplicationFactory` y base InMemory.

### 6.2 Casos cubiertos

**Cálculos (`AnaliticaServiceTests`):**

- Ponderación 40/30/30 y nota final sobre 20.
- Nota final nula cuando falta un componente.
- Resolución de estados `sin_entrega` y `pendiente`.
- `completada` cuenta como finalizada pero no promedia.
- Alerta de descenso con evidencia numérica.
- Alertas de atraso y baja finalización con umbrales.
- Actividades futuras no generan alertas.
- Reglas desactivadas no generan alertas.
- Tendencia grupal de mejora y actividades bajo el umbral.
- Escala de curso distinta de 20.
- Ficha de estudiante no matriculado devuelve `null`.

**Integración (`Sprint2EndpointsTests`):**

- Consulta de actividades, detalle y calificaciones con estado efectivo.
- Actividades, notas, cursos y matrículas no se modifican desde la API (`405`).
- Enlaces a Moodle del curso y de cada actividad tras la sincronización.
- Acceso `403` a los datos académicos de cursos de otro docente y también para el administrador técnico.
- Dashboard y ficha con datos reales.
- Alertas con evidencia y reglas configurables.
- Solo el administrador puede editar reglas; validación de JSON y parámetros.
- Filtros de período y búsqueda.
- Finalización por estudiante.
- `401` sin token.

**Integración con Moodle (`MoodleSincronizacionTests`, `MoodleService.Tests`):** importación idempotente, bajas de Moodle reflejadas como matrículas retiradas, curso omitido si su docente no tiene cuenta, historial con errores, sincronización automática con token técnico y mapeo de ítems de Moodle al modelo UTA.

**Cuentas de docentes (`UsuariosEndpointsTests`):** crear, correo duplicado (`409`), clave débil (`400`), desactivar, desbloquear, restablecer clave y acceso denegado al docente (`403`).

**Resultado:** 86 pruebas aprobadas (`dotnet test EduMonitor.slnx`: 65 del backend y 21 de MoodleService), 0 errores. Incluye `UsuariosEndpointsTests` (gestión de cuentas de docentes, HU-02).

### 6.3 Datos de prueba

El Moodle local de Docker (`moodle/seed.php`) crea 3 cursos del período 2026-B con 8 actividades (tres componentes UTA en ambos parciales y una futura) y patrones de notas que garantizan los casos de abajo. El curso *Sistemas Operativos* tiene un profesor sin cuenta en EduMonitor, para demostrar el registro de errores. Las pruebas automáticas usan los mismos patrones sembrados localmente por `DbInitializer` (`Seed:DatosDemoLocales`):

- estudiante de alto rendimiento;
- estudiante con descenso sostenido;
- estudiante con atrasos reiterados;
- estudiante con nota cero y estados pendientes;
- estudiante con rendimiento bajo general.

Los scripts `docs/sql/04` a `docs/sql/06` reproducen el esquema en SQL Server 2022 de forma idempotente.

---

## 7. Plan de trabajo

| Día | Actividad | Responsable |
| --- | --- | --- |
| 30 oct | Sprint Planning, modelo de actividades/calificaciones y contratos. | Todo el equipo |
| 30–31 oct | Entidades, DbContext, DDL y datos semilla. | Monserrath Medina / Fernando Llerena |
| 2–3 nov | `AnaliticaService` y pruebas TDD de indicadores y alertas. | Joel Poaquiza |
| 4–5 nov | Endpoints de consulta de actividades, calificaciones y permisos por curso. | Fernando Llerena |
| 5–6 nov | Dashboard, ficha individual y análisis grupal (API). | Joel Poaquiza |
| 6–7 nov | Pantallas de actividades, dashboard, ficha y alertas. | Daky Fueres |
| 9–10 nov | Filtros de período/búsqueda, configuración de reglas y estilo. | Daky Fueres |
| 11 nov | Pruebas de integración, corrección de defectos y OpenAPI. | Todo el equipo |
| 12 nov | Sprint Review, demostración y retrospectiva. | Todo el equipo |

### Dependencias

- El modelo de datos (actividades/calificaciones) habilita los endpoints y la analítica.
- La analítica determinista habilita dashboard, ficha, alertas y análisis.
- El backend autorizado habilita las pantallas del frontend.
- La configuración de reglas depende del endpoint de administración.

---

## 8. Criterios de aceptación

| Historia | Criterio de aceptación |
| --- | --- |
| HU-05 | El docente ve las actividades de sus cursos con componente, parcial, entrega, puntaje y avance de calificación, y la nota y el estado efectivo (RN-02) de cada estudiante. Los datos provienen de Moodle; EduMonitor no permite crearlos ni editarlos y ofrece enlaces para hacerlo en Moodle. |
| HU-06 | Un docente no puede leer actividades, calificaciones, indicadores, alertas ni análisis de cursos ajenos (`403`). El administrador técnico tampoco accede a esos datos (`403`); solo ve los datos generales de los cursos. |
| HU-07 | El dashboard muestra promedio con escala explícita, distribución por rangos, actividades evaluadas, progreso y advertencias cuando faltan datos. |
| HU-08 | La ficha muestra promedio, parciales, nota final (solo si está completa), componentes, evolución, estados y alertas del estudiante. |
| HU-09 | El período y la búsqueda filtran cursos sin exponer cursos no autorizados. En el dashboard se buscan estudiantes por nombre, cédula o correo y se filtran: con alertas, nota final < 14, sin entrega o nota final pendiente. |
| HU-10 | Cada alerta incluye regla, severidad y evidencia; el administrador puede ajustar `k`, `Δ`, `n` y `p`, y desactivar reglas. |
| HU-11 | El análisis grupal identifica actividades bajo el 60 % de logro, calcula tendencia y presenta hallazgos y advertencias. |
| TT-08 | Las pruebas de indicadores y alertas se ejecutan en CI y pasan en verde. |
| TT-02.3 | El prototipo de chat e informe queda documentado y visible como vista previa en el frontend. |

---

## 9. Demostración del incremento

1. Iniciar sesión como docente y abrir **Actividades**. La barra superior muestra la fuente: *Moodle · fecha de sincronización*.
2. Seleccionar una actividad y, en el panel derecho, revisar los estados (`calificada`, `nota_cero`, `sin_entrega`). Pulsar **Calificar en Moodle**, cambiar una nota allí y comprobar el cambio en EduMonitor tras la sincronización automática (o con *Sincronizar ahora* del administrador, si se quiere al instante).
3. Abrir **Resumen**: verificar promedio con escala, distribución, parciales (botón +) y el aviso **Datos incompletos**.
4. En **Estudiantes**, abrir la ficha de un estudiante con descenso: gráfico de evolución con ejes y umbral, componentes y alertas.
5. Abrir **Alertas**: comprobar que cada tarjeta muestra la evidencia y la regla aplicada.
6. Abrir **Análisis grupal**: revisar actividades bajo el umbral y tendencia general.
7. En el selector de asignatura del menú lateral, cambiar el **período** y buscar por código; en **Estudiantes**, filtrar **Con alertas**.
8. Iniciar sesión como administrador, abrir **Reglas de alerta**, desactivar la regla de descenso y comprobar que la alerta desaparece.
9. Como administrador, en **Docentes**, crear la cuenta `luis.martinez@uta.edu.ec`. Luego, en **Moodle → Sincronizar ahora**, ver que *Sistemas Operativos* deja de omitirse.
10. Verificar con `curl` o Postman que un docente recibe `403` al consultar un curso ajeno y que el administrador también recibe `403` en los indicadores de un curso.

---

## 10. Riesgos y mitigaciones

| Riesgo | Mitigación |
| --- | --- |
| Cálculos ambiguos por componentes incompletos. | La nota final queda en “pendiente” y el dashboard muestra advertencias explícitas. |
| Alertas interpretadas como diagnósticos. | Evidencia numérica obligatoria y mensajes que aclaran que son señales de seguimiento. |
| Fuga de datos entre docentes. | Verificación de propiedad del curso en todos los endpoints y pruebas de integración `403`. |
| Reglas mal configuradas. | Validación de JSON y rangos en el backend; solo el administrador puede modificarlas. |
| Datos de demostración insuficientes. | Semilla con patrones críticos y scripts SQL reproducibles. |
| Deuda de documentación. | OpenAPI, README, plan del sprint y scripts SQL actualizados en el mismo sprint. |

---

## 11. Definición de Hecho (DoD) del sprint

- [x] Historias HU-05 a HU-11 implementadas en backend y frontend.
- [x] Permisos por curso verificados con pruebas.
- [x] Cálculos deterministas cubiertos por pruebas unitarias.
- [x] Endpoints cubiertos por pruebas de integración.
- [x] `dotnet build` y `dotnet test` en verde.
- [x] `npm run build` en verde.
- [x] Scripts SQL del sprint disponibles.
- [x] `docs/openapi.yaml` actualizado.
- [x] README con instrucciones y alcance del sprint.
- [x] Demostración preparada para la Sprint Review del 12 de noviembre de 2026.

---

## 12. Retrospectiva prevista

**¿Qué funcionó?**

- Separar la analítica en un servicio puro permitió probar los cálculos sin base de datos ni IA.
- La autorización centralizada evitó duplicar lógica de permisos en cada controlador.
- Los datos semilla con casos críticos facilitaron la validación visual y las pruebas.
- Revisar el alcance a mitad del sprint (refinamientos del 10-nov) evitó construir funciones que duplicaban Moodle.

**¿Qué mejorar?**

- Añadir pruebas de componentes React cuando se incorpore Vitest.
- Extraer componentes de tabla/indicadores reutilizables para el Sprint 3.
- Automatizar la validación del contrato OpenAPI contra la API.

**Acciones para el Sprint 3:**

- Reutilizar `AnaliticaService` como fuente de las herramientas del agente.
- Exponer el resumen y las alertas al servicio Python mediante herramientas controladas.
- Incorporar el informe descargable con fecha de corte y trazabilidad.
