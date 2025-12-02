# Jenkins Pipeline Guide

## Overview

This guide provides Jenkins pipeline configurations for the Interface Configurator application, leveraging all the test infrastructure we've built.

## Pipeline Files

### 1. `Jenkinsfile` - Full Featured Pipeline
Complete pipeline with all stages including:
- Checkout and environment setup
- Dependency installation
- Linting and code quality
- Build
- Unit tests with coverage
- E2E tests
- Test metrics
- Security scanning
- Deployment

### 2. `Jenkinsfile.multibranch` - Multi-Branch Pipeline
Supports multiple branches with conditional stages:
- Feature branches: Unit tests only
- Develop branch: Unit + E2E tests
- Main branch: All tests + Visual regression + Production deploy

### 3. `jenkins/Jenkinsfile.simple` - Minimal Pipeline
Quick setup for basic CI/CD needs.

### 4. `jenkins/stages-config.groovy` - Reusable Stages
Modular stage definitions for custom pipelines.

## Quick Start

### Option 1: Use Existing Jenkinsfile

1. **Create Jenkins Pipeline Job:**
   - New Item → Pipeline
   - Pipeline → Definition: Pipeline script from SCM
   - SCM: Git
   - Repository URL: Your repo URL
   - Script Path: `Jenkinsfile`

2. **Configure Environment Variables:**
   ```
   COVERAGE_THRESHOLD=70
   PLAYWRIGHT_BASE_URL=http://localhost:4200
   ```

3. **Run Pipeline:**
   - Click "Build Now"

### Option 2: Multi-Branch Pipeline

1. **Create Multi-Branch Pipeline:**
   - New Item → Multibranch Pipeline
   - Branch Sources → Git
   - Repository URL: Your repo URL
   - Script Path: `Jenkinsfile.multibranch`

2. **Configure:**
   - Scan interval: Daily or on webhook
   - Build strategies: All branches

3. **Jenkins will automatically:**
   - Discover branches
   - Run appropriate tests per branch
   - Deploy based on branch

## Pipeline Stages Explained

### Stage 1: Checkout
```groovy
stage('Checkout') {
    steps {
        checkout scm
        // Get commit hash and branch name
    }
}
```

### Stage 2: Install Dependencies
```groovy
stage('Install Dependencies') {
    steps {
        sh 'cd frontend && npm ci'
    }
}
```

### Stage 3: Build
```groovy
stage('Build') {
    steps {
        sh 'cd frontend && npm run build'
    }
    post {
        always {
            archiveArtifacts 'frontend/dist/**/*'
        }
    }
}
```

### Stage 4: Unit Tests
```groovy
stage('Unit Tests') {
    parallel {
        stage('Run Tests') {
            steps {
                sh 'cd frontend && npm test -- --code-coverage'
            }
        }
        stage('Check Coverage') {
            steps {
                // Check coverage thresholds
            }
        }
    }
    post {
        always {
            publishHTML([
                reportDir: 'frontend/coverage/interface-configurator',
                reportFiles: 'index.html',
                reportName: 'Coverage Report'
            ])
        }
    }
}
```

### Stage 5: E2E Tests
```groovy
stage('E2E Tests') {
    steps {
        sh 'npx playwright install chromium'
        sh 'npm run test:e2e'
    }
    post {
        always {
            publishHTML([
                reportDir: 'playwright-report',
                reportFiles: 'index.html',
                reportName: 'E2E Test Report'
            ])
            junit 'test-results/junit.xml'
        }
    }
}
```

## Advanced Configuration

### Parallel Execution
```groovy
stage('Tests') {
    parallel {
        stage('Unit Tests') { /* ... */ }
        stage('E2E Tests') { /* ... */ }
        stage('Lint') { /* ... */ }
    }
}
```

### Conditional Stages
```groovy
stage('Deploy Production') {
    when {
        branch 'main'
    }
    steps {
        sh 'vercel deploy --prod'
    }
}
```

### Coverage Threshold Enforcement
```groovy
script {
    def coverage = readJSON file: 'coverage/coverage-summary.json'
    if (coverage.total.lines.pct < 70) {
        error("Coverage below threshold!")
    }
}
```

## Required Jenkins Plugins

Install these plugins in Jenkins:

1. **Pipeline** - Core pipeline support
2. **HTML Publisher** - Publish HTML reports
3. **JUnit** - Publish test results
4. **Coverage** - Code coverage visualization
5. **AnsiColor** - Colored console output
6. **Timestamper** - Build timestamps
7. **Git** - Git integration
8. **NodeJS** - Node.js support (optional)

## Environment Variables

Configure in Jenkins → Manage Jenkins → Configure System:

