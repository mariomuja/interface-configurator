// Vercel Serverless Function to proxy UpdateFileMask to Azure Functions
const fetch = require('node-fetch');

module.exports = async (req, res) => {
  // Handle CORS preflight requests
  if (req.method === 'OPTIONS') {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
    res.setHeader('Access-Control-Max-Age', '86400');
    return res.status(200).end();
  }

  // Set CORS headers for all responses
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');

  try {
    const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL || 'https://func-integration-main.azurewebsites.net';
    const url = `${functionAppUrl.replace(/\/$/, '')}/api/UpdateFileMask`;
    
    const response = await fetch(url, {
      method: req.method,
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(req.body),
      timeout: 10000
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Function App returned status ${response.status}: ${errorText}`);
    }

    const data = await response.json();
    res.status(response.status).json(data);
  } catch (error) {
    console.error('Error proxying UpdateFileMask:', error);
    res.status(500).json({ 
      error: 'Proxy error', 
      message: error.message 
    });
  }
};

