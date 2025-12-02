# Jenkins Multibranch Pipeline Setup Guide

This guide explains how to configure Jenkins to automatically run the pipeline for every branch pushed to GitHub.

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
     - Add **Discover pull requests** → Strategy: **Merging the pull request with the current target branch revision**
     - Add **Filter by name** (optional): Exclude branches like `*-wip`, `*-temp`

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

1. **Initial Scan**: Jenkins scans the repository and creates jobs for all existing branches
2. **New Branch Detection**: When a new branch is pushed to GitHub:
   - GitHub webhook triggers Jenkins
   - Jenkins scans the repository
   - A new job is created for the branch
   - Pipeline runs automatically

### Branch-Specific Behavior

The pipeline automatically adapts based on branch name:

- **All Branches**: Run checkout, install, lint, build, unit tests, security scan
- **main/develop/master**: Additionally run E2E tests, accessibility tests
- **main only**: Additionally run visual regression tests
- **develop**: Deploy preview
- **main**: Deploy production

### Pull Request Support

When a PR is opened targeting `main` or `develop`:
- Pipeline runs automatically
- E2E tests are included
- Results are visible in GitHub (if GitHub integration is configured)

## Testing the Setup

### Test 1: Create a New Branch

```bash
git checkout -b test/jenkins-pipeline
git push origin test/jenkins-pipeline
```

Expected: Jenkins should detect the new branch and run the pipeline.

### Test 2: Push Changes to Existing Branch

```bash
git checkout test/jenkins-pipeline
# Make a change
git commit -am "Test Jenkins pipeline"
git push
```

Expected: Jenkins should trigger a new build for the branch.

### Test 3: Create a Pull Request

1. Create a PR from `test/jenkins-pipeline` to `main`
2. Expected: Jenkins should run the pipeline with E2E tests included

## Troubleshooting

### Pipeline Not Running for New Branches

1. **Check Webhook**: Verify GitHub webhook is configured and active
2. **Check Scan**: Manually trigger "Scan Multibranch Pipeline Now" in Jenkins
3. **Check Logs**: View "Scan Multibranch Pipeline" logs in Jenkins
4. **Check Credentials**: Verify GitHub credentials are valid

### Builds Not Triggering on Push

1. **Check Webhook Delivery**: In GitHub → Settings → Webhooks → Recent Deliveries
2. **Check Jenkins Logs**: Jenkins → Manage Jenkins → System Log
3. **Verify URL**: Ensure webhook URL matches your Jenkins instance
4. **Check Permissions**: Ensure GitHub token has `repo` scope

### Coverage Thresholds Failing

- Coverage thresholds are only enforced on `main`, `develop`, and `master` branches
- Feature branches will show coverage but won't fail the build
- Adjust `COVERAGE_THRESHOLD` in Jenkinsfile if needed

### E2E Tests Not Running

- E2E tests only run on `main`, `develop`, `master` branches and PRs targeting them
- This is intentional to speed up builds on feature branches
- To run E2E on all branches, remove the `when` condition in the E2E stage

## Advanced Configuration

### Custom Branch Patterns

To include/exclude specific branch patterns, add to **Behaviors**:

```
Filter by name (with regular expression)
- Include: ^(main|develop|feature/.*|bugfix/.*)$
- Exclude: ^(wip/.*|temp/.*)$
```

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
