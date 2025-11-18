# GitHub Secret Setup - Einfache Anleitung

## Schritt 1: Öffne GitHub Secrets Seite

Öffne diesen Link direkt:
**https://github.com/mariomuja/interface-configurator/settings/secrets/actions**

## Schritt 2: Neues Secret hinzufügen

1. Klicke auf **"New repository secret"** (rechts oben)
2. **Name**: `AZURE_FUNCTIONAPP_PUBLISH_PROFILE`
3. **Secret**: Kopiere den **gesamten Inhalt** aus der Datei `publish-profile.xml` (siehe unten)
4. Klicke auf **"Add secret"**

## Schritt 3: Publish Profile Inhalt

Öffne die Datei `publish-profile.xml` im Root-Verzeichnis und kopiere den gesamten Inhalt.

**WICHTIG**: Kopiere alles von `<publishData>` bis `</publishData>` inklusive!

## Fertig!

Nach dem Setzen des Secrets wird der GitHub Actions Workflow automatisch funktionieren, wenn du Code zu `azure-functions/**` pusht.

## Testen

Um den Workflow zu testen:
1. Mache eine kleine Änderung in `azure-functions/main/`
2. Committe und pushe zu `main`
3. Gehe zu: https://github.com/mariomuja/interface-configurator/actions
4. Der Workflow sollte automatisch starten