```
NODE_VERSION=22.0.0
NPM_VERSION=10.0.0
COVERAGE_THRESHOLD=70
PLAYWRIGHT_BASE_URL=http://localhost:4200
```

## Webhook Configuration

### GitHub Webhook
1. GitHub repo → Settings → Webhooks
2. Add webhook:
   - Payload URL: `https://your-jenkins-url/github-webhook/`
   - Content type: `application/json`
   - Events: Push, Pull Request

### GitLab Webhook
1. GitLab repo → Settings → Webhooks
2. Add webhook:
   - URL: `https://your-jenkins-url/project/your-project`
   - Trigger: Push events, Merge request events

## Notification Configuration

### Email Notifications
```groovy
post {
    failure {
        emailext(
            subject: "Pipeline Failed: ${env.JOB_NAME}",
            body: "Build failed. Check: ${env.BUILD_URL}",
            to: "devops@example.com"
        )
    }
}
```

### Slack Notifications
```groovy
post {
    failure {
        slackSend(
            channel: '#devops',
            color: 'danger',
            message: "Pipeline failed: ${env.BUILD_URL}"
        )
    }
}
```

## Best Practices

### 1. Use Parallel Execution
Run independent stages in parallel to reduce build time:
```groovy
parallel {
    stage('Unit Tests') { /* ... */ }
    stage('E2E Tests') { /* ... */ }
}
```

### 2. Archive Artifacts
Always archive build artifacts and test reports:
```groovy
archiveArtifacts artifacts: 'frontend/dist/**/*'
```

### 3. Clean Workspace
Clean workspace in post-always to save disk space:
```groovy
post {
    always {
        cleanWs()
    }
}
```

### 4. Timeout Protection
Set timeouts to prevent hanging builds:
```groovy
options {
    timeout(time: 30, unit: 'MINUTES')
}
```

### 5. Build Retention
Configure build retention:
```groovy
options {
    buildDiscarder(logRotator(numToKeepStr: '10'))
}
```

## Troubleshooting

### Issue: Tests Timeout
**Solution:** Increase timeout in pipeline options:
```groovy
options {
    timeout(time: 60, unit: 'MINUTES')
}
```

### Issue: Coverage Not Found
**Solution:** Check coverage file path:
```groovy
def coverageFile = 'frontend/coverage/interface-configurator/coverage-summary.json'
```

### Issue: Playwright Browsers Not Installing
**Solution:** Add installation step:
```groovy
sh 'npx playwright install --with-deps chromium'
```

### Issue: Node Version Mismatch
**Solution:** Use NodeJS plugin or nvm:
```groovy
sh '''
    source ~/.nvm/nvm.sh
    nvm use 22.0.0
    npm test
'''
```

## Example: Custom Pipeline

```groovy
@Library('shared-library') _

pipeline {
    agent any
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }
        
        stage('Test') {
            steps {
                sh './scripts/test-ci.sh'
            }
        }
        
        stage('Deploy') {
            when {
                branch 'main'
            }
            steps {
                sh 'vercel deploy --prod'
            }
        }
    }
}
```

## Integration with Test Scripts

The Jenkins pipelines integrate with our CI/CD scripts:

```groovy
stage('Run Tests') {
    steps {
        sh './scripts/test-ci.sh'
    }
}
```

This uses:
- `scripts/test-ci.sh` - Bash script
- `scripts/test-ci.ps1` - PowerShell script (Windows agents)

## Monitoring & Metrics

### Build Trends
Jenkins automatically tracks:
- Build duration trends
- Test result trends
- Coverage trends

### Custom Metrics
Add custom metrics collection:
```groovy
script {
    def duration = currentBuild.duration
    def coverage = env.COVERAGE_LINES
    // Send to monitoring system
}
```

## Security Considerations

1. **Credentials Management:**
   - Use Jenkins Credentials Store
   - Never hardcode secrets
   - Use environment variables

2. **Pipeline Security:**
   - Use script approval
   - Limit pipeline permissions
   - Review pipeline scripts

3. **Test Data:**
   - Don't use production data
   - Use test data generators
   - Clean up after tests

## Next Steps

1. **Set up Jenkins:**
   - Install Jenkins
   - Install required plugins
   - Configure Node.js

2. **Create Pipeline:**
   - Use `Jenkinsfile` or `Jenkinsfile.multibranch`
   - Configure webhooks
   - Set up notifications

3. **Monitor:**
   - Check build status
   - Review test reports
   - Monitor coverage trends

4. **Optimize:**
   - Add parallel execution
   - Cache dependencies
   - Optimize test execution

---

For more information, see:
- [Jenkins Pipeline Documentation](https://www.jenkins.io/doc/book/pipeline/)
- [Test Infrastructure Guide](./TESTING_GUIDE.md)
- [CI/CD Scripts](../scripts/test-ci.sh)
