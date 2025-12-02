// Jenkins Pipeline Stages Configuration
// Reusable stage definitions for Jenkins pipelines

/**
 * Checkout stage
 */
def checkoutStage() {
    return {
        stage('Checkout') {
            steps {
                checkout scm
                script {
                    env.GIT_COMMIT_SHORT = sh(
                        script: 'git rev-parse --short HEAD',
                        returnStdout: true
                    ).trim()
                    env.GIT_BRANCH = sh(
                        script: 'git rev-parse --abbrev-ref HEAD',
                        returnStdout: true
                    ).trim()
                }
            }
        }
    }
}

/**
 * Install dependencies stage
 */
def installDependenciesStage() {
    return {
        stage('Install Dependencies') {
            steps {
                sh '''
                    cd frontend
                    npm ci --prefer-offline --no-audit
                '''
            }
        }
    }
}

/**
 * Build stage
 */
def buildStage() {
    return {
        stage('Build') {
            steps {
                sh '''
                    cd frontend
                    npm run build
                '''
                archiveArtifacts artifacts: 'frontend/dist/**/*', fingerprint: true
            }
        }
    }
}

/**
 * Unit tests stage with coverage
 */
def unitTestsStage() {
    return {
        stage('Unit Tests') {
            parallel {
                stage('Run Tests') {
                    steps {
                        sh '''
                            cd frontend
                            npm test -- --code-coverage --browsers=ChromeHeadless --watch=false
                        '''
                    }
                }
                stage('Check Coverage') {
                    steps {
                        script {
                            dir('frontend') {
                                def coverageFile = 'coverage/interface-configurator/coverage-summary.json'
                                if (fileExists(coverageFile)) {
                                    def coverage = readJSON file: coverageFile
                                    def statements = coverage.total.statements.pct
                                    def branches = coverage.total.branches.pct
                                    def functions = coverage.total.functions.pct
                                    def lines = coverage.total.lines.pct
                                    
                                    // Store coverage
                                    env.COVERAGE_STATEMENTS = statements.toString()
                                    env.COVERAGE_BRANCHES = branches.toString()
                                    env.COVERAGE_FUNCTIONS = functions.toString()
                                    env.COVERAGE_LINES = lines.toString()
                                    
                                    // Check thresholds
                                    def threshold = env.COVERAGE_THRESHOLD ?: '70'
                                    if (statements < threshold.toFloat() ||
                                        branches < 65 ||
                                        functions < threshold.toFloat() ||
                                        lines < threshold.toFloat()) {
                                        error("Coverage thresholds not met!")
                                    }
                                }
                            }
                        }
                    }
                }
            }
            post {
                always {
                    publishHTML([
                        reportDir: 'frontend/coverage/interface-configurator',
                        reportFiles: 'index.html',
                        reportName: 'Coverage Report',
                        keepAll: true
                    ])
                }
            }
        }
    }
}

/**
 * E2E tests stage
 */
def e2eTestsStage() {
    return {
        stage('E2E Tests') {
            steps {
                sh '''
                    npx playwright install --with-deps chromium
                    npm run test:e2e
                '''
            }
            post {
                always {
                    publishHTML([
                        reportDir: 'playwright-report',
                        reportFiles: 'index.html',
                        reportName: 'E2E Test Report',
                        keepAll: true
                    ])
                    junit 'test-results/junit.xml'
                }
            }
        }
    }
}

/**
 * Visual regression tests stage
 */
def visualRegressionStage() {
    return {
        stage('Visual Regression') {
            steps {
                sh 'npx playwright test e2e/visual-regression.spec.ts'
            }
            post {
                always {
                    archiveArtifacts artifacts: 'test-results/**/*.png', allowEmptyArchive: true
                }
            }
        }
    }
}

/**
 * Security scan stage
 */
def securityScanStage() {
    return {
        stage('Security Scan') {
            steps {
                sh '''
                    cd frontend
                    npm audit --audit-level=moderate || true
                '''
            }
        }
    }
}

/**
 * Deploy stage
 */
def deployStage(environment) {
    return {
        stage("Deploy ${environment}") {
            steps {
                script {
                    if (environment == 'production') {
                        sh 'vercel deploy --prod'
                    } else if (environment == 'preview') {
                        sh 'vercel deploy --preview'
                    }
                }
            }
        }
    }
}

// Export functions for use in Jenkinsfile
return [
    checkout: checkoutStage,
    installDependencies: installDependenciesStage,
    build: buildStage,
    unitTests: unitTestsStage,
    e2eTests: e2eTestsStage,
    visualRegression: visualRegressionStage,
    securityScan: securityScanStage,
    deploy: deployStage
]
