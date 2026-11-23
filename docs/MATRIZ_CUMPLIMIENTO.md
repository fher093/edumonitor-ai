# Matriz de cumplimiento RF / RNF (TT-12)

**Proyecto:** EduMonitor AI · **Corte:** cierre del Sprint 3 (26-nov-2026) · **Pruebas automáticas:** 157 en verde (`dotnet test EduMonitor.slnx`: 88 backend, 48 agente, 21 MoodleService).

Leyenda: ✅ cumplido y verificado · ⚠️ cumplido con limitación documentada.

---

## 1. Requisitos funcionales

| Código | Requisito | Estado | HU / TT | Evidencia en el código | Verificación |
| --- | --- | --- | --- | --- | --- |
| RF-01 | Autenticación y autorización por rol | ✅ | HU-01, HU-06 | `backend/Services/AuthService.cs`, `TokenService.cs`, `ControladorAcademicoBase.cs`; el AIService valida el mismo JWT | `AuthEndpointsTests`, `AuthServiceTests`, `Sprint2EndpointsTests` (403/401), `ChatEndpointTests` (401/403) |
| RF-02 | Conexión con Moodle (cursos del docente) | ✅ | HU-02, TT-05 | `MoodleService/`, `UsuariosController.cs` (cuentas que la sincronización empareja por correo) | `MoodleSincronizacionTests`, `UsuariosEndpointsTests` |
| RF-03 | Estudiantes, matrículas, actividades y calificaciones (sincronizados desde Moodle) | ✅ | HU-03, HU-05 | `SincronizacionMoodleService.cs`, `MoodleImportador.cs`, `SincronizacionAutomaticaWorker.cs`, `ActividadesController.cs` | `MoodleSincronizacionTests`, `MapeoUtaTests`, `Sprint2EndpointsTests` |
| RF-04 | Selección, búsqueda y filtros de cursos | ✅ | HU-04, HU-09 | `CursosController.cs` (`?periodo=&q=`), `frontend/src/components/CursoSwitcher.jsx` | `CursosEndpointsTests`, `Sprint2EndpointsTests` |
| RF-05 | Panel de indicadores | ✅ | HU-07 | `AnaliticaService.CalcularDashboard`, `pages/Resumen.jsx` | `AnaliticaServiceTests` |
| RF-06 | Seguimiento individual | ✅ | HU-08 | `AnaliticaService.CalcularFicha`, `pages/Estudiantes.jsx` | `AnaliticaServiceTests`, `Sprint2EndpointsTests` |
| RF-07 | Alertas configurables con evidencia | ✅ | HU-10 | `AnaliticaService.CalcularAlertas`, `AlertasController.cs`, `pages/Alertas.jsx`, `pages/AdminReglas.jsx` | `AnaliticaServiceTests`, `Sprint2EndpointsTests` |
| RF-08 | Análisis grupal | ✅ | HU-11 | `AnaliticaService.CalcularAnalisis`, `pages/Analisis.jsx` | `AnaliticaServiceTests` |
| RF-09 | Agente de IA con consultas controladas y herramientas | ⚠️ | HU-12, HU-13, HU-14, TT-09, TT-11 | `AIService/Agente/AgenteAcademico.cs`, `Herramientas.cs`, `VerificadorHechos.cs`, `pages/Agente.jsx` | `AgenteTests`, `CompatibilidadModeloTests`, `ContratosHerramientasTests`, `docs/EVALUACION_AGENTE.md`. Limitación: errores de relación entre cifras correctas (sección 2.4 de la evaluación). |
| RF-10 | Recomendaciones justificadas | ✅ | HU-15 | `AnaliticaService.CalcularRecomendaciones`, `GET /api/analisis/curso/{id}/recomendaciones`; el agente las usa como base | `RecomendacionesTests` (TDD), `Sprint3EndpointsTests` |
| RF-11 | Informe consultable o descargable | ✅ | HU-16 | `InformesController.cs`, `pages/Informe.jsx` (Descargar PDF) | `Sprint3EndpointsTests` (secciones, fecha de corte única, permisos) |
| RF-12 | Auditoría de cambios, consultas y herramientas de IA | ✅ | HU-17 | `AuditarAttribute.cs`, `AuditoriaService.cs`, `AuditoriaController.cs`, `pages/AdminAuditoria.jsx`, `docs/sql/07-auditoria.sql` | `Sprint3EndpointsTests` (registro de consultas, agente, cambios y logins; sin secretos; paginación) |

## 2. Requisitos no funcionales

