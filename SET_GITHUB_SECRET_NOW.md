# GitHub Secret jetzt setzen - Schnellanleitung

## Schritt 1: GitHub Secret setzen

1. **Öffne diesen Link direkt:**
   https://github.com/mariomuja/infrastructure-as-code/settings/secrets/actions

2. **Klicke auf "New repository secret"** (rechts oben)

3. **Fülle aus:**
   - **Name**: `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`
   - **Secret**: Kopiere den **gesamten Inhalt** aus `publish-profile.xml` (siehe unten)

4. **Klicke auf "Add secret"**

## Schritt 2: Publish Profile Inhalt

Öffne die Datei `publish-profile.xml` im Root-Verzeichnis und kopiere **alles** von `<publishData>` bis `</publishData>` inklusive.

**WICHTIG**: Kopiere den kompletten XML-Inhalt!

## Schritt 3: Deployment auslösen

Nach dem Setzen des Secrets kannst du das Deployment auslösen:

### Option A: Automatisch (bei nächstem Push)
- Mache eine Änderung in `azure-functions/ProcessCsvBlobTrigger/`
- Committe und pushe zu `main`
- GitHub Actions startet automatisch

### Option B: Manuell
1. Gehe zu: https://github.com/mariomuja/infrastructure-as-code/actions
2. Klicke auf "Deploy Azure Functions"
3. Klicke auf "Run workflow" → "Run workflow"

## Fertig!

Nach dem Deployment sollte die Function App den Code enthalten.


