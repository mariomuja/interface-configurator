# ⚠️ VERALTET - GitHub Source Control für Azure Functions

**Diese Methode wird nicht mehr verwendet!**

Wir verwenden jetzt **GitHub Actions** mit der **"Run from Package"** Methode.

## Aktuelle Methode

Siehe: [GITHUB_ACTIONS_DEPLOYMENT.md](../GITHUB_ACTIONS_DEPLOYMENT.md)

## Warum geändert?

- ✅ Run from Package ist Microsoft's empfohlene Methode
- ✅ Bessere Performance und schnellere Deployments
- ✅ Transparente Build-Logs in GitHub
- ✅ Keine Deployment Center Konfiguration nötig

## Migration

Die Deployment Center Konfiguration wurde aus Terraform entfernt. Die Function App wird jetzt über GitHub Actions deployed.

Siehe auch: [MIGRATION_SUMMARY.md](../MIGRATION_SUMMARY.md)
