# Azure Functions - JavaScript Implementation

This Azure Functions project uses **JavaScript** and is fully compatible with the **VS Code Azure Functions extension** by Microsoft.

## VS Code Setup

### 1. Install Required Extensions

1. Open VS Code
2. Press `Ctrl+Shift+X` to open Extensions
3. Install:
   - **Azure Functions** (ms-azuretools.vscode-azurefunctions)
   - **Azure Tools** (ms-azuretools.azure-dev) - Optional but recommended

### 2. Open Project

1. Open VS Code
2. File → Open Folder → Select `azure-functions` folder
3. VS Code will detect it as an Azure Functions project

## Project Structure

```
azure-functions/
├── host.json                    # Function app configuration
├── package.json                 # Node.js dependencies
├── local.settings.json          # Local development settings (create from example)
├── SimpleTestFunction/          # HTTP trigger function
│   ├── function.json           # Function binding configuration
│   └── index.js                # Function code
└── .vscode/
    └── settings.json           # VS Code Azure Functions settings
```

## Using VS Code Azure Functions Extension

### Create New Function

1. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
2. Type: `Azure Functions: Create Function`
3. Follow the wizard:
   - Select template (HTTP Trigger, Timer Trigger, etc.)
   - Enter function name
   - Select authorization level
4. VS Code will create the function folder with `function.json` and `index.js`

### Deploy Function

1. Press `Ctrl+Shift+P`
2. Type: `Azure Functions: Deploy to Function App`
3. Select your Azure subscription
4. Select your Function App
5. VS Code will deploy your functions

### Debug Locally

1. Press `F5` or click Debug → Start Debugging
2. VS Code will start the Functions runtime locally
3. Set breakpoints in your `index.js` files
4. Functions will be available at `http://localhost:7071`

### View Functions in Azure

1. Press `Ctrl+Shift+P`
2. Type: `Azure Functions: Open in Portal`
3. Select your Function App
4. Browser opens to Azure Portal

## Local Development

### Prerequisites

- Node.js 20.x installed
- Azure Functions Core Tools installed:
  ```bash
  npm install -g azure-functions-core-tools@4 --unsafe-perm true
  ```

### Run Locally

1. Copy `local.settings.json.example` to `local.settings.json`
2. Update connection strings if needed
3. Run:
   ```bash
   npm install
   func start
   ```

### Test Function

```bash
# HTTP GET
curl http://localhost:7071/api/SimpleTestFunction

# HTTP POST
curl -X POST http://localhost:7071/api/SimpleTestFunction
```

## Function Structure

### function.json

Defines the function bindings:

```json
{
  "bindings": [
    {
      "authLevel": "anonymous",
      "type": "httpTrigger",
      "direction": "in",
      "name": "req",
      "methods": ["get", "post"]
    },
    {
      "type": "http",
      "direction": "out",
      "name": "res"
    }
  ],
  "scriptFile": "index.js"
}
```

### index.js

The function code:

```javascript
module.exports = async function (context, req) {
    context.log('Function executed!');
    
    context.res = {
        status: 200,
        body: 'Hello from Azure Function!'
    };
};
```

## Deployment

Functions are automatically deployed via GitHub Actions when you push to `main` branch.

Manual deployment via VS Code:
1. Press `Ctrl+Shift+P`
2. Type: `Azure Functions: Deploy to Function App`
3. Follow the prompts

## Benefits of JavaScript Functions

✅ **Full VS Code Extension Support** - Create, deploy, debug directly from VS Code
✅ **Simple Structure** - Easy to understand and maintain
✅ **Fast Development** - No compilation step needed
✅ **Rich Ecosystem** - Access to all npm packages
✅ **Easy Debugging** - Built-in debugging support in VS Code

## Troubleshooting

**Extension not working?**
- Ensure you're in the `azure-functions` folder (not parent directory)
- Check that `host.json` exists
- Reload VS Code window

**Functions not appearing in VS Code?**
- Check that `function.json` files are valid JSON
- Ensure `host.json` is in the root of `azure-functions` folder
- Restart VS Code

**Local debugging not working?**
- Install Azure Functions Core Tools
- Check `local.settings.json` exists
- Run `func start` manually to see errors

