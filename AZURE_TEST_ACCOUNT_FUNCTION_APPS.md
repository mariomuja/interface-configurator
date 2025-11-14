# Azure Test Account - Function Apps Unterstützung

## Was wird unterstützt

### ✅ Consumption Plan (Y1) - Unterstützt

**Dein aktueller Plan:**
- **SKU**: Y1 (Consumption Plan)
- **Tier**: Dynamic
- **OS**: Linux
- **Runtime**: dotnet-isolated

**Unterstützt auch mit Azure Test Account / Free Tier:**
- ✅ Consumption Plan (Y1) - **JA, unterstützt**
- ✅ Linux Function Apps - **JA, unterstützt**
- ✅ dotnet-isolated Runtime - **JA, unterstützt**
- ✅ Blob Triggers - **JA, unterstützt**
- ✅ Run from Package (mit Blob URL) - **JA, unterstützt**

### ❌ Was NICHT unterstützt wird

**Für Linux Consumption Plan:**
- ❌ `WEBSITE_RUN_FROM_PACKAGE=1` - **NICHT unterstützt**
- ✅ Stattdessen: `WEBSITE_RUN_FROM_PACKAGE=<Blob-URL>` - **ERFORDERLICH**

## Wichtige Unterschiede: Linux Consumption Plan

### Windows vs Linux Consumption Plan

| Feature | Windows Consumption | Linux Consumption |
|---------|-------------------|-------------------|
| `WEBSITE_RUN_FROM_PACKAGE=1` | ✅ Unterstützt | ❌ **NICHT unterstützt** |
| `WEBSITE_RUN_FROM_PACKAGE=<URL>` | ✅ Unterstützt | ✅ **ERFORDERLICH** |
| Automatisches Setzen bei ZIP-Deploy | ✅ Automatisch | ✅ Automatisch (als URL) |

### Für Linux Consumption Plan

**Korrekte Konfiguration:**
```bash
WEBSITE_RUN_FROM_PACKAGE=https://<storage-account>.blob.core.windows.net/function-releases/<package>.zip?<sas-token>
```

**Azure setzt dies automatisch** beim Deployment mit `az functionapp deployment source config-zip`.

## Dein aktuelles Setup

✅ **Korrekt konfiguriert:**
- Consumption Plan (Y1) - unterstützt auch mit Test Account
- Linux Function App - unterstützt
- dotnet-isolated Runtime - unterstützt

❌ **Problem:**
- `WEBSITE_RUN_FROM_PACKAGE` ist nicht gesetzt
- Azure sollte es beim Deployment automatisch setzen, tut es aber nicht

## Lösung

### Option 1: Workflow aktualisieren (Empfohlen)

Der Workflow sollte nach dem Deployment prüfen, ob `WEBSITE_RUN_FROM_PACKAGE` gesetzt ist, und es ggf. manuell setzen.

### Option 2: Manuell setzen

Nach jedem Deployment die Blob URL manuell setzen:

```bash
# Finde die neueste Blob URL
az storage blob list \
  --account-name stfuncsappe1mz5h \
  --container-name function-releases \
  --query "[0].{Name: name, Url: properties.blobTier}" \
  -o table

# Setze WEBSITE_RUN_FROM_PACKAGE
az functionapp config appsettings set \
  --resource-group rg-infrastructure-as-code \
  --name func-appe1mz5h \
  --settings WEBSITE_RUN_FROM_PACKAGE="<blob-url>"
```

## Zusammenfassung

**Die Azure Testversion ist NICHT das Problem!**

- ✅ Consumption Plan wird vollständig unterstützt
- ✅ Linux Function Apps werden unterstützt
- ✅ Run from Package wird unterstützt (mit Blob URL)

**Das Problem ist:**
- `WEBSITE_RUN_FROM_PACKAGE` wird nicht automatisch gesetzt
- Muss manuell oder im Workflow gesetzt werden

## Nächste Schritte

1. Workflow aktualisieren, um `WEBSITE_RUN_FROM_PACKAGE` automatisch zu setzen
2. Oder manuell nach jedem Deployment setzen
3. Function App neu starten nach dem Setzen

