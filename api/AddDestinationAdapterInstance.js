// Vercel Serverless Function to proxy AddDestinationAdapterInstance to Azure Functions
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

  const ensureBody = async () => {
    if (req.body) {
      if (typeof req.body === 'string') {
        return req.body;
      }
      if (Buffer.isBuffer(req.body)) {
        return req.body.toString('utf8');
      }
      return JSON.stringify(req.body);
    }
    return await new Promise((resolve, reject) => {
      let rawBody = '';
      req.on('data', chunk => { rawBody += chunk; });
      req.on('end', () => resolve(rawBody || '{}'));
      req.on('error', reject);
    });
  };

  try {
    const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL || 'https://func-integration-main.azurewebsites.net';
    const url = `${functionAppUrl.replace(/\/$/, '')}/api/AddDestinationAdapterInstance`;

    const rawPayload = await ensureBody();
    let parsedBody = {};
    if (rawPayload && rawPayload.trim().length > 0) {
      try {
        parsedBody = JSON.parse(rawPayload);
      } catch (parseError) {
        console.error('Invalid JSON body for AddDestinationAdapterInstance:', parseError);
        return res.status(400).json({ error: 'Invalid JSON body' });
      }
    }

    const interfaceName = parsedBody.InterfaceName || parsedBody.interfaceName || '';
    const adapterName = parsedBody.AdapterName || parsedBody.adapterName || '';
    const instanceName = parsedBody.InstanceName || parsedBody.instanceName;
    const configuration = parsedBody.Configuration || parsedBody.configuration;

    if (!interfaceName || !adapterName) {
      return res.status(400).json({ error: 'InterfaceName and AdapterName are required' });
    }

    const payload = JSON.stringify({
      InterfaceName: interfaceName,
      AdapterName: adapterName,
      InstanceName: instanceName,
      Configuration: configuration
    });

    const response = await fetch(url, {
      method: req.method,
      headers: { 'Content-Type': 'application/json' },
      body: payload,
      timeout: 10000
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Function App returned status ${response.status}: ${errorText}`);
    }

    const data = await response.json();
    res.status(response.status).json(data);
  } catch (error) {
    console.error('Error proxying AddDestinationAdapterInstance:', error);
    res.status(500).json({
      error: 'Proxy error',
      message: error.message
    });
  }
};


