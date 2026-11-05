# PLANIFICACIÓN DEL SPRINT 2

**UNIVERSIDAD TÉCNICA DE AMBATO**

**FACULTAD DE INGENIERÍA EN SISTEMAS ELECTRÓNICA E INDUSTRIAL — CARRERA DE SOFTWARE**

**Proyecto:** EduMonitor AI — Sistema inteligente de seguimiento y análisis del rendimiento académico con módulo académico propio.

**Asignatura:** Metodologías Ágiles · **Docente:** Ing. Hernán Naranjo, Mgs.

**Integrantes:** Fernando Llerena, Daky Fueres, Joel Poaquiza, Monserrath Medina.

**Sprint:** 2 · **Fechas:** 30 de octubre al 12 de noviembre de 2026 · **Duración:** 8 días hábiles.

---

## 1. Objetivo del sprint

Construir el núcleo académico y analítico del MVP: registrar actividades y calificaciones con las reglas UTA, restringir el acceso a los cursos propios del docente, calcular indicadores deterministas del curso y del estudiante, generar alertas configurables con evidencia y producir un análisis grupal que permita detectar dificultades antes del cierre del período.

**Resultado esperado (incremento):** un docente autenticado puede seleccionar un período y un curso, crear actividades, calificar a sus estudiantes, revisar el dashboard, abrir la ficha individual, consultar alertas justificadas y analizar el rendimiento del grupo; el administrador técnico puede auditar y configurar los umbrales de las alertas.

---

## 2. Alcance comprometido

### 2.1 Historias de usuario

| ID | Historia | Requisito | PH |
| --- | --- | --- | --- |
| HU-05 | Crear actividades (fecha de entrega y puntaje máximo) y registrar calificaciones y estados por estudiante. | RF-03, RNF-03 | 8 |
| HU-06 | Ver únicamente los cursos y estudiantes autorizados del docente autenticado. | RF-01, RNF-01 | 5 |
| HU-07 | Panel con promedio (escala explícita), distribución, actividades evaluadas y progreso. | RF-05 | 5 |
| HU-08 | Ficha individual con evolución de notas y estados de actividad. | RF-06 | 5 |
| HU-09 | Seleccionar período y buscar/filtrar estudiantes y asignaturas. | RF-04, RNF-04 | 3 |
| HU-10 | Alertas con reglas configurables y evidencia verificable. | RF-07, RNF-08 | 8 |
| HU-11 | Análisis grupal con actividades de resultados bajos y tendencias. | RF-08 | 5 |

**Total: 39 PH.**

### 2.2 Tareas técnicas del sprint

| ID | Tarea | Entregable |
| --- | --- | --- |
| TT-08 | Pruebas unitarias de indicadores y reglas de alerta (TDD). | `backend.Tests/AnaliticaServiceTests.cs` y `Sprint2EndpointsTests.cs` |
| TT-02.3 | Prototipo de baja fidelidad del chat e informe. | Documentado en `DESIGN.md` y en el módulo de vista previa del frontend |
| TT-03.5 | Scripts SQL de actividades, calificaciones y reglas. | `docs/sql/04-actividades-calificaciones.sql`, `docs/sql/05-reglas-alerta.sql` |
| TT-04.4 | Actualización de contratos OpenAPI. | `docs/openapi.yaml` |

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
- Administrador técnico: acceso a todos los cursos.
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
| `backend/Controllers/ActividadesController.cs` | CRUD de actividades y finalización. |
| `backend/Controllers/CalificacionesController.cs` | Consulta y registro masivo de notas/estados. |
| `backend/Controllers/IndicadoresController.cs` | Dashboard y ficha individual. |
| `backend/Controllers/AlertasController.cs` | Alertas y reglas configurables. |
| `backend/Controllers/AnalisisController.cs` | Análisis grupal. |
| `backend/Controllers/CursosController.cs` | Filtros por período, búsqueda y matrículas. |
| `backend/Data/DbInitializer.cs` | DDL, reglas por defecto y datos de demostración. |

### 4.2 Frontend (React + Vite)

| Archivo | Responsabilidad |
| --- | --- |
| `frontend/src/hooks/useCursos.js` | Cursos autorizados, filtros de período/búsqueda y curso activo. |
| `frontend/src/components/SelectorCurso.jsx` | Barra contextual compartida. |
| `frontend/src/pages/Actividades.jsx` | CRUD de actividades y registro de calificaciones. |
| `frontend/src/pages/Dashboard.jsx` | Indicadores, distribución, parciales y seguimiento. |
| `frontend/src/pages/FichaEstudiante.jsx` | Evolución, componentes, actividades y alertas del estudiante. |
| `frontend/src/pages/Alertas.jsx` | Alertas con evidencia y reglas aplicadas. |
| `frontend/src/pages/Analisis.jsx` | Hallazgos, tendencias y detalle por actividad. |
| `frontend/src/pages/AdminReglas.jsx` | Configuración de umbrales (solo administrador). |
| `frontend/src/utils/formato.js` | Formatos y etiquetas compartidas. |
| `frontend/src/sprint2.css` | Estilos del incremento sobre los tokens de `DESIGN.md`. |

---

## 5. Contratos de API del sprint

