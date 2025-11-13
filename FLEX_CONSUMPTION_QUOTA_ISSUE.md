# Flex Consumption Migration - Quota Problem

## Problem

Die Migration zu Flex Consumption Plan (EP1) schlägt fehl mit folgendem Fehler:

```
Operation cannot be completed without additional quota.
Current Limit (ElasticPremium VMs): 0
```

## Ursache

Der **Flex Consumption Plan (EP1)** benötigt **ElasticPremium VM Quota** in deinem Azure Subscription. Dieses Quota ist standardmäßig nicht aktiviert.

## Lösung

### Option 1: ElasticPremium Quota anfordern (Empfohlen)

1. Gehe zu Azure Portal → Subscriptions → Dein Subscription
2. Klicke auf "Usage + quotas"
3. Suche nach "ElasticPremium VMs"
4. Klicke auf "Request increase"
5. Warte auf Freigabe (kann einige Stunden dauern)

**Nach Freigabe**: Führe `terraform apply` erneut aus.

### Option 2: Bei Y1 bleiben (Temporär)

Die Terraform Config wurde bereits auf EP1 aktualisiert. Um temporär bei Y1 zu bleiben:

1. Setze in `terraform.tfvars`:
   ```hcl
   functions_sku_name = "Y1"
   ```

2. Importiere die alte Function App zurück:
   ```bash
   terraform import azurerm_service_plan.functions /subscriptions/.../serverFarms/plan-funcs-appe1mz5h
   terraform import azurerm_linux_function_app.main[0] /subscriptions/.../sites/func-appe1mz5h
   ```

**WICHTIG**: Y1 erreicht EOL am 30. September 2028. Migration sollte vorher erfolgen.

### Option 3: Andere Region prüfen

Manche Regionen haben bereits ElasticPremium Quota aktiviert. Prüfe andere Regionen:

```bash
az vm list-usage --location "West Europe" --query "[?name.value=='ElasticPremiumVMs']"
```

## Aktueller Status

- ✅ Terraform Config auf EP1 aktualisiert
- ✅ Alte Function App gelöscht
- ❌ Neue Function App kann nicht erstellt werden (Quota fehlt)

## Nächste Schritte

1. **Quota anfordern** im Azure Portal
2. Nach Freigabe: `terraform apply` erneut ausführen
3. GitHub Actions Workflow funktioniert weiterhin (gleicher Name)

## Referenz

- [Azure Quota Limits](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/azure-subscription-service-limits)
- [Flex Consumption Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/flex-consumption-how-to)


