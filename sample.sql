-- Sample script for the Open .sql button. Read-only, so it is safe on a
-- production connection too. Three batches, to show GO being honoured.

SELECT
    DB_NAME()        AS [database],
    SUSER_SNAME()    AS login_name,
    @@VERSION        AS server_version;
GO

SELECT TOP 10
    s.name + '.' + t.name AS table_name,
    p.rows                AS row_count,
    t.create_date         AS created
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
ORDER BY p.rows DESC;
GO

SELECT type_desc, COUNT(*) AS objects
FROM sys.objects
WHERE is_ms_shipped = 0
GROUP BY type_desc
ORDER BY objects DESC;
