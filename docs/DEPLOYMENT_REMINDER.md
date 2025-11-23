# Deployment Reminder

## Azure Functions Deployment

**IMPORTANT:** After making changes to Azure Functions code, remember to deploy the updated functions to Azure.

### When to Deploy

Deploy Azure Functions after:
- ✅ Performance optimizations (bulk inserts, streaming CSV parsing, connection pooling, etc.)
- ✅ New features or functionality changes
- ✅ Bug fixes
- ✅ Configuration changes
- ✅ Dependency updates
- ✅ Constructor signature changes
- ✅ New API endpoints added
- ✅ CORS or authentication changes

### Recent Changes Requiring Deployment

- **Performance Optimizations** (Latest):
  - Bulk inserts using SqlBulkCopy
  - Streaming CSV parsing for large files
  - Increased batch sizes (100 → 1000-5000)
  - Connection pooling with retry logic
  - Parallel destination processing with concurrency limits

### Deployment Command

```bash
cd azure-functions/main
func azure functionapp publish func-integration-main
```

### Or via Azure Portal

1. Go to Azure Portal
2. Navigate to Function App: `func-integration-main`
3. Use "Deploy" option or continuous deployment

---

**Last Updated:** 2024-01-XX
**Status:** ⚠️ Pending Deployment

