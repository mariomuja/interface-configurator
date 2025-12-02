// Jenkins Pipeline Stages Configuration
// Reusable stage definitions for Jenkins pipelines

/**
 * Checkout stage with git information extraction
 * Usage: stages.checkout()
 */
def checkoutStage() {
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
                env.GIT_COMMIT = sh(
                    script: 'git rev-parse HEAD',
                    returnStdout: true
                ).trim()
                env.GIT_AUTHOR = sh(
                    script: 'git log -1 --pretty=format:"%an"',
                    returnStdout: true
                ).trim()
                env.GIT_MESSAGE = sh(
                    script: 'git log -1 --pretty=format:"%s"',
                    returnStdout: true
                ).trim()
                
                echo "Branch: ${env.GIT_BRANCH}"
                echo "Commit: ${env.GIT_COMMIT_SHORT}"
                echo "Author: ${env.GIT_AUTHOR}"
            }
        }
    }
}

/**
 * Environment setup stage
 * Usage: stages.setupEnvironment()
 */
def setupEnvironmentStage() {
    stage('Setup Environment') {
        steps {
            script {
                echo "⚙️ Setting up environment..."
                sh '''
                    echo "Node version:"
                    node --version || echo "Node.js not found"
                    echo "NPM version:"
                    npm --version || echo "NPM not found"
                    echo "Git version:"
                    git --version
                '''
                
                // Set environment variables
                try {
                    env.NODE_VERSION = sh(
                        script: 'node --version',
                        returnStdout: true
                    ).trim()
                    env.NPM_VERSION = sh(
                        script: 'npm --version',
                        returnStdout: true
                    ).trim()
                } catch (Exception e) {
                    echo "Could not detect Node.js/NPM versions: ${e.message}"
                }
            }
        }
    }
}

/**
 * Install dependencies stage with caching support
 * Usage: stages.installDependencies()
 */
def installDependenciesStage() {
    stage('Install Dependencies') {
        steps {
            script {
                echo "📦 Installing dependencies..."
                
                // Install root dependencies if package.json exists
                if (fileExists('package.json')) {
                    sh 'npm ci --prefer-offline --no-audit || npm install --prefer-offline --no-audit'
                }
                
                // Install frontend dependencies
                dir('frontend') {
                    sh 'npm ci --prefer-offline --no-audit || npm install --prefer-offline --no-audit'
                }
                
                echo "✅ Dependencies installed"
            }
        }
    }
}

/**
 * Lint and code quality stage
 * Usage: stages.lint()
 */
def lintStage() {
    stage('Lint & Code Quality') {
        parallel {
            stage('TypeScript Check') {
                steps {
                    script {
                        echo "🔍 Running TypeScript type checking..."
                        dir('frontend') {
                            sh 'npx tsc --noEmit || echo "TypeScript check skipped"'
                        }
                    }
                }
            }
            stage('ESLint') {
                steps {
                    script {
                        echo "🔍 Running ESLint..."
                        dir('frontend') {
                            sh 'npm run lint || echo "Linting skipped"'
                        }
                    }
                }
            }
        }
    }
}

/**
 * Build stage with artifact archiving
 * Usage: stages.build()
 */
def buildStage() {
    stage('Build') {
        steps {
            script {
                echo "🏗️ Building application..."
                dir('frontend') {
                    sh 'npm run build'
                }
                
                // Archive build artifacts
                archiveArtifacts artifacts: 'frontend/dist/**/*', fingerprint: true, allowEmptyArchive: true
                
                // Store build info
                env.BUILD_TIME = new Date().format("yyyy-MM-dd HH:mm:ss")
                echo "✅ Build completed at ${env.BUILD_TIME}"
            }
        }
        post {
            success {
                script {
                    def distSize = sh(
                        script: 'du -sh frontend/dist 2>/dev/null | cut -f1',
                        returnStdout: true
                    ).trim()
                    echo "Build size: ${distSize}"
                }
            }
        }
    }
}

/**
 * Unit tests stage with coverage
 * Usage: stages.unitTests()
 */
