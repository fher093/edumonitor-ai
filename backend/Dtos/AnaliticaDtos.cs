namespace Backend.Dtos;

public record DistribucionNotaDto(
    string Rango,
    string Etiqueta,
    int Cantidad,
    decimal Porcentaje);

public record ProgresoCursoDto(
    int TotalEsperado,
    int Completadas,
    int Calificadas,
    int NotaCero,
    int SinEntrega,
    int Pendientes,
    int ActividadesVencidas,
    decimal? PorcentajeAvance,
    decimal? PorcentajeFinalizacionVencidas);

public record ResumenComponenteDto(
    string Componente,
    string Etiqueta,
    string EtiquetaCorta,
    int Parcial,
    decimal Ponderacion,
    int Actividades,
    int Calificadas,
    decimal? PromedioSobre10);

public record ResumenParcialDto(
    int Parcial,
    decimal? PromedioSobre10,
    int EstudiantesConNota,
    int EstudiantesIncompletos,
    bool Completo);

public record EstudianteResumenDto(
    int EstudianteId,
    string Identificacion,
    string Nombre,
    string Correo,
    decimal? PromedioActividades,
    decimal? NotaParcial1,
    decimal? NotaParcial2,
    decimal? NotaFinal,
    int Calificadas,
    int NotaCero,
    int SinEntrega,
    int Pendientes,
    int CompletadasSinNota,
    decimal? PorcentajeFinalizacion,
    int Alertas);

public record DashboardCursoDto(
    int CursoId,
    string CursoNombre,
    string CursoCodigo,
    string Periodo,
    decimal EscalaCalificacion,
    decimal EscalaNotaFinal,
    DateTime FechaCalculo,
    int TotalMatriculados,
    int ActividadesTotales,
    int ActividadesEvaluadas,
    int EstudiantesConNota,
    decimal? PromedioCurso,
    decimal? DesviacionEstandar,
    int AprobadosProyectados,
    int EstudiantesEnRiesgo,
    List<DistribucionNotaDto> Distribucion,
    ProgresoCursoDto Progreso,
    List<ResumenComponenteDto> Componentes,
    List<ResumenParcialDto> Parciales,
    List<EstudianteResumenDto> Estudiantes,
    List<string> Advertencias);

public record ActividadEstudianteDto(
    int ActividadId,
    string Nombre,
    string Tipo,
    string Componente,
    string ComponenteEtiqueta,
    string ComponenteEtiquetaCorta,
    int Parcial,
    DateTime? FechaEntrega,
    decimal PuntajeMaximo,
    decimal? Nota,
    decimal? Porcentaje,
    string Estado,
    string EstadoEtiqueta,
    string? Observacion,
    bool Vencida);

public record EvolucionNotaDto(
    int ActividadId,
    string Etiqueta,
    DateTime? Fecha,
    decimal? Nota,
    decimal? NotaSobre10,
    string Estado);

public record ComponenteEstudianteDto(
    string Componente,
    string Etiqueta,
    string EtiquetaCorta,
    int Parcial,
    decimal Ponderacion,
    int ActividadesCalificadas,
    decimal? PromedioSobre10,
    bool TieneDatos);

public record FichaEstudianteDto(
    int CursoId,
    string CursoNombre,
    string CursoCodigo,
    string Periodo,
    decimal EscalaCalificacion,
    int EstudianteId,
    string Identificacion,
    string Nombre,
    string Correo,
    decimal? PromedioActividades,
    decimal? NotaParcial1,
    decimal? NotaParcial2,
    decimal? NotaFinal,
    bool Parcial1Completo,
    bool Parcial2Completo,
    int ActividadesTotales,
    int Calificadas,
    int NotaCero,
    int SinEntrega,
    int Pendientes,
    int Completadas,
    decimal? PorcentajeFinalizacion,
    List<ComponenteEstudianteDto> Componentes,
    List<ActividadEstudianteDto> Actividades,
    List<EvolucionNotaDto> Evolucion,
    List<AlertaDto> Alertas);

public record ActividadAnalisisDto(
    int ActividadId,
    string Nombre,
    string Componente,
    string ComponenteEtiquetaCorta,
    int Parcial,
    DateTime? FechaEntrega,
    decimal PuntajeMaximo,
    int Estudiantes,
    int Calificados,
    decimal? PromedioNota,
    decimal? PromedioPorcentaje,
    decimal? PorcentajeFinalizacion,
    bool ResultadoBajo);

public record TendenciaAnalisisDto(
    int ActividadId,
    string Etiqueta,
    DateTime? Fecha,
    decimal? PromedioPorcentaje,
    string? Tendencia,
    decimal? Variacion);

public record AnalisisGrupalDto(
    int CursoId,
    string CursoNombre,
    string CursoCodigo,
    string Periodo,
    decimal EscalaCalificacion,
    DateTime FechaCalculo,
    int TotalEstudiantes,
    int Actividades,
    int ActividadesBajas,
    int EstudiantesEnRiesgo,
    decimal? PromedioCurso,
    decimal? DesviacionEstandar,
    string TendenciaGeneral,
    decimal? VariacionTendencia,
    List<ActividadAnalisisDto> DetalleActividades,
    List<ResumenComponenteDto> Componentes,
    List<TendenciaAnalisisDto> Tendencias,
    List<string> Hallazgos,
    List<string> Advertencias);
