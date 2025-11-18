const { getSqlConfig, validateSqlConfig, sql } = require('./sql-config');
const fs = require('fs');
const path = require('path');

module.exports = async (req, res) => {
  try {
    const config = await getSqlConfig();
    
    // Validate configuration
    const validation = validateSqlConfig(config);
    if (!validation.isValid) {
      return res.status(500).json({
        ...validation.error,
        code: 'ENV_MISSING'
      });
    }
    
    const pool = await sql.connect(config);
    
    try {
      // Read SQL initialization script
      const sqlScriptPath = path.join(__dirname, '../terraform/init-database.sql');
      let sqlScript;
      
      try {
        sqlScript = fs.readFileSync(sqlScriptPath, 'utf8');
      } catch (fileError) {
        // If file doesn't exist, use inline SQL
        sqlScript = `
-- Create TransportData table with dynamic columns support
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TransportData]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[TransportData] (
        [PrimaryKey] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [datetime_created] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    
    CREATE INDEX [IX_TransportData_datetime_created] ON [dbo].[TransportData]([datetime_created]);
    
    PRINT 'TransportData table created successfully';
END
ELSE
BEGIN
    PRINT 'TransportData table already exists';
END
GO
`;
      }
      
      // Split script into individual statements (separated by GO)
      const statements = sqlScript
        .split(/^\s*GO\s*$/gim)
        .map(s => s.trim())
        .filter(s => s.length > 0 && !s.match(/^\s*--/));
      
      const results = [];
      
      for (const statement of statements) {
        if (statement.trim().length === 0 || statement.trim().startsWith('--')) {
          continue;
        }
        
        try {
          const result = await pool.request().query(statement);
          results.push({
            statement: statement.substring(0, 100) + '...',
            success: true,
            rowsAffected: result.rowsAffected || 0
          });
        } catch (stmtError) {
          // Some errors are expected (e.g., table already exists)
          if (stmtError.message.includes('already exists') || 
              stmtError.message.includes('There is already')) {
            results.push({
              statement: statement.substring(0, 100) + '...',
              success: true,
              message: 'Already exists',
              warning: true
            });
          } else {
            results.push({
              statement: statement.substring(0, 100) + '...',
              success: false,
              error: stmtError.message
            });
          }
        }
      }
      
      await pool.close();
      
      const allSuccess = results.every(r => r.success);
      
      res.status(allSuccess ? 200 : 500).json({
        message: allSuccess ? 'Database initialized successfully' : 'Some errors occurred',
        results: results,
        summary: {
          total: results.length,
          successful: results.filter(r => r.success).length,
          failed: results.filter(r => !r.success).length
        }
      });
    } catch (error) {
      await pool.close();
      throw error;
    }
  } catch (error) {
    console.error('Error initializing database:', error);
    
    const config = await getSqlConfig();
    const validation = validateSqlConfig(config);
    if (!validation.isValid) {
      return res.status(500).json({
        ...validation.error,
        code: 'ENV_MISSING'
      });
    }
    
    res.status(500).json({
      error: 'Failed to initialize database',
      details: error.message,
      code: error.code,
      message: 'An error occurred while initializing the database. Please check Azure SQL configuration.'
    });
  }
};





