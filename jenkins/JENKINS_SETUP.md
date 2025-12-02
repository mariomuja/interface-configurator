# Jenkins Multibranch Pipeline Setup Guide

This guide explains how to configure Jenkins to automatically run the pipeline for branches starting with `ready/` pushed to GitHub.

**Important**: The pipeline is configured to run **only for branches whose name starts with `ready/`** (e.g., `ready/feature-123`, `ready/bugfix-456`).

## Prerequisites

1. Jenkins server installed and running
2. GitHub repository access
3. Required Jenkins plugins installed (see below)

## Required Jenkins Plugins

Install these plugins via Jenkins → Manage Jenkins → Manage Plugins:

- **Git Plugin** - For Git integration
- **GitHub Plugin** or **GitHub Branch Source Plugin** - For GitHub integration
- **Pipeline Plugin** - For Pipeline support
- **Multibranch Pipeline Plugin** - For multibranch support
- **HTML Publisher Plugin** - For test reports
- **JUnit Plugin** - For test results
- **Code Coverage API Plugin** - For coverage reports
- **Timestamper Plugin** - For build timestamps
- **AnsiColor Plugin** - For colored console output
- **Email Extension Plugin** - For email notifications (optional)
- **Slack Notification Plugin** - For Slack notifications (optional)

## Setup Steps

### 1. Create Multibranch Pipeline Job

1. Go to Jenkins Dashboard
2. Click **New Item**
3. Enter a name (e.g., `interface-configurator`)
4. Select **Multibranch Pipeline**
5. Click **OK**

### 2. Configure Branch Sources

1. In the job configuration, scroll to **Branch Sources**
2. Click **Add source** → Select **GitHub**
3. Configure GitHub connection:
   - **Credentials**: Add GitHub credentials (Personal Access Token or SSH key)
   - **Owner**: Your GitHub username or organization
   - **Repository**: Your repository name
   - **Behaviors**: 
     - Add **Discover branches** → Strategy: **All branches**
     - Add **Filter by name (with regular expression)** → **Include**: `^ready/.*`
       - This ensures only branches starting with `ready/` are discovered
     - Add **Discover pull requests** → Strategy: **Merging the pull request with the current target branch revision**
       - **Filter by name (with regular expression)** → **Include**: `^ready/.*` (for PR branches)

### 3. Configure Build Configuration

1. **Build Configuration**:
   - **Mode**: **by Jenkinsfile**
   - **Script Path**: `Jenkinsfile` (default)

### 4. Configure Scan Multibranch Pipeline Triggers

1. **Scan Multibranch Pipeline Triggers**:
   - ✅ **Periodically if not otherwise run** → Interval: `1 hour` (or as needed)
   - ✅ **Trigger builds remotely** → Authentication token: Generate a token
   - ✅ **Build whenever a SNAPSHOT dependency is built** (optional)

### 5. Configure GitHub Webhook (Recommended)

To trigger builds immediately on push:

1. Go to your GitHub repository
2. Navigate to **Settings** → **Webhooks**
3. Click **Add webhook**
4. Configure:
   - **Payload URL**: `https://your-jenkins-url/github-webhook/`
   - **Content type**: `application/json`
   - **Secret**: (optional, but recommended)
   - **Which events**: Select **Just the push event** or **Let me select individual events** → ✅ **Pushes**
   - ✅ **Active**

5. In Jenkins, go to **Manage Jenkins** → **Configure System**
6. Under **GitHub**, add your GitHub server:
   - **Name**: `GitHub`
   - ✅ **Manage hooks**
   - **Credentials**: Add GitHub credentials

### 6. Configure Build Discarder

In the Multibranch Pipeline job:
- **Build Discarder**: 
  - ✅ **Discard old items**
  - **Days to keep builds**: `30`
  - **Max # of builds to keep**: `10`

### 7. Configure Notifications (Optional)

1. **Email Notifications**:
   - Add email recipients in the `post` section of Jenkinsfile
   - Configure SMTP in Jenkins → Manage Jenkins → Configure System

2. **Slack Notifications**:
   - Install Slack Notification Plugin
   - Configure Slack workspace in Jenkins → Manage Jenkins → Configure System
   - Add Slack webhook URL

## How It Works

### Automatic Branch Detection

When configured as a Multibranch Pipeline:

1. **Initial Scan**: Jenkins scans the repository and creates jobs for all branches matching `ready/*`
2. **New Branch Detection**: When a new branch starting with `ready/` is pushed to GitHub:
   - GitHub webhook triggers Jenkins
   - Jenkins scans the repository
   - A new job is created for the branch (if it matches `ready/*`)
   - Pipeline runs automatically
3. **Branch Filtering**: Branches not starting with `ready/` are ignored and won't trigger builds

### Branch-Specific Behavior

The pipeline automatically adapts based on branch name:

