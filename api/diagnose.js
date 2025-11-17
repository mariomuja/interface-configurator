module.exports = async (req, res) => {
  const diagnostics = {
    timestamp: new Date().toISOString(),
    checks: []
  };

  // Check 1: SQL Configuration
  try {
    const { getSqlConfig, validateSqlConfig } = require('./sql-config');
    const config = await getSqlConfig();
    const validation = validateSqlConfig(config);
    
    diagnostics.checks.push({
      name: 'SQL Configuration',
      status: validation.isValid ? 'OK' : 'FAILED',
      details: validation.isValid 
        ? 'All SQL environment variables are set'
        : `Missing: ${validation.missing?.join(', ') || 'Unknown'}`
    });
  } catch (error) {
    diagnostics.checks.push({
      name: 'SQL Configuration',
      status: 'ERROR',
      details: error.message
    });
  }

  // Check 2: Storage Configuration
  try {
    const hasConnectionString = !!process.env.AZURE_STORAGE_CONNECTION_STRING;
    const hasAccountName = !!process.env.AZURE_STORAGE_ACCOUNT_NAME;
    const hasAccountKey = !!process.env.AZURE_STORAGE_ACCOUNT_KEY;
    const storageConfigured = hasConnectionString || (hasAccountName && hasAccountKey);
    
    diagnostics.checks.push({
      name: 'Storage Configuration',
      status: storageConfigured ? 'OK' : 'FAILED',
      details: storageConfigured
        ? 'Storage credentials are configured'
        : 'Missing AZURE_STORAGE_CONNECTION_STRING or AZURE_STORAGE_ACCOUNT_NAME/AZURE_STORAGE_ACCOUNT_KEY'
    });
  } catch (error) {
    diagnostics.checks.push({
      name: 'Storage Configuration',
      status: 'ERROR',
      details: error.message
    });
  }

  // Check 3: Function App URL
  try {
    const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL || process.env.FUNCTION_APP_URL;
    
    diagnostics.checks.push({
      name: 'Function App URL',
      status: functionAppUrl ? 'OK' : 'FAILED',
      details: functionAppUrl 
        ? `Configured: ${functionAppUrl.substring(0, 50)}...`
        : 'Missing AZURE_FUNCTION_APP_URL environment variable'
    });

    // Check 4: Function App Connectivity
    if (functionAppUrl) {
      try {
        let fetch;
        try {
          fetch = globalThis.fetch || require('node-fetch');
        } catch {
          const nodeFetch = await import('node-fetch');
          fetch = nodeFetch.default;
        }

        const baseUrl = functionAppUrl.replace(/\/$/, '');
        const logsUrl = `${baseUrl}/api/GetProcessLogs`;
        
        const response = await fetch(logsUrl, {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' },
          signal: AbortSignal.timeout(5000) // 5 second timeout
        });

        if (response.ok) {
          const logs = await response.json();
          diagnostics.checks.push({
            name: 'Function App Connectivity',
            status: 'OK',
            details: `Connected successfully. Found ${logs.length} log entries.`
          });
        } else {
          diagnostics.checks.push({
            name: 'Function App Connectivity',
            status: 'FAILED',
            details: `HTTP ${response.status}: ${await response.text()}`
          });
        }
      } catch (error) {
        diagnostics.checks.push({
          name: 'Function App Connectivity',
          status: 'ERROR',
          details: `Cannot connect: ${error.message}`
        });
      }
    }
  } catch (error) {
    diagnostics.checks.push({
      name: 'Function App URL',
      status: 'ERROR',
      details: error.message
    });
  }

  // Check 5: SQL Database Connection
  try {
    const { getSqlConfig, validateSqlConfig, sql } = require('./sql-config');
    const config = await getSqlConfig();
    const validation = validateSqlConfig(config);
    
    if (validation.isValid) {
      try {
        const pool = await sql.connect(config);
        const result = await pool.request().query(`
          SELECT COUNT(*) AS TableCount
          FROM sys.tables
          WHERE name = 'TransportData'
        `);
        const tableExists = result.recordset[0].TableCount > 0;
        
        await pool.close();
        
        diagnostics.checks.push({
          name: 'SQL Database Connection',
          status: 'OK',
          details: tableExists 
            ? 'Connected successfully. TransportData table exists.'
            : 'Connected successfully. TransportData table does not exist yet (will be created on first CSV upload).'
        });
      } catch (error) {
        diagnostics.checks.push({
          name: 'SQL Database Connection',
          status: 'FAILED',
          details: `Connection error: ${error.message}`
        });
      }
    } else {
      diagnostics.checks.push({
        name: 'SQL Database Connection',
        status: 'SKIPPED',
        details: 'SQL configuration incomplete'
      });
    }
  } catch (error) {
    diagnostics.checks.push({
      name: 'SQL Database Connection',
      status: 'ERROR',
      details: error.message
    });
  }

  // Summary
  const allOk = diagnostics.checks.every(c => c.status === 'OK' || c.status === 'SKIPPED');
  const hasErrors = diagnostics.checks.some(c => c.status === 'ERROR' || c.status === 'FAILED');
  
  diagnostics.summary = {
    overall: allOk ? 'OK' : hasErrors ? 'ISSUES FOUND' : 'WARNINGS',
    totalChecks: diagnostics.checks.length,
    passed: diagnostics.checks.filter(c => c.status === 'OK').length,
    failed: diagnostics.checks.filter(c => c.status === 'FAILED' || c.status === 'ERROR').length
  };

  res.status(200).json(diagnostics);
};

