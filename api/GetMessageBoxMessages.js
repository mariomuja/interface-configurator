const fetch = require('node-fetch');

module.exports = async (req, res) => {
  if (req.method === 'OPTIONS') {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
    res.setHeader('Access-Control-Max-Age', '86400');
    return res.status(200).end();
  }

  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');

  try {
    const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL || 'https://func-integration-main.azurewebsites.net';
    const queryString = req.url && req.url.includes('?') ? req.url.substring(req.url.indexOf('?')) : '';
    const url = `${functionAppUrl.replace(/\/$/, '')}/api/GetMessageBoxMessages${queryString}`;

    const response = await fetch(url, {
      method: 'GET',
      headers: {
        'Content-Type': 'application/json'
      },
      timeout: 15000
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Function App returned status ${response.status}: ${errorText}`);
    }

    const data = await response.json();
    res.status(200).json(data);
  } catch (error) {
    console.error('Error proxying GetMessageBoxMessages:', error);
    res.status(500).json({
      error: 'Proxy error',
      message: error.message
    });
  }
};


