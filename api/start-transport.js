const { BlobServiceClient } = require('@azure/storage-blob');
const { v4: uuidv4 } = require('uuid');

function generateSampleCsvData() {
  const data = [];
  const names = ['Max Mustermann', 'Anna Schmidt', 'Peter Müller', 'Lisa Weber', 'Thomas Fischer'];
  const cities = ['Berlin', 'München', 'Hamburg', 'Köln', 'Frankfurt'];
  
  for (let i = 1; i <= 50; i++) {
    const name = names[Math.floor(Math.random() * names.length)];
    const city = cities[Math.floor(Math.random() * cities.length)];
    data.push({
      id: i,
      name: `${name} ${i}`,
      email: `user${i}@example.com`,
      age: Math.floor(Math.random() * 40) + 20,
      city: city,
      salary: Math.floor(Math.random() * 50000) + 30000
    });
  }
  
  return data;
}

// Get CSV field separator from environment variable or use default
// This can be configured via Azure Function App Settings: CsvFieldSeparator
// Default: ║ (Box Drawing Double Vertical Line, U+2551)
const FIELD_SEPARATOR = process.env.CsvFieldSeparator || '║';

function convertToCsv(data) {
  const headers = ['id', 'name', 'email', 'age', 'city', 'salary'];
  const rows = data.map(row => 
    headers.map(header => {
      const value = row[header];
      // Escape separator and quotes if present
      const valueStr = String(value || '');
      if (valueStr.includes(FIELD_SEPARATOR) || valueStr.includes('"')) {
        return `"${valueStr.replace(/"/g, '""')}"`;
      }
      return valueStr;
    }).join(FIELD_SEPARATOR)
  );
  return [headers.join(FIELD_SEPARATOR), ...rows].join('\n');
}