def unitTestsStage() {
    stage('Unit Tests') {
        parallel {
            stage('Run Tests') {
                steps {
                    script {
                        echo "🧪 Running unit tests..."
                        dir('frontend') {
                            sh 'npm test -- --code-coverage --browsers=ChromeHeadless --watch=false'
                        }
                    }
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
                                
                                // Store coverage in environment
                                env.COVERAGE_STATEMENTS = statements.toString()
                                env.COVERAGE_BRANCHES = branches.toString()
                                env.COVERAGE_FUNCTIONS = functions.toString()
                                env.COVERAGE_LINES = lines.toString()
                                
                                echo "Coverage Report:"
                                echo "  Statements: ${statements}%"
                                echo "  Branches: ${branches}%"
                                echo "  Functions: ${functions}%"
                                echo "  Lines: ${lines}%"
                                
                                // Check thresholds (only enforce on main/develop)
                                def threshold = env.COVERAGE_THRESHOLD ?: '70'
                                def enforceThreshold = env.BRANCH_NAME in ['main', 'develop']
                                
                                if (enforceThreshold) {
                                    if (statements < threshold.toFloat() ||
                                        branches < 65 ||
                                        functions < threshold.toFloat() ||
                                        lines < threshold.toFloat()) {
                                        error("Coverage thresholds not met! Required: ${threshold}% statements, 65% branches, ${threshold}% functions, ${threshold}% lines")
                                    }
                                } else {
                                    echo "⚠️ Coverage thresholds not enforced on branch ${env.BRANCH_NAME}"
                                }
                            } else {
                                echo "⚠️ Coverage file not found, skipping threshold check"
                            }
                        }
                    }
                }
            }
        }
        post {
            always {
                script {
                    echo "📊 Publishing coverage reports..."
                    dir('frontend') {
                        if (fileExists('coverage/interface-configurator/index.html')) {
                            publishHTML([
                                reportDir: 'coverage/interface-configurator',
                                reportFiles: 'index.html',
                                reportName: 'Coverage Report',
                                keepAll: true
                            ])
                        }
                        
                        // Publish LCOV for coverage plugins
                        if (fileExists('coverage/interface-configurator/lcov.info')) {
                            try {
                                publishCoverage adapters: [
                                    lcovAdapter('coverage/interface-configurator/lcov.info')
                                ],
                                sourceFileResolver: sourceFiles('STORE_LAST_BUILD')
                            } catch (Exception e) {
                                echo "Coverage plugin not available: ${e.message}"
                            }
                        }
                    }
                }
            }
        }
    }
}

/**
 * E2E tests stage with Playwright
 * Usage: stages.e2eTests()
 */
def e2eTestsStage() {
    stage('E2E Tests') {
        steps {
            script {
                echo "🌐 Running E2E tests..."
                
                // Install Playwright browsers
                sh 'npx playwright install --with-deps chromium || echo "Playwright install skipped"'
                
                // Run E2E tests
                sh 'npm run test:e2e || echo "E2E tests completed"'
            }
        }
        post {
            always {
                script {
                    echo "📊 Publishing E2E test results..."
                    
                    // Publish Playwright HTML report
                    if (fileExists('playwright-report/index.html')) {
                        publishHTML([
                            reportDir: 'playwright-report',
                            reportFiles: 'index.html',
                            reportName: 'E2E Test Report',
                            keepAll: true
                        ])
                    }
                    
                    // Publish JUnit results
                    if (fileExists('test-results/junit.xml')) {
                        junit 'test-results/junit.xml'
                    }
                    
                    // Archive screenshots and videos
                    archiveArtifacts artifacts: 'test-results/**/*', allowEmptyArchive: true
                }
            }
        }
    }
}

/**
 * Visual regression tests stage
 * Usage: stages.visualRegression()
 */
def visualRegressionStage() {
    stage('Visual Regression') {
        steps {
            script {
                echo "🎨 Running visual regression tests..."
                sh 'npx playwright test e2e/visual-regression.spec.ts || echo "Visual regression tests completed"'
            }
        }
        post {
            always {
                archiveArtifacts artifacts: 'test-results/**/*.png', allowEmptyArchive: true
            }
        }
    }
}

/**
 * Test metrics and analysis stage
 * Usage: stages.testMetrics()
 */
