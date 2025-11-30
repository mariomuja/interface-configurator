import { test, expect } from '@playwright/test';

/**
 * Azure Functions HTTP Endpoint Tests
 * 
 * These tests verify that all Azure Function HTTP endpoints are accessible
 * and respond correctly after deployment. Tests use fetch() API since we're
 * testing HTTP endpoints, not web pages.
 */

// Base URL for Azure Function App - should be set via environment variable
const FUNCTION_APP_URL = process.env.AZURE_FUNCTION_APP_URL || 
  process.env.FUNCTION_APP_URL || 
  'https://func-integration-main.azurewebsites.net';

// Helper function to make HTTP requests
async function callFunction(
  route: string,
  method: string = 'GET',
  body?: any,
  headers: Record<string, string> = {}
): Promise<{ status: number; statusText: string; body: any; headers: Headers }> {
  const url = `${FUNCTION_APP_URL}/api/${route}`;
  const options: RequestInit = {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...headers,
    },
  };

  if (body && (method === 'POST' || method === 'PUT' || method === 'DELETE')) {
    options.body = JSON.stringify(body);
  }

  const response = await fetch(url, options);
  let responseBody: any = null;
  
  try {
    const contentType = response.headers.get('content-type');
    if (contentType && contentType.includes('application/json')) {
      responseBody = await response.json();
    } else {
      responseBody = await response.text();
    }
  } catch (e) {
    // If response body is empty or not JSON, that's okay
    responseBody = null;
  }

  return {
    status: response.status,
    statusText: response.statusText,
    body: responseBody,
    headers: response.headers,
  };
}

