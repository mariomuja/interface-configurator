const { getSqlConfig, validateSqlConfig, getSqlErrorMessage, sql } = require('./sql-config');

module.exports = async (req, res) => {
  try {
    const config = await getSqlConfig();
    
    // Validate configuration
    const validation = validateSqlConfig(config);
    if (!validation.isValid) {
      console.error('Missing SQL configuration:', validation.missing);
      return res.status(500).json(validation.error);
    }
    
    const pool = await sql.connect(config);
    
    // First, check if TransportData table exists
    const tableExistsQuery = `
      SELECT COUNT(*) AS TableCount
      FROM sys.tables
      WHERE name = 'TransportData'
    `;
    
    const tableExistsResult = await pool.request().query(tableExistsQuery);
    const tableExists = tableExistsResult.recordset[0].TableCount > 0;
    
    // If table doesn't exist, return empty array (table will be created when first CSV is processed)
    if (!tableExists) {
      await pool.close();
      return res.status(200).json([]);
    }
    
    // Get all column names from TransportData table (excluding PrimaryKey, datetime_created, and reserved columns)
    const columnQuery = `
      SELECT c.name AS ColumnName
      FROM sys.columns c
      INNER JOIN sys.tables t ON c.object_id = t.object_id
      WHERE t.name = 'TransportData'
      AND c.name NOT IN ('PrimaryKey', 'Id', 'datetime_created', 'CsvDataJson')
      ORDER BY c.column_id
    `;
    
    const columnResult = await pool.request().query(columnQuery);
    const csvColumns = columnResult.recordset.map(r => r.ColumnName);
    
    // Build dynamic SELECT query - use individual columns (PrimaryKey is the primary key column)
    let selectColumns = 'PrimaryKey';
    let hasCsvDataJson = false;
    
    // Filter out reserved columns from csvColumns
    const reservedColumns = ['PrimaryKey', 'Id', 'datetime_created', 'CsvDataJson'];
    const filteredCsvColumns = csvColumns.filter(c => !reservedColumns.includes(c));
    
    if (filteredCsvColumns.length > 0) {
      // New approach: Use individual columns
      selectColumns = 'PrimaryKey, ' + filteredCsvColumns.join(', ');
    } else {
      // No CSV columns found - just select PrimaryKey and datetime_created
      selectColumns = 'PrimaryKey';
    }
    
    // Every SQL table has datetime_created column with DEFAULT GETUTCDATE()
    const query = `
      SELECT ${selectColumns}, 
             FORMAT(datetime_created, 'yyyy-MM-dd HH:mm:ss') as datetime_created
      FROM TransportData
      ORDER BY datetime_created DESC
    `;
    
    const result = await pool.request().query(query);
    await pool.close();
    
    // Parse results - use individual columns (PrimaryKey is the primary key)
    const records = result.recordset.map(row => {
      const record = {
        id: row.PrimaryKey || row.Id, // Use PrimaryKey, fallback to Id for backward compatibility
        datetime_created: row.datetime_created,
        createdAt: row.datetime_created // Backward compatibility
      };
      
      // Add all CSV columns (filteredCsvColumns contains only actual CSV columns)
      filteredCsvColumns.forEach(col => {
        if (row[col] !== undefined && row[col] !== null) {
          record[col] = row[col];
        }
      });
      
      return record;
    });
    
    res.status(200).json(records);
  } catch (error) {
    console.error('Error fetching SQL data:', error);
    console.error('Error details:', {
      code: error.code,
      message: error.message,
      name: error.name,
      stack: error.stack?.substring(0, 200)
    });
    
    // Check if environment variables are missing first
    const config = await getSqlConfig();
    const validation = validateSqlConfig(config);
    if (!validation.isValid) {
      return res.status(500).json({ 
        ...validation.error,
        code: 'ENV_MISSING'
      });
    }
    
    // Check if error is due to missing table or connection issues
    const errorMessage = error.message || '';
    const isTableMissing = error.code === 'EREQUEST' && (
      errorMessage.includes('Invalid object name') ||
      errorMessage.includes("doesn't exist") ||
      errorMessage.includes('does not exist')
    );
    
    if (isTableMissing) {
      // Table doesn't exist yet - return empty array with informative message
      // The table will be created automatically when the first CSV is processed
      return res.status(200).json([]);
    }
    
    // Check for connection errors
    const isConnectionError = error.code === 'ETIMEOUT' || 
                              error.code === 'ESOCKET' || 
                              error.code === 'ECONNREFUSED' ||
                              error.code === 'ELOGIN';
    
    if (isConnectionError) {
      return res.status(500).json({ 
        error: 'Database connection failed', 
        details: getSqlErrorMessage(error),
        code: error.code,
        message: 'Cannot connect to Azure SQL Server. Please check firewall rules, credentials, and connection string.'
      });
    }
    
    // Provide more detailed error information for other errors
    const detailedErrorMessage = getSqlErrorMessage(error);
    
    res.status(500).json({ 
      error: 'Failed to fetch SQL data', 
      details: detailedErrorMessage,
      code: error.code,
      message: 'An error occurred while querying the database. Please check Azure SQL configuration.'
    });
  }
};



