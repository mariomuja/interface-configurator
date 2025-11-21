// Vercel Serverless Function to proxy API requests to Azure Functions
import type { VercelRequest, VercelResponse } from '@vercel/node';

export default async function handler(req: VercelRequest, res: VercelResponse) {
  const { path } = req.query;
  const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL || 'https://func-integration-main.azurewebsites.net';
  
  // Reconstruct the path and query string
  const apiPath = Array.isArray(path) ? path.join('/') : path || '';
  const queryString = req.url?.includes('?') ? req.url.substring(req.url.indexOf('?')) : '';
  const targetUrl = `${functionAppUrl}/api/${apiPath}${queryString}`;
  
  try {
    // Prepare headers (exclude host and other Vercel-specific headers)
    const headers: Record<string, string> = {};
    Object.keys(req.headers).forEach(key => {
      if (!['host', 'x-vercel', 'x-forwarded'].some(prefix => key.toLowerCase().startsWith(prefix))) {
        const value = req.headers[key];
        if (typeof value === 'string') {
          headers[key] = value;
        } else if (Array.isArray(value) && value.length > 0) {
          headers[key] = value[0];
        }
      }
    });
    
    // Forward the request to Azure Functions
    const response = await fetch(targetUrl, {
      method: req.method,
      headers: headers,
      body: req.method !== 'GET' && req.method !== 'HEAD' ? JSON.stringify(req.body) : undefined,
    });
    
    const data = await response.text();
    
    // Forward response headers
    response.headers.forEach((value, key) => {
      res.setHeader(key, value);
    });
    
    // Set CORS headers
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
    
    // If we get a 404, provide more helpful error message
    if (response.status === 404) {
      console.error(`Function not found: ${targetUrl}. This usually means the function hasn't been deployed or triggers haven't synced.`);
    }
    
    res.status(response.status).send(data);
  } catch (error: any) {
    console.error('Proxy error:', error);
    res.status(500).json({ error: 'Proxy error', message: error.message });
  }
}

