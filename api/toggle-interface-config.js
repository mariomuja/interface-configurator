const fetch = require('node-fetch');

module.exports = async (req, res) => {
  const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL || process.env.FUNCTION_APP_URL;
  
  if (!functionAppUrl) {
    return res.status(500).json({
      error: 'AZURE_FUNCTION_APP_URL environment variable is not set',
      message: 'Please configure the Function App URL in Vercel environment variables'
    });
  }

  try {
    const { interfaceName, enabled } = req.body;

    if (!interfaceName || typeof enabled !== 'boolean') {
      return res.status(400).json({
        error: 'Missing required fields',
        message: 'interfaceName and enabled (boolean) are required'
      });
    }

    const url = `${functionAppUrl.replace(/\/$/, '')}/api/ToggleInterfaceConfiguration`;
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        interfaceName,
        enabled
      }),
      timeout: 10000
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Function App returned status ${response.status} when accessing ${url}: ${errorText}`);
    }

    const data = await response.json();
    res.status(200).json(data);
  } catch (error) {
    console.error('Error toggling interface configuration:', error);
    const accessedUrl = functionAppUrl ? `${functionAppUrl.replace(/\/$/, '')}/api/ToggleInterfaceConfiguration` : 'unknown';
    res.status(500).json({
      error: 'Failed to toggle interface configuration',
      details: error.message,
      url: accessedUrl,
      message: `Please check Function App configuration. Attempted to access: ${accessedUrl}. Ensure AZURE_FUNCTION_APP_URL is set correctly.`
    });
  }
};