| Código | Requisito | Estado | Evidencia | Verificación |
| --- | --- | --- | --- | --- |
| RNF-01 | Seguridad: secretos en variables de entorno, control por curso, mínimo privilegio | ✅ | `.env.example`, `docker-compose.yml` (sin secretos en el repositorio); verificación de propiedad del curso en cada endpoint; el agente usa el JWT del docente; el administrador no ve notas. HTTPS: se delega al proxy del despliegue (fuera del MVP local). | Pruebas 401/403 en `Sprint2EndpointsTests`, `Sprint3EndpointsTests`, `ChatEndpointTests`, `AgenteTests` |
| RNF-02 | Privacidad: datos mínimos, IA local | ✅ | Ollama local (RN-11); las herramientas envían al modelo solo id y nombre del estudiante (sin cédula ni correo); la auditoría no guarda preguntas, notas ni secretos | `Las_herramientas_no_envian_cedula_ni_correo_al_modelo`, `Login_se_audita_sin_guardar_contrasenas_ni_tokens`, `Sanitizar_elimina_secretos_del_detalle` |
| RNF-03 | Confiabilidad: errores y datos ausentes | ✅ | “Datos insuficientes” en lugar de métricas engañosas (RN-08); el agente no responde sin datos; Ollama caído → 503 con mensaje; JSON inválido → 400 | `Curso_sin_calificaciones_responde_datos_insuficientes`, `Backend_caido_avisa_el_error_sin_responder_de_memoria`, `Modelo_caido_devuelve_503_con_mensaje_claro`, `Cuerpo_que_no_es_utf8_devuelve_400_y_no_500` |
| RNF-04 | Usabilidad | ✅ | Escalas explícitas, filtros, mensajes de error, estado del modelo visible en el chat, sugerencias de preguntas | Revisión en la Sprint Review |
| RNF-05 | Mantenibilidad | ✅ | Ramas por funcionalidad y PR, Docker Compose, CI (`.github/workflows/ci.yml`), README, OpenAPI, scripts SQL 01–07, planificaciones de los 3 sprints | CI en verde |
| RNF-06 | Calidad: pruebas unitarias, de integración y reproducibilidad | ✅ | 157 pruebas xUnit; evaluación del agente repetible con `docs/evaluacion/evaluar-agente.mjs` | `dotnet test EduMonitor.slnx` |
| RNF-07 | Rendimiento: consultas paginadas y tiempos medidos | ⚠️ | Auditoría paginada (máx. 200 por página) e indexada; tiempos de la API medidos en prueba (< 500 ms de media). El agente responde en 13–19 s con el modelo local cargado en una GPU de 6 GB, tras la optimización (`EVALUACION_AGENTE.md` §2.5). | `Indicadores_alertas_e_informe_responden_en_tiempo_acotado`, `Auditoria_esta_paginada_y_ordenada_de_la_mas_reciente` |
| RNF-08 | Transparencia: fuente, fecha e incertidumbre | ✅ | Fuente y fecha de sincronización en la barra superior y en el informe; alertas con evidencia; el chat muestra fuentes, fecha de corte, advertencias y traza de herramientas; descarte de afirmaciones no verificables | `Hecho_con_cifra_inventada_se_descarta`, `Informe_incluye_encabezado_fecha_de_corte_y_todas_las_secciones` |

## 3. Reglas de negocio

| Regla | Dónde se aplica | Prueba |
| --- | --- | --- |
| RN-01 Escala explícita | Dashboard, informe, herramientas (`escala_promedio`) | `AnaliticaServiceTests` |
| RN-02 Estados de actividad | `EstadosActividad.Resolver` | `AnaliticaServiceTests` |
| RN-03 Cálculo determinista | `AnaliticaService` (indicadores, alertas, recomendaciones); el agente no calcula | `RecomendacionesTests` |
| RN-04 a RN-06 Alertas | `AnaliticaService.CalcularAlertas`, reglas configurables | `AnaliticaServiceTests`, `Sprint2EndpointsTests` |
| RN-07 Evidencia, no diagnóstico | Alertas, chat e informe | Revisión de pantallas |
| RN-08 Datos insuficientes | Dashboard, recomendaciones, herramientas del agente | `Sin_calificaciones_advierte_datos_insuficientes` |
| RN-09 El agente solo usa datos de herramientas | Consulta mínima, verificador de cifras, eliminación de fuentes no consultadas | `AgenteTests` |
| RN-10 Sin mensajes automáticos | El agente solo sugiere; instrucción explícita sin terceros | Revisión de prompts |
| RN-11 IA local | Ollama en el equipo; datos mínimos | `ChatEndpointTests`, configuración |
| RN-12 Mínimo privilegio | `VerificarAccesoCursoAsync`; el agente hereda el JWT del docente | `Sprint2EndpointsTests`, `Sprint3EndpointsTests`, `Curso_ajeno_responde_sin_acceso_y_no_inventa_datos` |

## 4. Pendientes al cierre

| Pendiente | Motivo | Plan |
| --- | --- | --- |
| Comparación con `qwen2.5:7b` | La descarga del modelo se interrumpió por falta de memoria | `ollama pull qwen2.5:7b` y ejecutar `evaluar-agente.mjs` (5 minutos) |
| Verificación de relaciones entre cifras en el agente | Limitación del verificador basado en cifras | Trabajo futuro (EVALUACION_AGENTE.md §2.4) |
| Pruebas de componentes React (Vitest) | Fuera del alcance del MVP | Trabajo futuro |
