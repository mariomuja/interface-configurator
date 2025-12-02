# Jenkins Stages Usage Examples

## How to Use the Stages Configuration

The `stages-config.groovy` file provides reusable stage functions that can be used in your Jenkinsfile.

## Basic Usage

### Load the Stages

```groovy
def stages = load 'jenkins/stages-config.groovy'
```

### Use Individual Stages

```groovy
pipeline {
    agent any
    stages {
        script {
            stages.checkout()
            stages.setupEnvironment()
            stages.installDependencies()
            stages.build()
            stages.unitTests()
        }
    }
}
```

## Complete Pipeline Example

```groovy
def stages = load 'jenkins/stages-config.groovy'

pipeline {
    agent any
    
    options {
        timeout(time: 30, unit: 'MINUTES')
        timestamps()
    }
    
    environment {
        COVERAGE_THRESHOLD = '70'
    }
    
    stages {
        script {
            stages.checkout()
            stages.setupEnvironment()
            stages.installDependencies()
            stages.lint()
            stages.build()
            stages.unitTests()
            stages.e2eTests()
            stages.testMetrics()
            stages.securityScan()
            stages.generateTestReport()
        }
    }
    
    post {
        success {
            script {
                stages.notification('SUCCESS')
            }
        }
        failure {
            script {
                stages.notification('FAILURE')
            }
        }
        always {
            script {
                stages.cleanup()
            }
        }
    }
}
```

## Available Stages

### 1. checkout()
Extracts git information and checks out code.

```groovy
stages.checkout()
```

**Sets Environment Variables:**
- `GIT_COMMIT_SHORT`
- `GIT_BRANCH`
- `GIT_COMMIT`
- `GIT_AUTHOR`
- `GIT_MESSAGE`

### 2. setupEnvironment()
Verifies Node.js, NPM, and Git versions.

```groovy
stages.setupEnvironment()
```

**Sets Environment Variables:**
- `NODE_VERSION`
- `NPM_VERSION`

### 3. installDependencies()
Installs npm dependencies.

```groovy
stages.installDependencies()
```

### 4. lint()
Runs linting and code quality checks.

```groovy
stages.lint()
```

### 5. build()
Builds the application and archives artifacts.

```groovy
stages.build()
```

**Sets Environment Variables:**
- `BUILD_TIME`

### 6. unitTests()
Runs unit tests with coverage checking.

```groovy
stages.unitTests()
```

**Sets Environment Variables:**
- `COVERAGE_STATEMENTS`
- `COVERAGE_BRANCHES`
- `COVERAGE_FUNCTIONS`
- `COVERAGE_LINES`

**Publishes:**
- HTML coverage report
- LCOV coverage data

### 7. e2eTests()
Runs E2E tests with Playwright.

```groovy
stages.e2eTests()
```

**Publishes:**
- HTML E2E report
- JUnit test results
- Screenshots and videos

### 8. visualRegression()
Runs visual regression tests.

```groovy
stages.visualRegression()
```

**Archives:**
- Screenshot artifacts

### 9. testMetrics()
Generates test metrics and summary.

```groovy
stages.testMetrics()
```

**Creates:**
- `test-summary.md` file

### 10. securityScan()
Runs security vulnerability scan.

```groovy
stages.securityScan()
```

### 11. performanceTest()
Runs performance tests.

```groovy
stages.performanceTest()
```

### 12. accessibilityTest()
Runs accessibility tests.

```groovy
stages.accessibilityTest()
```

### 13. deploy(environment)
Deploys to specified environment.

```groovy
stages.deploy('production')
stages.deploy('preview')
```

**Sets Environment Variables:**
- `DEPLOY_URL` (on success)

### 14. cleanup()
Cleans up workspace.

```groovy
stages.cleanup()
```

### 15. notification(status)
Sends notifications.

```groovy
stages.notification('SUCCESS')
stages.notification('FAILURE')
```

### 16. generateTestReport()
Generates comprehensive test report.

```groovy
stages.generateTestReport()
```

**Creates:**
- `test-report.md` file

## Conditional Usage

### Run E2E Only on Main/Develop

```groovy
stage('E2E Tests') {
    when {
        anyOf {
            branch 'main'
            branch 'develop'
        }
    }
    steps {
        script {
            stages.e2eTests()
        }
    }
}
```

### Deploy Based on Branch

```groovy
stage('Deploy') {
    when {
        branch 'main'
    }
    steps {
        script {
            stages.deploy('production')
        }
    }
}
```

## Parallel Execution

```groovy
stage('Tests') {
    parallel {
        stage('Unit') {
            steps {
                script {
                    stages.unitTests()
                }
            }
        }
        stage('E2E') {
            steps {
                script {
                    stages.e2eTests()
                }
            }
        }
    }
}
```

## Error Handling

All stages include error handling and will not fail the pipeline if optional steps fail (like linting, security scans, etc.).

## Integration with CI Scripts

The stages integrate with our CI scripts:

```groovy
stage('Run Tests') {
    steps {
        sh './scripts/test-ci.sh'
    }
}
```

## Best Practices

1. **Always use script block** when calling stages:
   ```groovy
   script {
       stages.checkout()
   }
   ```

2. **Set environment variables** before using stages:
   ```groovy
   environment {
       COVERAGE_THRESHOLD = '70'
   }
   ```

3. **Use conditional stages** for branch-specific logic:
   ```groovy
   when {
       branch 'main'
   }
   ```

4. **Combine with parallel execution** for faster builds:
   ```groovy
   parallel {
       stage('Unit') { /* ... */ }
       stage('E2E') { /* ... */ }
   }
   ```
