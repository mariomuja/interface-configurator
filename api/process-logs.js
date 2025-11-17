module.exports = async (req, res) => {
  try {
    // Get Function App URL from environment variable
    const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL || process.env.FUNCTION_APP_URL;
    
    if (!functionAppUrl) {
      return res.status(500).json({ 
        error: 'Function App URL not configured',
        message: 'Please set AZURE_FUNCTION_APP_URL environment variable'
      });
    }

    // Call Function App endpoint to get logs from in-memory store
    // Remove trailing slash if present
    const baseUrl = functionAppUrl.replace(/\/$/, '');
    const logsUrl = `${baseUrl}/api/GetProcessLogs`;
    
    // Use built-in fetch (Node.js 18+) or node-fetch
    let fetch;
    try {
      // Try built-in fetch first (Node.js 18+)
      fetch = globalThis.fetch || require('node-fetch');
    } catch {
      // Fallback to node-fetch if not available
      const nodeFetch = await import('node-fetch');
      fetch = nodeFetch.default;
    }
    
    const response = await fetch(logsUrl, {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json'
      }
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Function App returned status ${response.status}: ${errorText}`);
    }

    const logs = await response.json();
    
    // Transform to match expected format
    const transformedLogs = logs.map(log => ({
      id: log.id,
      timestamp: log.datetime_created ? new Date(log.datetime_created).toISOString() : new Date().toISOString(),
      level: log.level || 'info',
      message: log.message || '',
      details: log.details || null,
      component: log.component || null
    }));

    res.status(200).json(transformedLogs);
  } catch (error) {
    console.error('Error fetching process logs:', error);
    
    res.status(500).json({ 
      error: 'Failed to fetch process logs', 
      details: error.message,
      message: 'Please check Function App configuration and ensure AZURE_FUNCTION_APP_URL is set'
    });
  }
};
