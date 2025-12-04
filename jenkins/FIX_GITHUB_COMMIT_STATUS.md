# Fix GitHub Commit Status 403 Error

## Problem
```
Could not update commit status. Message: {"message":"Resource not accessible by personal access token","documentation_url":"https://docs.github.com/rest/commits/statuses#create-a-commit-status","status":"403"}
```

## Root Cause
Jenkins cannot update commit statuses on GitHub because:
1. The GitHub plugin is missing or not configured
2. The Personal Access Token doesn't have the right permissions
3. The token is not properly linked to the multibranch pipeline

## Solution

### Step 1: Rebuild Jenkins Container (to install GitHub plugin)
```bash
cd jenkins
docker-compose down
docker-compose build --no-cache
docker-compose up -d
```

Wait 2-3 minutes for Jenkins to start, then go to http://localhost:8080

### Step 2: Configure GitHub Token in Jenkins UI

1. **Go to Jenkins** → **Manage Jenkins** → **Credentials** → **System** → **Global credentials**

2. **Add new credential:**
   - **Kind:** Secret text
   - **Scope:** Global
   - **Secret:** Paste your GitHub token (the one with all permissions)
   - **ID:** `githubtokenall`
   - **Description:** GitHub Personal Access Token with all permissions
   - Click **Create**

3. **Verify the token has these permissions in GitHub:**
   - Go to https://github.com/settings/tokens
   - Find your token
   - Ensure it has:
     - ✅ `repo` (Full control of private repositories)
       - Includes: `repo:status` (Commit statuses)
     - ✅ `workflow` (Update GitHub Action workflows)
   - If using **Fine-grained token**, ensure:
     - **Repository access:** `mariomuja/interface-configurator`
     - **Repository permissions:**
       - **Commit statuses:** Read and write
       - **Contents:** Read and write
       - **Metadata:** Read-only

### Step 3: Configure Multibranch Pipeline to Use GitHub

1. **Go to Jenkins** → Click on your pipeline job
2. **Click "Configure"**
3. **Under "Branch Sources":**
   - If using "Git", change to **"GitHub"**
   - **Owner:** mariomuja
   - **Repository:** interface-configurator
   - **Credentials:** Select `githubtokenall`
   - **Behaviors:** Add "Discover branches" and "Discover pull requests"
4. **Under "Build Configuration":**
   - **Mode:** by Jenkinsfile
   - **Script Path:** Jenkinsfile
5. **Save**

### Step 4: Set Environment Variable in Docker Compose

Edit `jenkins/docker-compose.yml` and add:

```yaml
environment:
  - GITHUB_TOKEN_ALL=your_github_token_here
```

Or better, use `.env` file:

```bash
# jenkins/.env
GITHUB_TOKEN_ALL=ghp_your_token_here
```

Then in `docker-compose.yml`:
```yaml
env_file:
  - .env
```

### Step 5: Restart Jenkins
```bash
docker-compose restart
```

### Step 6: Trigger a Build

Push a commit and check if the error is gone.

## Alternative: Disable Commit Status Updates

If you don't need commit status badges on GitHub, you can disable this feature:

1. Go to Jenkins → Your pipeline → Configure
2. Under "Branch Sources" → "GitHub" → "Behaviors"
3. Remove or disable "Notify GitHub of build status"
4. Save

## Verify Token Permissions

Test your token manually:
```bash
curl -H "Authorization: token YOUR_TOKEN" \
  https://api.github.com/repos/mariomuja/interface-configurator/statuses/ffb7400
```

Expected response:
- ✅ `200 OK` with JSON array = Token works
- ❌ `403 Forbidden` = Token lacks permissions
- ❌ `404 Not Found` = Token can't access repo

## Common Issues

1. **Fine-grained token not authorized for repo:**
   - Go to token settings in GitHub
   - Ensure repository access includes `interface-configurator`

2. **Classic token missing `repo` scope:**
   - Edit token in GitHub
   - Check `repo` (includes all sub-scopes)
   - Regenerate and update in Jenkins

3. **Token expired:**
   - Check expiration date in GitHub
   - Generate new token if expired

4. **Wrong credential ID:**
   - Jenkins is looking for credential ID that matches what's configured
   - Ensure credential ID matches what's used in pipeline configuration

