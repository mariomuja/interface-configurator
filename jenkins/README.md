# Jenkins Pipeline Configuration

This directory contains Jenkins pipeline configurations and utilities for the Interface Configurator application.

## Main Pipeline

- **`Jenkinsfile`** (in repository root) - Single unified pipeline that runs automatically for every GitHub branch

## Supporting Files

- **`jenkins/stages-config.groovy`** - Reusable stage definitions (optional, for advanced use)
- **`jenkins/JENKINS_SETUP.md`** - Complete setup guide for multibranch pipeline
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
4. Set up GitHub webhook for automatic triggers
5. Pipeline will automatically run for every branch

See **`jenkins/JENKINS_SETUP.md`** for detailed setup instructions.

### For Standard Pipeline

1. Ensure `Jenkinsfile` is in your repository root
2. Create a **Pipeline** job in Jenkins
3. Point to the Jenkinsfile
4. Run manually or configure triggers

## Key Features

- ✅ **Automatic branch detection** - Runs for every GitHub branch
- ✅ **Branch-specific stages** - Adapts behavior based on branch name
- ✅ **Comprehensive testing** - Unit, E2E, visual regression, accessibility
- ✅ **Coverage enforcement** - Enforced on main/develop branches
- ✅ **Artifact publishing** - Builds, reports, coverage
- ✅ **Deployment** - Automatic preview/production deployment

## Documentation

- **Setup Guide**: `jenkins/JENKINS_SETUP.md` - How to configure multibranch pipeline
- **Pipeline Guide**: `jenkins/JENKINS_PIPELINE_GUIDE.md` - Detailed pipeline documentation
- **Usage Examples**: `jenkins/USAGE_EXAMPLES.md` - Examples for advanced usage
