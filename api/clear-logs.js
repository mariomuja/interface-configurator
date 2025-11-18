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

    // Call Function App endpoint to clear logs
    // Remove trailing slash if present
    const baseUrl = functionAppUrl.replace(/\/$/, '');
    const clearUrl = `${baseUrl}/api/ClearProcessLogs`;
    
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
    
    const response = await fetch(clearUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      }
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Function App returned status ${response.status} when accessing ${clearUrl}: ${errorText}`);
    }

    const result = await response.json();
    
    res.status(200).json(result);
  } catch (error) {
    console.error('Error clearing process logs:', error);
    
    // Extract URL from error message if available
    const urlMatch = error.message.match(/https?:\/\/[^\s]+/);
    const accessedUrl = urlMatch ? urlMatch[0] : (functionAppUrl ? `${functionAppUrl.replace(/\/$/, '')}/api/ClearProcessLogs` : 'unknown');
    
    res.status(500).json({ 
      error: 'Failed to clear process logs', 
      details: error.message,
      url: accessedUrl,
      message: `Please check Function App configuration. Attempted to access: ${accessedUrl}. Ensure AZURE_FUNCTION_APP_URL is set correctly.`
    });
  }
};
