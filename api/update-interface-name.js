const { default: fetch } = require('node-fetch');

module.exports = async (req, res) => {
  const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL;
  
  if (!functionAppUrl) {
    return res.status(500).json({ 
      error: 'Please check Function App configuration and ensure AZURE_FUNCTION_APP_URL is set',
      url: 'Not configured'
    });
  }

  try {
    const { oldInterfaceName, newInterfaceName } = req.body;

    if (!oldInterfaceName || !newInterfaceName) {
      return res.status(400).json({ 
        error: 'oldInterfaceName and newInterfaceName are required',
        url: `${functionAppUrl}/api/UpdateInterfaceName`
      });
    }

    const functionUrl = `${functionAppUrl}/api/UpdateInterfaceName?code=${process.env.AZURE_FUNCTION_KEY || ''}`;
    
    const response = await fetch(functionUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        oldInterfaceName,
        newInterfaceName
      })
    });

    if (!response.ok) {
      const errorText = await response.text();
      let errorData;
      try {
        errorData = JSON.parse(errorText);
      } catch {
        errorData = { error: errorText };
      }
      
      return res.status(response.status).json({
        ...errorData,
        url: functionUrl,
        status: response.status,
        statusText: response.statusText
      });
    }

    const data = await response.json();
    res.json(data);
  } catch (error) {
    console.error('Error updating interface name:', error);
    res.status(500).json({ 
      error: error.message || 'An error occurred while updating interface name',
      url: `${functionAppUrl}/api/UpdateInterfaceName`,
      details: error.stack
    });
  }
};




