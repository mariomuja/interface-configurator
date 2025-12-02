# Jenkins Pipeline Configuration

This directory contains Jenkins pipeline configurations for the Interface Configurator application.

## Files

- **`Jenkinsfile`** - Full-featured pipeline with all stages
- **`Jenkinsfile.multibranch`** - Multi-branch pipeline configuration
- **`jenkins/Jenkinsfile.simple`** - Minimal pipeline for quick setup
- **`jenkins/stages-config.groovy`** - Reusable stage definitions
- **`jenkins/JENKINS_PIPELINE_GUIDE.md`** - Comprehensive guide
- **`jenkins/docker-agent/Dockerfile`** - Custom Jenkins agent with Node.js and Playwright

## Quick Start

1. Copy `Jenkinsfile` to your repository root
2. Create a Pipeline job in Jenkins
3. Point to the Jenkinsfile
4. Run the pipeline

## Documentation

See `JENKINS_PIPELINE_GUIDE.md` for detailed documentation.
