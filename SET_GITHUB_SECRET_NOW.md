# ⚠️ VERALTET - GitHub Secret Setup

**Diese Anleitung ist veraltet!**

Wir verwenden jetzt **Service Principal Credentials** statt Publish Profile.

## Aktuelle Methode

Siehe: [SETUP_GITHUB_SECRETS.md](./SETUP_GITHUB_SECRETS.md)

## Schnellstart

```powershell
# Windows
.\setup-github-secrets.ps1
```

```bash
# Linux/Mac
./setup-github-secrets.sh
```

## Was hat sich geändert?

**Alt**: Publish Profile (XML)  
**Neu**: Service Principal Credentials (JSON)

**Vorteile**:
- ✅ Automatisiertes Setup via Scripts
- ✅ Sicherer (Service Principal mit minimalen Berechtigungen)
- ✅ Einfacher zu verwalten

## Benötigte Secrets

1. `AZURE_CREDENTIALS` - Service Principal JSON
2. `AZURE_RESOURCE_GROUP` - Resource Group Name
3. `AZURE_FUNCTIONAPP_NAME` - Function App Name

Siehe: [SETUP_GITHUB_SECRETS.md](./SETUP_GITHUB_SECRETS.md) für Details.
