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
    
    // Check if table exists before trying to drop it
    const tableExistsQuery = `
      SELECT COUNT(*) AS TableCount
      FROM sys.tables
      WHERE name = 'TransportData'
    `;
    
    const tableExistsResult = await pool.request().query(tableExistsQuery);
    const tableExists = tableExistsResult.recordset[0].TableCount > 0;
    
    if (!tableExists) {
      await pool.close();
      return res.status(200).json({ message: 'TransportData table does not exist. Nothing to drop.' });
    }
    
    // Drop the table
    await pool.request().query('DROP TABLE [dbo].[TransportData]');
    
    // Log the action
    await pool.request()
      .input('level', sql.VarChar, 'info')
      .input('message', sql.VarChar, 'TransportData table dropped by user')
      .input('details', sql.VarChar, 'TransportData table dropped to allow recreation with new structure (PrimaryKey instead of Id)')
      .query(`
        INSERT INTO ProcessLogs (datetime_created, level, message, details)
        VALUES (GETUTCDATE(), @level, @message, @details)
      `);
    
    await pool.close();
    
    res.status(200).json({ message: 'Zieltabelle TransportData wurde erfolgreich gelöscht. Die Tabelle wird beim nächsten CSV-Upload automatisch neu erstellt.' });
  } catch (error) {
    console.error('Error dropping table:', error);
    
    // Provide more detailed error information
    let errorMessage = getSqlErrorMessage(error);
    if (error.code === 'EREQUEST') {
      errorMessage = 'SQL query failed. Check if TransportData table exists.';
    }
    
    res.status(500).json({ 
      error: 'Failed to drop table', 
      details: errorMessage,
      code: error.code,
      message: 'Please check Azure SQL configuration'
    });
  }
};

