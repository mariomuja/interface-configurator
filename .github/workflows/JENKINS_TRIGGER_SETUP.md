# Jenkins Build Trigger via GitHub Actions

## Übersicht

Die GitHub Action `trigger-jenkins.yml` triggert automatisch Jenkins-Builds bei jedem Push zu `ready/*` oder `main` Branches.

## Setup

### 1. Jenkins API Token erstellen

1. Gehe zu Jenkins: http://localhost:8080 (oder deine Jenkins URL)
2. Klicke auf deinen Benutzernamen (oben rechts) → **Configure**
3. Scrolle zu **API Token** → Klicke **Add new Token**
4. Gib einen Namen ein (z.B. "GitHub Actions")
5. Klicke **Generate** und **kopiere den Token** (wird nur einmal angezeigt!)

### 2. GitHub Secrets konfigurieren

Gehe zu: **Repository → Settings → Secrets and variables → Actions**

Erstelle folgende Secrets:

#### `JENKINS_URL`
Die URL deiner Jenkins-Instanz
```
http://localhost:8080
```
oder
```
https://jenkins.deine-domain.de
```

#### `JENKINS_USER`
Dein Jenkins Benutzername (z.B. `admin`)

#### `JENKINS_TOKEN`
Der API Token aus Schritt 1

### 3. Test

1. Push einen Commit zu einem `ready/*` Branch
2. Gehe zu **Actions** Tab in GitHub
3. Du solltest den Workflow "Trigger Jenkins Build" laufen sehen
4. Prüfe Jenkins - ein neuer Build sollte gestartet sein

## Alternative: Generic Webhook Trigger Plugin

Wenn du das [Generic Webhook Trigger Plugin](https://plugins.jenkins.io/generic-webhook-trigger/) in Jenkins installierst, kannst du einen einfacheren Webhook ohne Authentifizierung nutzen:

1. Installiere das Plugin in Jenkins
2. Konfiguriere den Webhook im Jenkinsfile oder Job
3. Nutze Option 1 im Workflow (auskommentiert)

## Vorteile gegenüber SCM Polling

- ✅ **Sofortige Builds** statt Warten auf nächsten Poll
- ✅ **Weniger Serverlast** durch Polling
- ✅ **Eindeutige Zuordnung** welcher Commit welchen Build triggert
- ✅ **GitHub Status Checks** können aktualisiert werden

## Troubleshooting

### "403 Forbidden" oder "401 Unauthorized"
- Prüfe ob JENKINS_USER und JENKINS_TOKEN korrekt sind
- Stelle sicher, dass der User Rechte zum Triggern von Builds hat

### "404 Not Found"
- Prüfe die JENKINS_URL (mit/ohne trailing slash)
- Stelle sicher, dass der Job "interface-configurator" existiert

### Build wird nicht getriggert
- Prüfe die GitHub Actions Logs
- Prüfe Jenkins Logs unter "Manage Jenkins → System Log"
- Teste den curl-Befehl manuell in deinem Terminal

