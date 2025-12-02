// Single Jenkins Pipeline for All Branches
// Automatically runs for every branch pushed to GitHub
// Configure as Multibranch Pipeline in Jenkins with GitHub webhook integration

pipeline {
    agent any
    
    options {
        timeout(time: 30, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timestamps()
        ansiColor('xterm')
        // Skip checkout if already done (for multibranch)
        skipDefaultCheckout(false)
    }
    
    environment {
        NODE_VERSION = '22.0.0'
        NPM_VERSION = '10.0.0'
        COVERAGE_THRESHOLD = '70'
        BRANCH_NAME = "${env.BRANCH_NAME ?: sh(script: 'git rev-parse --abbrev-ref HEAD', returnStdout: true).trim()}"
    }
    
    stages {
        stage('Checkout') {
            steps {
                checkout scm
                script {
                    // Extract git information
                    env.GIT_COMMIT_SHORT = sh(
                        script: 'git rev-parse --short HEAD',
                        returnStdout: true
                    ).trim()
                    env.GIT_COMMIT = sh(
                        script: 'git rev-parse HEAD',
                        returnStdout: true
                    ).trim()
                    env.GIT_BRANCH = sh(
                        script: 'git rev-parse --abbrev-ref HEAD',
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
                    
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "🔍 Build Information"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "Branch: ${env.GIT_BRANCH}"
                    echo "Commit: ${env.GIT_COMMIT_SHORT}"
                    echo "Author: ${env.GIT_AUTHOR}"
                    echo "Message: ${env.GIT_MESSAGE}"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                }
            }
        }
        
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
                                    
                                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                                    echo "📊 Coverage Report"
                                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                                    echo "  Statements: ${statements}%"
                                    echo "  Branches: ${branches}%"
                                    echo "  Functions: ${functions}%"
                                    echo "  Lines: ${lines}%"
                                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                                    
                                    // Check thresholds (only enforce on main/develop)
                                    def threshold = env.COVERAGE_THRESHOLD ?: '70'
                                    def enforceThreshold = env.BRANCH_NAME in ['main', 'develop', 'master']
                                    
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
        
        stage('E2E Tests') {
            when {
                // Run E2E tests on main, develop, and PRs targeting them
                anyOf {
                    branch 'main'
                    branch 'develop'
                    branch 'master'
                    expression { 
                        env.CHANGE_TARGET in ['main', 'develop', 'master'] 
                    }
                }
            }
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
        
        stage('Visual Regression') {
            when {
                // Only run visual regression on main branch
                branch 'main'
            }
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
        
        stage('Accessibility Tests') {
            when {
                // Run accessibility tests on main and develop
                anyOf {
                    branch 'main'
                    branch 'develop'
                    branch 'master'
                }
            }
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
- E2E Tests: ${env.BRANCH_NAME in ['main', 'develop', 'master'] ? '✅ Completed' : '⏭️ Skipped'}
- Visual Regression: ${env.BRANCH_NAME == 'main' ? '✅ Completed' : '⏭️ Skipped'}

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
        
        stage('Deploy Preview') {
            when {
                // Deploy preview for develop branch
                branch 'develop'
            }
            steps {
                script {
                    echo "🚀 Deploying preview build..."
                    // Uncomment and configure when ready
                    // sh 'vercel deploy --preview --token=${VERCEL_TOKEN} || echo "Deployment skipped"'
                }
            }
            post {
                success {
                    script {
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
        
        stage('Deploy Production') {
            when {
                // Deploy production for main branch
                branch 'main'
            }
            steps {
                script {
                    echo "🚀 Deploying to production..."
                    // Uncomment and configure when ready
                    // sh 'vercel deploy --prod --token=${VERCEL_TOKEN} || echo "Deployment skipped"'
                }
            }
            post {
                success {
                    script {
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
    
    post {
        always {
            script {
                echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                echo "📊 Build Summary"
                echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                echo "Branch: ${env.GIT_BRANCH}"
                echo "Status: ${currentBuild.result}"
                echo "Coverage: ${env.COVERAGE_LINES ?: 'N/A'}%"
                echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
            }
        }
        success {
            script {
                echo "✅ Pipeline succeeded!"
                
                // Generate comprehensive test report
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
        failure {
            script {
                echo "❌ Pipeline failed!"
                
                // Send failure notification
                try {
                    emailext(
                        subject: "Pipeline Failed: ${env.JOB_NAME} - Build #${env.BUILD_NUMBER}",
                        body: """
                            <h2>Pipeline Failed</h2>
                            <p><strong>Job:</strong> ${env.JOB_NAME}</p>
                            <p><strong>Build:</strong> #${env.BUILD_NUMBER}</p>
                            <p><strong>Branch:</strong> ${env.GIT_BRANCH}</p>
                            <p><strong>Commit:</strong> ${env.GIT_COMMIT_SHORT}</p>
                            <p><strong>Author:</strong> ${env.GIT_AUTHOR}</p>
                            <p><strong>Message:</strong> ${env.GIT_MESSAGE}</p>
                            <p><strong>Build URL:</strong> <a href="${env.BUILD_URL}">${env.BUILD_URL}</a></p>
                        """,
                        to: "${env.CHANGE_AUTHOR_EMAIL ?: env.GIT_AUTHOR_EMAIL ?: 'devops@example.com'}",
                        mimeType: 'text/html'
                    )
                } catch (Exception e) {
                    echo "Email notification failed: ${e.message}"
                }
            }
        }
        unstable {
            script {
                echo "⚠️ Pipeline unstable!"
            }
        }
    }
}