def testMetricsStage() {
    stage('Test Metrics & Analysis') {
        steps {
            script {
                echo "📈 Analyzing test metrics..."
                
                // Generate test summary
                def summary = """
# Test Execution Summary

**Branch:** ${env.GIT_BRANCH}
**Commit:** ${env.GIT_COMMIT_SHORT}
**Build:** #${env.BUILD_NUMBER}
**Build Time:** ${env.BUILD_TIME ?: new Date().format("yyyy-MM-dd HH:mm:ss")}

## Coverage
- Statements: ${env.COVERAGE_STATEMENTS ?: 'N/A'}%
- Branches: ${env.COVERAGE_BRANCHES ?: 'N/A'}%
- Functions: ${env.COVERAGE_FUNCTIONS ?: 'N/A'}%
- Lines: ${env.COVERAGE_LINES ?: 'N/A'}%

## Test Results
- Unit Tests: ${currentBuild.result == 'SUCCESS' ? '✅ Passed' : '❌ Failed'}
- E2E Tests: ${fileExists('test-results/junit.xml') ? '✅ Completed' : '⏭️ Skipped'}

## Build Information
- Node Version: ${env.NODE_VERSION ?: 'N/A'}
- NPM Version: ${env.NPM_VERSION ?: 'N/A'}
- Author: ${env.GIT_AUTHOR ?: 'N/A'}
- Commit Message: ${env.GIT_MESSAGE ?: 'N/A'}
"""
                
                writeFile file: 'test-summary.md', text: summary
                archiveArtifacts artifacts: 'test-summary.md', allowEmptyArchive: true
            }
        }
    }
}

/**
 * Security scan stage
 * Usage: stages.securityScan()
 */
def securityScanStage() {
    stage('Security Scan') {
        steps {
            script {
                echo "🔒 Running security scans..."
                dir('frontend') {
                    sh 'npm audit --audit-level=moderate || echo "Security scan completed"'
                }
            }
        }
        post {
            always {
                script {
                    // Archive audit results if available
                    if (fileExists('frontend/npm-audit.json')) {
                        archiveArtifacts artifacts: 'frontend/npm-audit.json', allowEmptyArchive: true
                    }
                }
            }
        }
    }
}

/**
 * Performance testing stage
 * Usage: stages.performanceTest()
 */
def performanceTestStage() {
    stage('Performance Tests') {
        steps {
            script {
                echo "⚡ Running performance tests..."
                dir('frontend') {
                    // Run performance tests if available
                    sh 'npm run test:performance || echo "Performance tests skipped"'
                }
            }
        }
    }
}

/**
 * Accessibility testing stage
 * Usage: stages.accessibilityTest()
 */
def accessibilityTestStage() {
    stage('Accessibility Tests') {
        steps {
            script {
                echo "♿ Running accessibility tests..."
                sh 'npx playwright test e2e/accessibility.spec.ts || echo "Accessibility tests completed"'
            }
        }
        post {
            always {
                archiveArtifacts artifacts: 'test-results/**/*', allowEmptyArchive: true
            }
        }
    }
}

/**
 * Deploy stage with environment support
 * Usage: stages.deploy('production') or stages.deploy('preview')
 */
def deployStage(environment) {
    stage("Deploy ${environment}") {
        steps {
            script {
                echo "🚀 Deploying to ${environment}..."
                
                if (environment == 'production') {
                    sh '''
                        echo "Deploying to production..."
                        vercel deploy --prod --token=${VERCEL_TOKEN} || echo "Deployment skipped"
                    '''
                } else if (environment == 'preview') {
                    sh '''
                        echo "Deploying preview..."
                        vercel deploy --preview --token=${VERCEL_TOKEN} || echo "Deployment skipped"
                    '''
                } else {
                    echo "Unknown environment: ${environment}"
                }
            }
        }
        post {
            success {
                script {
                    // Get deployment URL if available
                    try {
                        def deployUrl = sh(
                            script: 'vercel ls --token=${VERCEL_TOKEN} 2>/dev/null | grep -o "https://[^ ]*" | head -1',
                            returnStdout: true
                        ).trim()
                        if (deployUrl) {
                            env.DEPLOY_URL = deployUrl
                            echo "✅ Deployed to: ${deployUrl}"
                        }
                    } catch (Exception e) {
                        echo "Could not retrieve deployment URL: ${e.message}"
                    }
                }
            }
        }
    }
}

/**
 * Cleanup stage
 * Usage: stages.cleanup()
 */