test.describe('Azure Functions HTTP Endpoints', () => {
  test.beforeAll(async () => {
    // Verify Function App is accessible
    const healthCheck = await callFunction('health', 'GET');
    expect(healthCheck.status).toBeLessThan(500); // Any status < 500 means the app is running
  });

  // ============================================
  // GET Endpoints
  // ============================================

  test('HealthCheck endpoint should respond', async () => {
    const response = await callFunction('health', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('GetInterfaceConfigurations endpoint should respond', async () => {
    const response = await callFunction('GetInterfaceConfigurations', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('GetInterfaceConfiguration endpoint should respond', async () => {
    const response = await callFunction('GetInterfaceConfiguration', 'GET');
    // May return 400 if required params missing, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('GetDestinationAdapterInstances endpoint should respond', async () => {
    const response = await callFunction('GetDestinationAdapterInstances', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('GetFeatures endpoint should respond', async () => {
    const response = await callFunction('GetFeatures', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('GetProcessLogs endpoint should respond', async () => {
    const response = await callFunction('GetProcessLogs', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('GetProcessingStatistics endpoint should respond', async () => {
    const response = await callFunction('GetProcessingStatistics', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('GetSqlData endpoint should respond', async () => {
    const response = await callFunction('sql-data', 'GET');
    // May return 400 if required params missing, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('GetSqlTableSchema endpoint should respond', async () => {
    const response = await callFunction('GetSqlTableSchema', 'GET');
    // May return 400 if required params missing, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('GetTargetSystems endpoint should respond', async () => {
    const response = await callFunction('GetTargetSystems', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('GetBlobContainerFolders endpoint should respond', async () => {
    const response = await callFunction('GetBlobContainerFolders', 'GET');
    // May return 400 if required params missing, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('GetServiceBusMessages endpoint should respond', async () => {
    const response = await callFunction('GetServiceBusMessages', 'GET');
    // May return 400 if required params missing, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('GetMessageBoxMessages endpoint should respond', async () => {
    const response = await callFunction('GetMessageBoxMessages', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('CheckMessageBox endpoint should respond', async () => {
    const response = await callFunction('CheckMessageBox', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('GetContainerAppStatus endpoint should respond', async () => {
    const response = await callFunction('GetContainerAppStatus', 'GET');
    // May return 400 if required params missing, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('ValidateCsvFile endpoint should respond', async () => {
    const response = await callFunction('ValidateCsvFile', 'GET');
    // May return 400 if required params missing, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('CompareCsvSqlSchema endpoint should respond', async () => {
    const response = await callFunction('CompareCsvSqlSchema', 'GET');
    // May return 400 if required params missing, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('Diagnose endpoint should respond', async () => {
    const response = await callFunction('diagnose', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('Bootstrap endpoint should respond', async () => {
    const response = await callFunction('bootstrap', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('TestConfigLoading endpoint should respond', async () => {
    const response = await callFunction('test-config-loading', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('TestCsvAdapterMessageBox endpoint should respond', async () => {
    const response = await callFunction('test-csv-adapter-messagebox', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('TestCsvProcessing endpoint should respond', async () => {
    const response = await callFunction('test-csv-processing', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('TestMessageBoxService endpoint should respond', async () => {
    const response = await callFunction('test-messagebox-service', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('TestMessageBoxWrite endpoint should respond', async () => {
    const response = await callFunction('test-messagebox-write', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  test('TestSourceAdapterProcess endpoint should respond', async () => {
    const response = await callFunction('test-source-adapter-process', 'GET');
    expect(response.status).toBeLessThan(500);
  });

  // ============================================
  // POST Endpoints
  // ============================================

  test('Login endpoint should respond', async () => {
    const response = await callFunction('Login', 'POST', {});
    // May return 400/401 for invalid credentials, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('CreateInterfaceConfiguration endpoint should respond', async () => {
    const response = await callFunction('CreateInterfaceConfiguration', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('AddDestinationAdapterInstance endpoint should respond', async () => {
    const response = await callFunction('AddDestinationAdapterInstance', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('CreateFeature endpoint should respond', async () => {
    const response = await callFunction('CreateFeature', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('ToggleFeature endpoint should respond', async () => {
    const response = await callFunction('ToggleFeature', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('ToggleInterfaceConfiguration endpoint should respond', async () => {
    const response = await callFunction('ToggleInterfaceConfiguration', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('StartTransport endpoint should respond', async () => {
    const response = await callFunction('start-transport', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('RunTransportPipeline endpoint should respond', async () => {
    const response = await callFunction('RunTransportPipeline', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('RestartAdapter endpoint should respond', async () => {
    const response = await callFunction('RestartAdapter', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('ClearProcessLogs endpoint should respond', async () => {
    const response = await callFunction('ClearProcessLogs', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('SubmitErrorToAI endpoint should respond', async () => {
    const response = await callFunction('SubmitErrorToAI', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('ProcessErrorForAI endpoint should respond', async () => {
    const response = await callFunction('ProcessErrorForAI', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('InitializeFeatures endpoint should respond', async () => {
    const response = await callFunction('InitializeFeatures', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateInstanceName endpoint should respond', async () => {
    const response = await callFunction('UpdateInstanceName', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateInterfaceName endpoint should respond', async () => {
    const response = await callFunction('UpdateInterfaceName', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateReceiveFolder endpoint should respond', async () => {
    const response = await callFunction('UpdateReceiveFolder', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateDestinationJQScriptFile endpoint should respond', async () => {
    const response = await callFunction('UpdateDestinationJQScriptFile', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateDestinationSqlStatements endpoint should respond', async () => {
    const response = await callFunction('UpdateDestinationSqlStatements', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateDestinationSourceAdapterSubscription endpoint should respond', async () => {
    const response = await callFunction('UpdateDestinationSourceAdapterSubscription', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateFeatureTestComment endpoint should respond', async () => {
    const response = await callFunction('UpdateFeatureTestComment', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('CreateContainerApp endpoint should respond', async () => {
    const response = await callFunction('CreateContainerApp', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateContainerAppConfiguration endpoint should respond', async () => {
    const response = await callFunction('UpdateContainerAppConfiguration', 'POST', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  // ============================================
  // PUT Endpoints
  // ============================================

  test('UpdateBatchSize endpoint should respond', async () => {
    const response = await callFunction('UpdateBatchSize', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateCsvData endpoint should respond', async () => {
    const response = await callFunction('UpdateCsvData', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateCsvPollingInterval endpoint should respond', async () => {
    const response = await callFunction('UpdateCsvPollingInterval', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateDestinationAdapterInstance endpoint should respond', async () => {
    const response = await callFunction('UpdateDestinationAdapterInstance', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateDestinationFileMask endpoint should respond', async () => {
    const response = await callFunction('UpdateDestinationFileMask', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateDestinationReceiveFolder endpoint should respond', async () => {
    const response = await callFunction('UpdateDestinationReceiveFolder', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateFieldSeparator endpoint should respond', async () => {
    const response = await callFunction('UpdateFieldSeparator', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateFileMask endpoint should respond', async () => {
    const response = await callFunction('UpdateFileMask', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateSourceAdapterInstance endpoint should respond', async () => {
    const response = await callFunction('UpdateSourceAdapterInstance', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateSqlConnectionProperties endpoint should respond', async () => {
    const response = await callFunction('UpdateSqlConnectionProperties', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateSqlPollingProperties endpoint should respond', async () => {
    const response = await callFunction('UpdateSqlPollingProperties', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('UpdateSqlTransactionProperties endpoint should respond', async () => {
    const response = await callFunction('UpdateSqlTransactionProperties', 'PUT', {});
    // May return 400 for invalid data, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  // ============================================
  // DELETE Endpoints
  // ============================================

  test('DeleteInterfaceConfiguration endpoint should respond', async () => {
    const response = await callFunction('DeleteInterfaceConfiguration', 'DELETE');
    // May return 400/404 for invalid/missing ID, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('DeleteBlobFile endpoint should respond', async () => {
    const response = await callFunction('DeleteBlobFile', 'DELETE');
    // May return 400/404 for invalid/missing file, but should not be 500
    expect(response.status).toBeLessThan(500);
  });

  test('RemoveDestinationAdapterInstance endpoint should respond', async () => {
    const response = await callFunction('RemoveDestinationAdapterInstance', 'DELETE');
    // May return 400/404 for invalid/missing ID, but should not be 500
    expect(response.status).toBeLessThan(500);
  });
});

