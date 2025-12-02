# Jenkins Pipeline Configuration

This directory contains Jenkins pipeline configurations and utilities for the Interface Configurator application.

## Main Pipeline

- **`Jenkinsfile`** (in repository root) - Single unified pipeline that runs automatically for branches starting with `ready/`

## Supporting Files

- **`jenkins/stages-config.groovy`** - Reusable stage definitions (optional, for advanced use)
- **`jenkins/JENKINS_SETUP.md`** - Complete setup guide for multibranch pipeline
- **`jenkins/BRANCH_NAMING.md`** - Branch naming convention guide (branches must start with `ready/`)
- **`jenkins/JENKINS_PIPELINE_GUIDE.md`** - Comprehensive pipeline documentation
- **`jenkins/docker-agent/Dockerfile`** - Custom Jenkins agent with Node.js and Playwright
- **`jenkins/Jenkinsfile.simple`** - Minimal pipeline example (for reference)
- **`jenkins/Jenkinsfile.using-stages`** - Example using stages-config.groovy (for reference)
- **`jenkins/USAGE_EXAMPLES.md`** - Examples for using stages-config.groovy
- **`jenkins/example-pipelines.md`** - Additional pipeline examples

## Quick Start

### For Multibranch Pipeline (Recommended)

1. Ensure `Jenkinsfile` is in your repository root
2. In Jenkins, create a **Multibranch Pipeline** job
3. Configure GitHub as branch source
4. **Set branch filter**: `^ready/.*` (only branches starting with `ready/`)
5. Set up GitHub webhook for automatic triggers
6. Pipeline will automatically run for branches matching `ready/*`

See **`jenkins/JENKINS_SETUP.md`** for detailed setup instructions.

### For Standard Pipeline

1. Ensure `Jenkinsfile` is in your repository root
2. Create a **Pipeline** job in Jenkins
3. Point to the Jenkinsfile
4. Run manually or configure triggers

## Key Features

- ✅ **Selective branch detection** - Runs only for branches starting with `ready/`
- ✅ **Branch validation** - Pipeline fails early if branch doesn't match pattern
- ✅ **Branch-specific stages** - Adapts behavior based on branch name and PR target
- ✅ **Comprehensive testing** - Unit, E2E, visual regression, accessibility
- ✅ **Coverage enforcement** - Enforced on all `ready/*` branches
- ✅ **Artifact publishing** - Builds, reports, coverage
- ✅ **Deployment** - Automatic preview/production deployment based on PR target

## Documentation

- **Setup Guide**: `jenkins/JENKINS_SETUP.md` - How to configure multibranch pipeline
- **Branch Naming**: `jenkins/BRANCH_NAMING.md` - Branch naming convention (`ready/*` required)
- **Pipeline Guide**: `jenkins/JENKINS_PIPELINE_GUIDE.md` - Detailed pipeline documentation
- **Usage Examples**: `jenkins/USAGE_EXAMPLES.md` - Examples for advanced usage

## Branch Naming Convention

**Important**: The pipeline runs **only for branches starting with `ready/`**.

- ✅ Valid: `ready/feature-123`, `ready/bugfix-456`, `ready/hotfix-789`
- ❌ Invalid: `feature-123`, `bugfix-456`, `develop`, `main`

See `jenkins/BRANCH_NAMING.md` for complete details.
