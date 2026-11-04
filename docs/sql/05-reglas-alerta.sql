/* =====================================================================
   EduMonitor AI - 05: Reglas de Alerta Configurables (HU-10, RN-04 a RN-06)
   Script IDEMPOTENTE para SQL Server 2022.
   Las reglas son deterministas: la API evalúa estos umbrales y devuelve la
   evidencia numérica que activó cada alerta (sin etiquetas definitivas).
   ===================================================================== */
USE EduMonitor;
GO

-- 1. Tabla de reglas (una fila por tipo de alerta)
IF OBJECT_ID(N'dbo.ReglasAlerta', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReglasAlerta (
        Id                 INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReglasAlerta PRIMARY KEY,
        Tipo               NVARCHAR(40)      NOT NULL,
        Nombre             NVARCHAR(150)     NOT NULL,
        ParametrosJson     NVARCHAR(400)     NOT NULL CONSTRAINT DF_ReglasAlerta_Parametros DEFAULT (N'{}'),
        Activa             BIT               NOT NULL CONSTRAINT DF_ReglasAlerta_Activa DEFAULT (1),
        FechaActualizacion DATETIME2         NOT NULL CONSTRAINT DF_ReglasAlerta_FActual DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT CK_ReglasAlerta_Tipo CHECK (
            Tipo IN (N'descenso', N'atraso', N'baja_finalizacion'))
    );
    CREATE UNIQUE INDEX IX_ReglasAlerta_Tipo ON dbo.ReglasAlerta (Tipo);
END
GO

-- 2. Semilla idempotente de las tres reglas del Sprint 2.
--    k     -> ventana de evaluaciones para detectar el descenso (RN-04).
--    delta -> caída mínima en puntos sobre 10 (RN-04).
--    n     -> actividades vencidas sin entrega (RN-05).
--    p     -> porcentaje mínimo de finalización de actividades vencidas (RN-06).
MERGE dbo.ReglasAlerta AS destino
USING (VALUES
    (N'descenso',
     N'Descenso en las últimas evaluaciones (RN-04)',
     N'{"k":3,"delta":1.0}'),
    (N'atraso',
     N'Actividades vencidas sin entrega (RN-05)',
     N'{"n":2}'),
    (N'baja_finalizacion',
     N'Baja finalización de actividades (RN-06)',
     N'{"p":60}')
) AS origen (Tipo, Nombre, ParametrosJson)
ON destino.Tipo = origen.Tipo
WHEN MATCHED THEN
    UPDATE SET
        Nombre = origen.Nombre,
        ParametrosJson = CASE
            -- Conserva parámetros ajustados por el Administrador Técnico;
            -- solo restaura el valor sugerido si el JSON quedó vacío o inválido.
            WHEN destino.ParametrosJson IS NULL
                 OR LTRIM(RTRIM(destino.ParametrosJson)) IN (N'', N'{}')
            THEN origen.ParametrosJson
            ELSE destino.ParametrosJson
        END,
        FechaActualizacion = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Tipo, Nombre, ParametrosJson, Activa, FechaActualizacion)
    VALUES (origen.Tipo, origen.Nombre, origen.ParametrosJson, 1, SYSUTCDATETIME());
GO

-- 3. Verificación rápida de las reglas activas
SELECT Id, Tipo, Nombre, ParametrosJson, Activa, FechaActualizacion
FROM dbo.ReglasAlerta
ORDER BY Tipo;
GO
