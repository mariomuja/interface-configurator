# Jenkins Pipeline Examples

## Example 1: Basic Pipeline Using Stages

```groovy
def stages = load 'jenkins/stages-config.groovy'

pipeline {
    agent any
    stages {
        stage('Checkout') {
            steps { script { stages.checkout() } }
        }
        stage('Install') {
            steps { script { stages.installDependencies() } }
        }
        stage('Build') {
            steps { script { stages.build() } }
        }
        stage('Test') {
            steps { script { stages.unitTests() } }
        }
    }
}
```

## Example 2: Feature Branch Pipeline

```groovy
def stages = load 'jenkins/stages-config.groovy'

pipeline {
    agent any
    stages {
        stage('Checkout') {
            steps { script { stages.checkout() } }
        }
        stage('Install') {
            steps { script { stages.installDependencies() } }
        }
        stage('Build') {
            steps { script { stages.build() } }
        }
        stage('Unit Tests') {
            steps { script { stages.unitTests() } }
        }
        // Skip E2E on feature branches for faster feedback
    }
}
```

## Example 3: Main Branch Pipeline

```groovy
def stages = load 'jenkins/stages-config.groovy'

pipeline {
    agent any
    stages {
        stage('Checkout') {
            steps { script { stages.checkout() } }
        }
        stage('Install') {
            steps { script { stages.installDependencies() } }
        }
        stage('Build') {
            steps { script { stages.build() } }
        }
        stage('Tests') {
            parallel {
                stage('Unit') {
                    steps { script { stages.unitTests() } }
                }
                stage('E2E') {
                    steps { script { stages.e2eTests() } }
                }
                stage('Visual') {
                    steps { script { stages.visualRegression() } }
                }
            }
        }
        stage('Deploy') {
            steps { script { stages.deploy('production') } }
        }
    }
}
```

## Example 4: Parallel Execution

```groovy
def stages = load 'jenkins/stages-config.groovy'

pipeline {
    agent any
    stages {
        stage('Checkout') {
            steps { script { stages.checkout() } }
        }
        stage('Install') {
            steps { script { stages.installDependencies() } }
        }
        stage('Build & Test') {
            parallel {
                stage('Build') {
                    steps { script { stages.build() } }
                }
                stage('Unit Tests') {
                    steps { script { stages.unitTests() } }
                }
            }
        }
        stage('E2E & Quality') {
            parallel {
                stage('E2E') {
                    steps { script { stages.e2eTests() } }
                }
                stage('Security') {
                    steps { script { stages.securityScan() } }
                }
            }
        }
    }
}
```

## Example 5: Conditional Stages

```groovy
def stages = load 'jenkins/stages-config.groovy'

pipeline {
    agent any
    stages {
        stage('Checkout') {
            steps { script { stages.checkout() } }
        }
        stage('Install') {
            steps { script { stages.installDependencies() } }
        }
        stage('Build') {
            steps { script { stages.build() } }
        }
        stage('Unit Tests') {
            steps { script { stages.unitTests() } }
        }
        stage('E2E Tests') {
            when {
                expression { 
                    env.BRANCH_NAME == 'main' || 
                    env.BRANCH_NAME == 'develop' ||
                    env.CHANGE_TARGET in ['main', 'develop']
                }
            }
            steps { script { stages.e2eTests() } }
        }
        stage('Deploy') {
            when {
                branch 'main'
            }
            steps { script { stages.deploy('production') } }
        }
    }
}
```
