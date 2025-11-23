# Vercel Environment Variables Setzen

## Connection String für Azure Storage

Führe diesen Befehl aus, um den Connection String in Vercel zu setzen:

```bash
vercel env add AZURE_STORAGE_CONNECTION_STRING production
```

Wenn du zur Eingabe aufgefordert wirst, füge diesen Wert ein:

```
DefaultEndpointsProtocol=https;AccountName=stcsvtransportud3e1cem;AccountKey=bR4vExh9KvgaiRR2veY5uecDlEySMtH6ovz3dWW5pg1DiJZXTYFBVlh8INei7Fr2IDU5+AStEWtpsw==;EndpointSuffix=core.windows.net
```

## Alle benötigten Environment Variables

Stelle sicher, dass alle diese Variablen in Vercel gesetzt sind:

### Azure SQL Database
- `AZURE_SQL_SERVER` = `sql-csvtransportud3e1cem.database.windows.net`
- `AZURE_SQL_DATABASE` = `csvtransportdb`
- `AZURE_SQL_USER` = `sqladmin` (oder der Wert aus terraform.tfvars)
- `AZURE_SQL_PASSWORD` = Das Passwort aus terraform.tfvars

### Azure Storage
- `AZURE_STORAGE_CONNECTION_STRING` = Siehe oben
- `AZURE_STORAGE_CONTAINER` = `csv-uploads` (optional, Standardwert)

## Nach dem Setzen

1. **Trigger ein neues Deployment**:
   ```bash
   git commit --allow-empty -m "Trigger deployment after env vars update"
   git push origin main
   ```

2. **Oder manuell deployen**:
   ```bash
   vercel deploy --prod
   ```

3. **Testen**: Öffne die App und klicke auf "Transport starten"



