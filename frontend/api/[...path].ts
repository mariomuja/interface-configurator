// Vercel Serverless Function to proxy API requests to Azure Functions
// This file should be in frontend/api/ directory for Vercel to recognize it

export default async function handler(req: any, res: any) {
  const { path } = req.query;
  const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL || 'https://func-integration-main.azurewebsites.net';
  
  // Reconstruct the path
  const apiPath = Array.isArray(path) ? path.join('/') : path || '';
  const targetUrl = `${functionAppUrl}/api/${apiPath}${req.url.includes('?') ? req.url.substring(req.url.indexOf('?')) : ''}`;
  
  try {
    // Forward the request to Azure Functions
    const response = await fetch(targetUrl, {
      method: req.method,
      headers: {
        ...req.headers,
        host: undefined, // Remove host header
      },
      body: req.method !== 'GET' && req.method !== 'HEAD' ? JSON.stringify(req.body) : undefined,
    });
    
    const data = await response.text();
    
    // Forward response headers
    response.headers.forEach((value: string, key: string) => {
      res.setHeader(key, value);
    });
    
    // Set CORS headers
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
    
    res.status(response.status).send(data);
  } catch (error: any) {
    console.error('Proxy error:', error);
    res.status(500).json({ error: 'Proxy error', message: error.message });
  }
}