| Método | Ruta | Rol | Descripción |
| --- | --- | --- | --- |
| GET | `/api/cursos?periodo=&q=` | Docente / Admin | Cursos filtrados por período y texto. |
| GET | `/api/cursos/periodos` | Docente / Admin | Períodos disponibles del usuario. |
| GET | `/api/cursos/{cursoId}/matriculas?q=&estado=` | Docente / Admin | Nómina filtrable. |
| GET | `/api/cursos/{cursoId}/actividades` | Docente / Admin | Actividades del curso. |
| POST | `/api/cursos/{cursoId}/actividades` | Docente / Admin | Crear actividad. |
| GET | `/api/actividades/{id}` | Docente / Admin | Detalle de actividad. |
| PUT | `/api/actividades/{id}` | Docente / Admin | Editar actividad. |
| DELETE | `/api/actividades/{id}` | Docente / Admin | Eliminar actividad y sus notas. |
| GET | `/api/actividades/{id}/calificaciones` | Docente / Admin | Nómina con estado efectivo. |
| POST | `/api/actividades/{id}/calificaciones` | Docente / Admin | Upsert masivo de notas/estados. |
| GET | `/api/cursos/{cursoId}/finalizacion` | Docente / Admin | Finalización por estudiante. |
| GET | `/api/indicadores/curso/{cursoId}` | Docente / Admin | Dashboard del curso. |
| GET | `/api/indicadores/curso/{cursoId}/estudiantes/{estudianteId}` | Docente / Admin | Ficha individual. |
| GET | `/api/alertas/curso/{cursoId}` | Docente / Admin | Alertas con evidencia. |
| GET | `/api/alertas/reglas` | Docente / Admin | Reglas configuradas. |
| PUT | `/api/alertas/reglas/{id}` | Admin | Actualizar parámetros/estado. |
| GET | `/api/analisis/curso/{cursoId}` | Docente / Admin | Análisis grupal. |

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

- CRUD completo de actividades y validación de componente.
- Validación de nota, estado, matrícula y duplicados al calificar.
- Normalización de nota 0 a `nota_cero`.
- Acceso 403 a cursos de otro docente y 200 para el administrador.
- Dashboard y ficha con datos reales.
- Alertas con evidencia y reglas configurables.
- Solo el administrador puede editar reglas; validación de JSON y parámetros.
- Filtros de período y búsqueda.
- Finalización por estudiante.
- `401` sin token.

**Resultado:** 54 pruebas aprobadas (`dotnet test EduMonitor.slnx`), 0 errores.

### 6.3 Datos de prueba

`DbInitializer` siembra un curso con 7 actividades (tres componentes en ambos parciales) y patrones que garantizan:

- estudiante de alto rendimiento;
- estudiante con descenso sostenido;
- estudiante con atrasos reiterados;
- estudiante con nota cero y estados pendientes;
- estudiante con rendimiento bajo general.

Los scripts `docs/sql/04` y `docs/sql/05` reproducen el esquema y los datos en SQL Server 2022 de forma idempotente.

---

## 7. Plan de trabajo

| Día | Actividad | Responsable |
| --- | --- | --- |
| 30 oct | Sprint Planning, modelo de actividades/calificaciones y contratos. | Todo el equipo |
| 30–31 oct | Entidades, DbContext, DDL y datos semilla. | Monserrath Medina / Fernando Llerena |
| 2–3 nov | `AnaliticaService` y pruebas TDD de indicadores y alertas. | Joel Poaquiza |
| 4–5 nov | Endpoints de actividades, calificaciones y permisos por curso. | Fernando Llerena |
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
| HU-05 | El docente crea, edita y elimina actividades de sus cursos; registra notas por estudiante; el sistema rechaza notas fuera de rango, estados inválidos y estudiantes no matriculados. |
| HU-06 | Un docente no puede leer ni modificar actividades, calificaciones, indicadores, alertas o análisis de cursos ajenos (`403`); el administrador sí puede. |
| HU-07 | El dashboard muestra promedio con escala explícita, distribución por rangos, actividades evaluadas, progreso y advertencias cuando faltan datos. |
| HU-08 | La ficha muestra promedio, parciales, nota final (solo si está completa), componentes, evolución, estados y alertas del estudiante. |
| HU-09 | El período y la búsqueda filtran cursos y matrículas sin exponer cursos no autorizados. |
| HU-10 | Cada alerta incluye regla, severidad y evidencia; el administrador puede ajustar `k`, `Δ`, `n` y `p`, y desactivar reglas. |
| HU-11 | El análisis grupal identifica actividades bajo el 60 % de logro, calcula tendencia y presenta hallazgos y advertencias. |
| TT-08 | Las pruebas de indicadores y alertas se ejecutan en CI y pasan en verde. |
| TT-02.3 | El prototipo de chat e informe queda documentado y visible como vista previa en el frontend. |

---

## 9. Demostración del incremento

1. Iniciar sesión como docente y abrir **Actividades**.
2. Crear una actividad del parcial 2 y calificar a un estudiante con nota 0 y a otro como `sin_entrega`.
3. Abrir **Dashboard**: verificar promedio, distribución, parciales y advertencias.
4. Abrir la **ficha** de un estudiante con descenso y revisar su evolución y alertas.
5. Abrir **Alertas**: comprobar que cada tarjeta muestra la evidencia y la regla aplicada.
6. Abrir **Análisis grupal**: revisar actividades bajo el umbral y tendencia general.
7. Cambiar el **período** y buscar una asignatura por código.
8. Iniciar sesión como administrador, abrir **Reglas de alerta**, desactivar la regla de descenso y comprobar que la alerta desaparece.
9. Verificar con `curl` o Postman que un docente recibe `403` al consultar un curso ajeno.

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

**¿Qué mejorar?**

- Añadir pruebas de componentes React cuando se incorpore Vitest.
- Extraer componentes de tabla/indicadores reutilizables para el Sprint 3.
- Automatizar la validación del contrato OpenAPI contra la API.

**Acciones para el Sprint 3:**

- Reutilizar `AnaliticaService` como fuente de las herramientas del agente.
- Exponer el resumen y las alertas al servicio Python mediante herramientas controladas.
- Incorporar el informe descargable con fecha de corte y trazabilidad.
