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
    const { interfaceName, instanceType, instanceName } = req.body;

    if (!interfaceName || !instanceType) {
      return res.status(400).json({ 
        error: 'interfaceName and instanceType are required',
        url: `${functionAppUrl}/api/UpdateInstanceName`
      });
    }

    if (instanceType !== 'Source' && instanceType !== 'Destination') {
      return res.status(400).json({ 
        error: 'instanceType must be "Source" or "Destination"',
        url: `${functionAppUrl}/api/UpdateInstanceName`
      });
    }

    const functionUrl = `${functionAppUrl}/api/UpdateInstanceName?code=${process.env.AZURE_FUNCTION_KEY || ''}`;
    
    const response = await fetch(functionUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        interfaceName,
        instanceType,
        instanceName: instanceName || (instanceType === 'Source' ? 'Source' : 'Destination')
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
    console.error('Error updating instance name:', error);
    res.status(500).json({ 
      error: error.message || 'An error occurred while updating instance name',
      url: `${functionAppUrl}/api/UpdateInstanceName`,
      details: error.stack
    });
  }
};




