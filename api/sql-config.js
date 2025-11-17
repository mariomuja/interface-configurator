const sql = require('mssql');

async function getSqlConfig() {
  // Trim whitespace and newlines from environment variables (fixes Windows CRLF issue)
  const server = process.env.AZURE_SQL_SERVER?.trim();
  const database = process.env.AZURE_SQL_DATABASE?.trim();
  const user = process.env.AZURE_SQL_USER?.trim();
  const password = process.env.AZURE_SQL_PASSWORD?.trim();
  
  // Log configuration status (without sensitive data)
  console.log('SQL Config Check:', {
    server: server ? `${server.substring(0, 20)}...` : 'MISSING',
    database: database || 'MISSING',
    user: user || 'MISSING',
    password: password ? 'SET' : 'MISSING'
  });
  
  return {
    server,
    database,
    user,
    password,
    options: {
      encrypt: true,
      trustServerCertificate: false, // Keep false for security, but handle certificate validation
      requestTimeout: 30000,
      connectionTimeout: 30000,
      // Additional options for Azure SQL
      enableArithAbort: true,
      cryptoCredentialsDetails: {
        minVersion: 'TLSv1.2'
      }
    }
  };
}

function validateSqlConfig(config) {
  if (!config.server || !config.database || !config.user || !config.password) {
    const missing = [];
    if (!config.server) missing.push('AZURE_SQL_SERVER');
    if (!config.database) missing.push('AZURE_SQL_DATABASE');
    if (!config.user) missing.push('AZURE_SQL_USER');
    if (!config.password) missing.push('AZURE_SQL_PASSWORD');
    
    return {
      isValid: false,
      missing,
      error: {
        error: 'Database configuration incomplete',
        details: `Missing environment variables: ${missing.join(', ')}`,
        message: 'Please configure Azure SQL environment variables in Vercel'
      }
    };
  }
  
  return { isValid: true };
}

function getSqlErrorMessage(error) {
  let errorMessage = error.message;
  if (error.code === 'ETIMEOUT' || error.code === 'ESOCKET' || error.code === 'ECONNREFUSED') {
    errorMessage = 'Cannot connect to Azure SQL Server. Check firewall rules and connection string.';
  } else if (error.code === 'ELOGIN') {
    errorMessage = 'Authentication failed. Check SQL credentials.';
  } else if (error.code === 'EREQUEST') {
    errorMessage = 'SQL query failed. Check if tables exist.';
  }
  return errorMessage;
}

module.exports = {
  getSqlConfig,
  validateSqlConfig,
  getSqlErrorMessage,
  sql
};

