# Branch Naming Convention

## Overview

The Jenkins pipeline is configured to run **only for branches whose name starts with `ready/`**.

## Branch Naming Rules

### ✅ Valid Branch Names (Will Trigger Pipeline)

- `ready/feature-123`
- `ready/bugfix-456`
- `ready/hotfix-789`
- `ready/update-dependencies`
- `ready/refactor-auth-service`
- `ready/feature/user-authentication`
- `ready/release/v1.2.0`

### ❌ Invalid Branch Names (Will NOT Trigger Pipeline)

- `feature-123` (missing `ready/` prefix)
- `bugfix-456` (missing `ready/` prefix)
- `develop` (doesn't start with `ready/`)
- `main` (doesn't start with `ready/`)
- `wip/feature-123` (wrong prefix)

## Creating a Branch

### Example: Creating a Feature Branch

```bash
# Create and switch to new branch
git checkout -b ready/feature-user-dashboard

# Make changes and commit
git add .
git commit -m "Add user dashboard feature"

# Push to GitHub
git push origin ready/feature-user-dashboard
```

**Result**: Jenkins will automatically detect the branch and run the pipeline.

### Example: Creating a Bugfix Branch

```bash
git checkout -b ready/bugfix-login-error
git add .
git commit -m "Fix login error handling"
git push origin ready/bugfix-login-error
```

**Result**: Jenkins will automatically detect the branch and run the pipeline.

## Branch Workflow

1. **Create branch** with `ready/` prefix
2. **Push to GitHub** → Jenkins automatically runs pipeline
3. **Pipeline validates** code quality, tests, coverage
4. **Create Pull Request** → Pipeline runs again with additional tests
5. **Merge to main/develop** → Deployment stages run

## Why This Convention?

- **Clear Intent**: Branches prefixed with `ready/` indicate they're ready for CI/CD validation
- **Selective Building**: Reduces unnecessary builds for work-in-progress branches
- **Resource Efficiency**: Only builds branches that are ready for review/merge
- **Quality Gate**: Ensures only validated code reaches main branches

## Changing the Convention

If you need to change the branch naming convention:

1. **Update Jenkins Job Configuration**:
   - Branch Source → Behaviors → Filter by name
   - Change pattern from `^ready/.*` to your desired pattern

2. **Update Jenkinsfile**:
   - Modify the branch validation check in the Checkout stage
   - Update all `when` conditions that check branch names

3. **Update Documentation**:
   - Update this file and `JENKINS_SETUP.md`

## Examples

### Feature Development

```bash
# Start feature work
git checkout -b ready/feature-payment-integration

# Work on feature...
git commit -am "Add payment gateway integration"

# Push - triggers Jenkins pipeline
git push origin ready/feature-payment-integration

# Create PR from ready/feature-payment-integration to main
# Jenkins runs full test suite including E2E tests
```

### Bug Fix

```bash
# Create bugfix branch
git checkout -b ready/bugfix-memory-leak

# Fix the bug
git commit -am "Fix memory leak in data processing"

# Push - triggers Jenkins pipeline
git push origin ready/bugfix-memory-leak
```

### Hotfix

```bash
# Create hotfix branch
git checkout -b ready/hotfix-security-patch

# Apply hotfix
git commit -am "Apply critical security patch"

# Push - triggers Jenkins pipeline
git push origin ready/hotfix-security-patch
```

## Troubleshooting

### Pipeline Not Running?

1. **Check branch name**: Does it start with `ready/`?
2. **Check Jenkins scan**: Manually trigger "Scan Multibranch Pipeline Now"
3. **Check branch filter**: Verify filter pattern in Jenkins job configuration
4. **Check logs**: View "Scan Multibranch Pipeline" logs

### Renaming a Branch

If you need to rename a branch:

```bash
# Rename locally
git branch -m old-branch-name ready/new-branch-name

# Push new branch
git push origin ready/new-branch-name

# Delete old branch on remote
git push origin --delete old-branch-name
```

**Note**: Jenkins will automatically detect the new branch name and create a new job for it.