- **All `ready/*` branches**: Run checkout, install, lint, build, unit tests, security scan, coverage enforcement
- **`ready/*` branches targeting main/develop**: Additionally run E2E tests, accessibility tests
- **`ready/*` branches targeting main**: Additionally run visual regression tests
- **`ready/*` branches targeting develop**: Deploy preview
- **`ready/*` branches targeting main**: Deploy production

**Note**: Only branches starting with `ready/` will trigger the pipeline. Other branches are ignored.

### Pull Request Support

When a PR is opened targeting `main` or `develop`:
- Pipeline runs automatically
- E2E tests are included
- Results are visible in GitHub (if GitHub integration is configured)

## Testing the Setup

### Test 1: Create a New Branch Starting with "ready/"

```bash
git checkout -b ready/test-jenkins-pipeline
git push origin ready/test-jenkins-pipeline
```

Expected: Jenkins should detect the new branch and run the pipeline.

**Note**: If you create a branch without `ready/` prefix, it will NOT trigger the pipeline.

### Test 2: Push Changes to Existing Branch

```bash
git checkout ready/test-jenkins-pipeline
# Make a change
git commit -am "Test Jenkins pipeline"
git push
```

Expected: Jenkins should trigger a new build for the branch.

**Note**: Only works if the branch name starts with `ready/`.

### Test 3: Create a Pull Request

1. Create a PR from `ready/test-jenkins-pipeline` to `main`
2. Expected: Jenkins should run the pipeline with E2E tests included

**Note**: The PR source branch must start with `ready/` for the pipeline to run.

## Troubleshooting

### Pipeline Not Running for New Branches

1. **Check Branch Name**: Ensure branch name starts with `ready/` (e.g., `ready/feature-123`)
2. **Check Branch Filter**: Verify branch filter is set to `^ready/.*` in Jenkins job configuration
3. **Check Webhook**: Verify GitHub webhook is configured and active
4. **Check Scan**: Manually trigger "Scan Multibranch Pipeline Now" in Jenkins
5. **Check Logs**: View "Scan Multibranch Pipeline" logs in Jenkins
6. **Check Credentials**: Verify GitHub credentials are valid

### Builds Not Triggering on Push

1. **Check Webhook Delivery**: In GitHub → Settings → Webhooks → Recent Deliveries
2. **Check Jenkins Logs**: Jenkins → Manage Jenkins → System Log
3. **Verify URL**: Ensure webhook URL matches your Jenkins instance
4. **Check Permissions**: Ensure GitHub token has `repo` scope

### Coverage Thresholds Failing

- Coverage thresholds are enforced on **all** `ready/*` branches
- This ensures code quality before merging
- Adjust `COVERAGE_THRESHOLD` in Jenkinsfile if needed (default: 70%)

### E2E Tests Not Running

- E2E tests run on `ready/*` branches that target `main`, `develop`, or `master`
- This is intentional to speed up builds on feature branches
- To run E2E on all `ready/*` branches, modify the `when` condition in the E2E stage

## Advanced Configuration

### Custom Branch Patterns

The pipeline is configured to only run for branches starting with `ready/`. To modify this:

1. **In Jenkins Job Configuration**:
   - Update **Filter by name (with regular expression)** → **Include**: `^ready/.*`
   - Change the pattern to match your needs (e.g., `^(ready|release)/.*`)

2. **In Jenkinsfile**:
   - Update the branch validation check in the Checkout stage
   - Modify the `when` conditions in stages that check branch names

**Current Configuration**: Only `ready/*` branches trigger builds.

### Parallel Execution

The pipeline already uses parallel execution for:
- Lint & Code Quality (TypeScript + ESLint)
- Unit Tests (Run Tests + Check Coverage)

### Build Caching

Consider configuring:
- **Node modules caching**: Use Jenkins workspace caching
- **Docker agents**: Use Docker agents with pre-installed dependencies
- **Build artifacts**: Artifacts are automatically archived

## Security Considerations

1. **Credentials**: Store GitHub tokens securely in Jenkins Credentials
2. **Secrets**: Use Jenkins Credentials for deployment tokens (VERCEL_TOKEN)
3. **Webhook Secret**: Use webhook secrets to verify requests
4. **Permissions**: Use least-privilege GitHub tokens

## Monitoring

### View All Branch Builds

- Go to Multibranch Pipeline job
- View all branch jobs in the list
- Each branch has its own build history

### View Build Status

- Green: All stages passed
- Yellow: Some stages unstable (warnings)
- Red: Build failed
- Blue: Build in progress

### View Reports

- **Coverage Report**: Click on build → Coverage Report
- **E2E Report**: Click on build → E2E Test Report
- **Test Summary**: Download `test-summary.md` artifact

## Next Steps

1. Configure deployment credentials (VERCEL_TOKEN)
2. Set up email/Slack notifications
3. Configure build retention policies
4. Set up monitoring and alerts
5. Customize pipeline stages as needed
