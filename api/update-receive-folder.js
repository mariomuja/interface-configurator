const https = require('https');

module.exports = async (req, res) => {
  // Set CORS headers
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') {
    res.status(200).end();
    return;
  }

  if (req.method !== 'POST') {
    res.status(405).json({ error: 'Method not allowed' });
    return;
  }

  const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL;
  if (!functionAppUrl) {
    res.status(500).json({ 
      error: 'Please check Function App configuration and ensure AZURE_FUNCTION_APP_URL is set',
      details: 'AZURE_FUNCTION_APP_URL environment variable is missing'
    });
    return;
  }

  try {
    const { interfaceName, receiveFolder } = req.body;

    if (!interfaceName) {
      res.status(400).json({ error: 'interfaceName is required' });
      return;
    }

    // Get function key from environment variable
    const functionKey = process.env.AZURE_FUNCTION_KEY || '';
    const functionName = 'UpdateReceiveFolder';
    const url = `${functionAppUrl}/api/${functionName}?code=${functionKey}`;

    const requestData = JSON.stringify({
      interfaceName,
      receiveFolder: receiveFolder || ''
    });

    const options = {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(requestData)
      },
      timeout: 30000
    };

    return new Promise((resolve, reject) => {
      const urlObj = new URL(url);
      const request = https.request(urlObj, options, (response) => {
        let data = '';

        response.on('data', (chunk) => {
          data += chunk;
        });

        response.on('end', () => {
          try {
            const jsonData = JSON.parse(data);
            
            if (response.statusCode >= 200 && response.statusCode < 300) {
              res.status(response.statusCode).json(jsonData);
              resolve();
            } else {
              res.status(response.statusCode).json({
                error: jsonData.error || 'Function App returned an error',
                details: jsonData.details || data,
                statusCode: response.statusCode,
                url: url
              });
              resolve();
            }
          } catch (parseError) {
            res.status(500).json({
              error: 'Failed to parse Function App response',
              details: data,
              parseError: parseError.message,
              url: url
            });
            resolve();
          }
        });
      });

      request.on('error', (error) => {
        res.status(500).json({
          error: 'Failed to connect to Function App',
          details: error.message,
          url: url
        });
        resolve();
      });

      request.on('timeout', () => {
        request.destroy();
        res.status(504).json({
          error: 'Function App request timeout',
          details: 'The request to the Function App timed out after 30 seconds',
          url: url
        });
        resolve();
      });

      request.write(requestData);
      request.end();
    });
  } catch (error) {
    res.status(500).json({
      error: 'An error occurred while updating receive folder',
      details: error.message,
      stack: process.env.NODE_ENV === 'development' ? error.stack : undefined
    });
  }
};




