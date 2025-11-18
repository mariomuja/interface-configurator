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
    const url = `${functionAppUrl.replace(/\/$/, '')}/api/InitializeMessageBox`;
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      timeout: 30000
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`Function App returned ${response.status}: ${errorText}`);
    }

    const data = await response.json();
    res.status(200).json(data);
  } catch (error) {
    console.error('Error initializing MessageBox:', error);
    res.status(500).json({
      error: 'Failed to initialize MessageBox database',
      details: error.message
    });
  }
};




