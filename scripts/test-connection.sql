-- Test SQL File für VS Code MSSQL Extension
-- Öffne diese Datei in VS Code nach dem Neustart
-- Die Extension sollte automatisch aktiviert werden

-- Test Query
SELECT 
    'Connection successful!' AS Status,
    DB_NAME() AS DatabaseName,
    @@VERSION AS SQLVersion;

-- Zeige Tabellen
SELECT name 
FROM sys.tables 
WHERE name IN ('TransportData', 'ProcessLogs')
ORDER BY name;

-- Zeige Daten (falls vorhanden)
SELECT TOP 10 * FROM TransportData;
SELECT TOP 10 * FROM ProcessLogs ORDER BY Timestamp DESC;