module.exports = async (req, res) => {
  // Handle CORS preflight requests
  if (req.method === 'OPTIONS') {
    res.setHeader('Access-Control-Allow-Origin', '*');
    res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
    res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');
    res.setHeader('Access-Control-Max-Age', '86400'); // 24 hours
    return res.status(200).end();
  }

  // Set CORS headers for all responses
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type, Authorization');

  try {
    // Ensure interface configuration exists before uploading CSV
    const functionAppUrl = process.env.AZURE_FUNCTION_APP_URL || process.env.FUNCTION_APP_URL;
    const interfaceName = process.env.INTERFACE_NAME || 'FromCsvToSqlServerExample';
    
    if (functionAppUrl) {
      try {
        const configUrl = `${functionAppUrl.replace(/\/$/, '')}/api/CreateInterfaceConfiguration`;
        const fetch = require('node-fetch');
        
        await fetch(configUrl, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify({
            interfaceName: interfaceName,
            sourceAdapterName: 'CSV',
            sourceConfiguration: JSON.stringify({ source: 'csv-files/csv-incoming', enabled: true }),
            destinationAdapterName: 'SqlServer',
            destinationConfiguration: JSON.stringify({ destination: 'TransportData', enabled: true }),
            description: 'Default CSV to SQL Server interface'
          }),
          timeout: 5000
        });
        console.log(`Interface configuration '${interfaceName}' ensured`);
      } catch (configError) {
        console.warn('Could not ensure interface configuration (will continue anyway):', configError.message);
        // Continue with upload even if config creation fails
      }
    }
    // Support both connection string and account name/key authentication
    let blobServiceClient;
    if (process.env.AZURE_STORAGE_CONNECTION_STRING) {
      blobServiceClient = BlobServiceClient.fromConnectionString(
        process.env.AZURE_STORAGE_CONNECTION_STRING
      );
    } else if (process.env.AZURE_STORAGE_ACCOUNT_NAME && process.env.AZURE_STORAGE_ACCOUNT_KEY) {
      const accountName = process.env.AZURE_STORAGE_ACCOUNT_NAME;
      const accountKey = process.env.AZURE_STORAGE_ACCOUNT_KEY;
      const connectionString = `DefaultEndpointsProtocol=https;AccountName=${accountName};AccountKey=${accountKey};EndpointSuffix=core.windows.net`;
      blobServiceClient = BlobServiceClient.fromConnectionString(connectionString);
    } else {
      throw new Error('Azure Storage credentials not configured');
    }
    
    // Use csv-files container with csv-incoming folder
    const containerName = process.env.AZURE_STORAGE_CONTAINER || 'csv-files';
    const containerClient = blobServiceClient.getContainerClient(containerName);
    
    // Ensure container exists (container is already created via Bicep/Terraform with private access)
    // We don't need to specify access level here since the container is managed by Infrastructure as Code
    // If container doesn't exist, create it without specifying access (will use storage account default)
    try {
      const createResponse = await containerClient.createIfNotExists();
      console.log('Container createIfNotExists response:', {
        succeeded: createResponse.succeeded,
        errorCode: createResponse.errorCode
      });
    } catch (createError) {
      console.error('Error creating container:', {
        message: createError.message,
        code: createError.code,
        statusCode: createError.statusCode,
        details: createError.details
      });
      // If container already exists, that's fine - continue
      if (createError.statusCode !== 409) { // 409 = Conflict (already exists)
        throw createError;
      }
    }
    
    // Use CSV content from request body if provided, otherwise generate sample data
    let csvContent;
    if (req.body && req.body.csvContent && typeof req.body.csvContent === 'string' && req.body.csvContent.trim() !== '') {
      csvContent = req.body.csvContent;
      console.log('Using CSV content from request body');
    } else {
      // Generate CSV data
      const csvData = generateSampleCsvData();
      csvContent = convertToCsv(csvData);
      console.log('Using generated sample CSV data');
    }
    
    // Upload to blob storage in csv-incoming folder
    const fileName = `transport-${uuidv4()}.csv`;
    const blobPath = `csv-incoming/${fileName}`; // Upload to csv-incoming folder
    const blockBlobClient = containerClient.getBlockBlobClient(blobPath);
    
    await blockBlobClient.upload(csvContent, csvContent.length, {
      blobHTTPHeaders: { blobContentType: 'text/csv' }
    });
    
    res.status(200).json({
      message: 'CSV file uploaded to Azure Blob Storage',
      fileId: fileName,
      blobUrl: blockBlobClient.url
    });
  } catch (error) {
    console.error('Error starting transport:', error);
    console.error('Error details:', {
      message: error.message,
      code: error.code,
      statusCode: error.statusCode,
      requestId: error.requestId,
      details: error.details,
      stack: error.stack?.substring(0, 500)
    });
    
    // Provide more detailed error information
    let errorMessage = error.message || 'Unknown error';
    let errorDetails = '';
    
    if (error.message && error.message.includes('Azure Storage credentials')) {
      errorMessage = 'Azure Storage configuration incomplete';
      errorDetails = 'Missing AZURE_STORAGE_CONNECTION_STRING or AZURE_STORAGE_ACCOUNT_NAME/AZURE_STORAGE_ACCOUNT_KEY in Vercel environment variables';
    } else if (error.statusCode === 403) {
      errorMessage = 'Access denied to storage account';
      errorDetails = 'Check if AZURE_STORAGE_CONNECTION_STRING is correct and has proper permissions';
    } else if (error.statusCode === 404) {
      errorMessage = 'Storage account or container not found';
      errorDetails = 'Check storage account name and container name';
    } else if (error.code === 'ContainerNotFound' || error.message?.includes('container')) {
      errorMessage = 'Error accessing or creating storage container';
      errorDetails = `${error.message} (Status: ${error.statusCode || 'N/A'})`;
    } else if (error.message?.includes('Public access')) {
      errorMessage = 'Public access not permitted';
      errorDetails = 'Storage account has public access disabled. Container will be created with private access.';
    }
    
    res.status(500).json({ 
      error: 'Failed to start transport', 
      details: errorDetails || errorMessage,
      message: errorMessage,
      code: error.code,
      statusCode: error.statusCode,
      requestId: error.requestId
    });
  }
};

