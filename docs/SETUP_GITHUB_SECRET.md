# GitHub Secret Setup für Azure Functions Deployment

## Schritt 1: GitHub CLI Login

Führe folgenden Befehl aus und folge den Anweisungen:

```bash
gh auth login
```

Wähle:
- **GitHub.com**
- **HTTPS**
- **Yes** für Git Credential Helper
- **Login with a web browser** (empfohlen)

## Schritt 2: Secret setzen

Nach dem Login, führe aus:

```bash
gh secret set AZURE_FUNCTIONAPP_PUBLISH_PROFILE < publish-profile.xml
```

Oder manuell über GitHub Web UI:

1. Gehe zu: https://github.com/mariomuja/interface-configurator/settings/secrets/actions
2. Klicke auf "New repository secret"
3. **Name**: `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`
4. **Value**: Kopiere den gesamten Inhalt aus `publish-profile.xml`
5. Klicke auf "Add secret"

## Publish Profile

Das Publish Profile wurde in `publish-profile.xml` gespeichert.

**WICHTIG**: Diese Datei enthält sensible Credentials. Füge sie zu `.gitignore` hinzu!


