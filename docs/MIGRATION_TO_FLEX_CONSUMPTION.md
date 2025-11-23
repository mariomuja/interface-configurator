# Migration zu Flex Consumption Plan

## Übersicht

Azure hat angekündigt, dass der **Linux Consumption Plan (Y1)** am **30. September 2028** end-of-life wird. Die Terraform Config wurde aktualisiert, um **Flex Consumption Plan (EP1)** zu verwenden.

## Was wurde geändert

1. **Default SKU geändert**: Von `Y1` (Linux Consumption) zu `EP1` (Flex Consumption)
2. **Terraform Config aktualisiert**: Kommentare hinzugefügt zur Dokumentation
3. **Variables aktualisiert**: Default-Wert auf `EP1` gesetzt

## Migration der bestehenden Function App

Die aktuelle Function App (`func-appe1mz5h`) läuft noch auf dem alten Consumption Plan. Um zu migrieren:

### Option 1: Terraform Migration (Empfohlen)

1. **Backup erstellen**: Stelle sicher, dass alle App Settings dokumentiert sind
2. **Terraform Apply**: Die Config verwendet jetzt EP1, Terraform wird die Migration durchführen
3. **Prüfen**: Nach dem Apply die Function App testen

### Option 2: Azure CLI Migration

```bash
# Prüfe Migration-Kompatibilität
az functionapp flex-migration list

# Starte Migration
az functionapp flex-migration start \
  --source-name func-appe1mz5h \
  --source-resource-group rg-infrastructure-as-code \
  --name func-app-flex-e1mz5h \
  --resource-group rg-infrastructure-as-code
```

**WICHTIG**: Die CLI-Migration erstellt eine NEUE Function App. Du musst dann:
- Die alte Function App löschen
- Terraform State aktualisieren
- GitHub Actions Workflow aktualisieren (neuer Function App Name)

## Vorteile von Flex Consumption

- ✅ **Schnellere Skalierung**
- ✅ **Bessere Kaltstart-Zeiten**
- ✅ **VNET-Integration möglich**
- ✅ **Private Endpoints unterstützt**
- ✅ **Zukunftssicher** (kein EOL 2028)

## Nächste Schritte

1. **Terraform Plan ausführen** um die Änderungen zu sehen:
   ```bash
   cd terraform
   terraform plan
   ```

2. **Terraform Apply** um zu migrieren:
   ```bash
   terraform apply
   ```

3. **Function App testen** nach der Migration

4. **GitHub Actions Workflow aktualisieren** falls Function App Name sich ändert


