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
    const url = `${functionAppUrl.replace(/\/$/, '')}/api/GetInterfaceConfigurations`;
    const response = await fetch(url, {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json'
      },
      timeout: 10000
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Function App returned status ${response.status} when accessing ${url}: ${errorText}`);
    }

    const data = await response.json();
    res.status(200).json(data);
  } catch (error) {
    console.error('Error fetching interface configurations:', error);
    const accessedUrl = functionAppUrl ? `${functionAppUrl.replace(/\/$/, '')}/api/GetInterfaceConfigurations` : 'unknown';
    res.status(500).json({
      error: 'Failed to fetch interface configurations',
      details: error.message,
      url: accessedUrl,
      message: `Please check Function App configuration. Attempted to access: ${accessedUrl}. Ensure AZURE_FUNCTION_APP_URL is set correctly.`
    });
  }
};