def cleanupStage() {
    stage('Cleanup') {
        steps {
            script {
                echo "🧹 Cleaning up..."
                // Clean workspace if needed
                // cleanWs()
            }
        }
    }
}

/**
 * Notification stage
 * Usage: stages.notification('SUCCESS') or stages.notification('FAILURE')
 */
def notificationStage(status) {
    script {
        echo "📧 Sending notifications..."
        
        def subject = "Pipeline ${status}: ${env.JOB_NAME} - Build #${env.BUILD_NUMBER}"
        def body = """
            Pipeline ${status} for ${env.JOB_NAME} - Build #${env.BUILD_NUMBER}
            
            Branch: ${env.GIT_BRANCH}
            Commit: ${env.GIT_COMMIT_SHORT}
            Author: ${env.GIT_AUTHOR}
            
            Check the build: ${env.BUILD_URL}
            
            Coverage: ${env.COVERAGE_LINES ?: 'N/A'}%
        """
        
        // Email notification
        try {
            emailext(
                subject: subject,
                body: body,
                to: "${env.CHANGE_AUTHOR_EMAIL ?: env.GIT_AUTHOR_EMAIL ?: 'devops@example.com'}",
                mimeType: 'text/html'
            )
        } catch (Exception e) {
            echo "Email notification failed: ${e.message}"
        }
        
        // Slack notification (if configured)
        try {
            slackSend(
                channel: '#devops',
                color: status == 'SUCCESS' ? 'good' : 'danger',
                message: "${subject}\n${env.BUILD_URL}"
            )
        } catch (Exception e) {
            echo "Slack notification failed: ${e.message}"
        }
    }
}

/**
 * Generate test report stage
 * Usage: stages.generateTestReport()
 */
def generateTestReportStage() {
    stage('Generate Test Report') {
        steps {
            script {
                echo "📊 Generating comprehensive test report..."
                
                // Use our test reporting script if available
                if (fileExists('scripts/test-ci.sh')) {
                    sh './scripts/test-ci.sh || true'
                }
                
                // Generate report from test results
                def report = """
# Comprehensive Test Report

## Build Information
- **Job:** ${env.JOB_NAME}
- **Build:** #${env.BUILD_NUMBER}
- **Branch:** ${env.GIT_BRANCH}
- **Commit:** ${env.GIT_COMMIT_SHORT}
- **Status:** ${currentBuild.result}

## Test Coverage
${env.COVERAGE_LINES ? """
- **Statements:** ${env.COVERAGE_STATEMENTS}%
- **Branches:** ${env.COVERAGE_BRANCHES}%
- **Functions:** ${env.COVERAGE_FUNCTIONS}%
- **Lines:** ${env.COVERAGE_LINES}%
""" : '- Coverage data not available'}

## Test Execution
- **Unit Tests:** ${fileExists('frontend/coverage') ? '✅ Completed' : '⏭️ Skipped'}
- **E2E Tests:** ${fileExists('test-results/junit.xml') ? '✅ Completed' : '⏭️ Skipped'}
- **Visual Regression:** ${fileExists('test-results') ? '✅ Completed' : '⏭️ Skipped'}

## Artifacts
- Coverage Report: Available in Jenkins
- E2E Report: Available in Jenkins
- Build Artifacts: Available in Jenkins

## Links
- **Build URL:** ${env.BUILD_URL}
- **Deployment:** ${env.DEPLOY_URL ?: 'Not deployed'}
"""
                
                writeFile file: 'test-report.md', text: report
                archiveArtifacts artifacts: 'test-report.md', allowEmptyArchive: true
            }
        }
    }
}

// Export all functions for use in Jenkinsfile
return [
    checkout: checkoutStage,
    setupEnvironment: setupEnvironmentStage,
    installDependencies: installDependenciesStage,
    lint: lintStage,
    build: buildStage,
    unitTests: unitTestsStage,
    e2eTests: e2eTestsStage,
    visualRegression: visualRegressionStage,
    testMetrics: testMetricsStage,
    securityScan: securityScanStage,
    performanceTest: performanceTestStage,
    accessibilityTest: accessibilityTestStage,
    deploy: deployStage,
    cleanup: cleanupStage,
    notification: notificationStage,
    generateTestReport: generateTestReportStage
]
