/* =====================================================================
   EduMonitor AI - 07: Auditoría (HU-17 / RF-12)
   Script IDEMPOTENTE para SQL Server 2022.
   NOTA: el backend aplica este mismo DDL al arrancar (DbInitializer).
   Este script lo documenta y permite preparar una base existente a mano.
   Ejecutar después de 01 a 06.

   Qué se registra:
     - acceso    : inicios de sesión (éxito o fallo) con el correo usado.
     - consulta  : lecturas de datos académicos (indicadores, alertas, análisis, informe...).
     - cambio    : reglas de alerta, cuentas de docentes y sincronización con Moodle.
     - agente    : consultas que hace el agente de IA, con la herramienta usada
                   (cabecera X-Herramienta-Agente) y el id de la conversación.
   Qué NO se registra: contraseñas, tokens, notas, cuerpos de las peticiones
   ni el texto de las preguntas al agente (mínimo privilegio y privacidad, RNF-02).
   ===================================================================== */
USE EduMonitor;
GO

IF OBJECT_ID(N'dbo.RegistrosAuditoria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosAuditoria (
        Id            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistrosAuditoria PRIMARY KEY,
        Fecha         DATETIME2      NOT NULL,                -- UTC
        UsuarioId     INT            NULL,                    -- NULL en un login fallido de un correo inexistente
        UsuarioCorreo NVARCHAR(254)  NULL,
        Rol           NVARCHAR(40)   NULL,                    -- Docente | AdministradorTecnico
        Categoria     NVARCHAR(20)   NOT NULL,                -- acceso | consulta | cambio | agente
        Accion        NVARCHAR(80)   NOT NULL,                -- p. ej. auth.login, indicadores.dashboard, regla.editar
        Recurso       NVARCHAR(200)  NULL,                    -- solo ids de la ruta: curso/3/estudiante/7
        Detalle       NVARCHAR(500)  NULL,                    -- p. ej. herramienta=identificar_alertas; consulta=ab12...
        Resultado     NVARCHAR(20)   NOT NULL,                -- exito | denegado | fallido | no_encontrado | rechazado | error
        CodigoHttp    INT            NULL,
        Ip            NVARCHAR(45)   NULL,
        CONSTRAINT CK_RegistrosAuditoria_Categoria
            CHECK (Categoria IN (N'acceso', N'consulta', N'cambio', N'agente'))
    );
END;
GO

-- Consultas de la pantalla de auditoría: por fecha descendente y filtradas por categoría (RNF-07).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RegistrosAuditoria_Fecha')
    CREATE INDEX IX_RegistrosAuditoria_Fecha ON dbo.RegistrosAuditoria (Fecha);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RegistrosAuditoria_Categoria_Fecha')
    CREATE INDEX IX_RegistrosAuditoria_Categoria_Fecha ON dbo.RegistrosAuditoria (Categoria, Fecha);
GO

/* ---------------------------------------------------------------------
   Consultas útiles para la revisión
   --------------------------------------------------------------------- */

-- Herramientas usadas por el agente en los últimos 7 días, por docente.
SELECT UsuarioCorreo,
       SUBSTRING(Detalle, CHARINDEX(N'herramienta=', Detalle) + 12,
                 CHARINDEX(N';', Detalle + N';', CHARINDEX(N'herramienta=', Detalle)) - CHARINDEX(N'herramienta=', Detalle) - 12) AS Herramienta,
       COUNT(*) AS Usos
FROM dbo.RegistrosAuditoria
WHERE Categoria = N'agente' AND Fecha >= DATEADD(DAY, -7, SYSUTCDATETIME())
GROUP BY UsuarioCorreo,
         SUBSTRING(Detalle, CHARINDEX(N'herramienta=', Detalle) + 12,
                   CHARINDEX(N';', Detalle + N';', CHARINDEX(N'herramienta=', Detalle)) - CHARINDEX(N'herramienta=', Detalle) - 12)
ORDER BY Usos DESC;

-- Intentos de acceso a cursos ajenos (403) y logins fallidos.
SELECT TOP (50) Fecha, UsuarioCorreo, Categoria, Accion, Recurso, Resultado, CodigoHttp
FROM dbo.RegistrosAuditoria
WHERE Resultado IN (N'denegado', N'fallido')
ORDER BY Fecha DESC;
GO
