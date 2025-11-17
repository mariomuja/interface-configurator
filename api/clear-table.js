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
    
    // Check if table exists before trying to clear it
    const tableExistsQuery = `
      SELECT COUNT(*) AS TableCount
      FROM sys.tables
      WHERE name = 'TransportData'
    `;
    
    const tableExistsResult = await pool.request().query(tableExistsQuery);
    const tableExists = tableExistsResult.recordset[0].TableCount > 0;
    
    if (!tableExists) {
      await pool.close();
      return res.status(200).json({ message: 'Table does not exist yet. Nothing to clear.' });
    }
    
    // Clear the table
    await pool.request().query('DELETE FROM TransportData');
    
    // Log the action
    await pool.request()
      .input('level', sql.VarChar, 'info')
      .input('message', sql.VarChar, 'Table cleared by user')
      .input('details', sql.VarChar, 'All data removed from TransportData table')
      .query(`
        INSERT INTO ProcessLogs (datetime_created, level, message, details)
        VALUES (GETUTCDATE(), @level, @message, @details)
      `);
    
    await pool.close();
    
    res.status(200).json({ message: 'Table cleared successfully' });
  } catch (error) {
    console.error('Error clearing table:', error);
    
    // Check if error is due to missing table
    if (error.code === 'EREQUEST' && error.message && error.message.includes('Invalid object name')) {
      return res.status(200).json({ message: 'Table does not exist yet. Nothing to clear.' });
    }
    
    // Provide more detailed error information
    let errorMessage = getSqlErrorMessage(error);
    if (error.code === 'EREQUEST') {
      errorMessage = 'SQL query failed. Check if TransportData table exists.';
    }
    
    res.status(500).json({ 
      error: 'Failed to clear table', 
      details: errorMessage,
      code: error.code,
      message: 'Please check Azure SQL configuration and ensure tables are initialized'
    });
  }
};



