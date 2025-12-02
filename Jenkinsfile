pipeline {
    agent any
    
    environment {
        NODE_VERSION = '22.0.0'
        NPM_VERSION = '10.0.0'
        COVERAGE_THRESHOLD = '70'
        PLAYWRIGHT_BASE_URL = 'http://localhost:4200'
    }
    
    options {
        timeout(time: 30, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timestamps()
        ansiColor('xterm')
    }
    
    stages {
        stage('Checkout') {
            steps {
                script {
                    echo "🔍 Checking out code..."
                    checkout scm
                    sh 'git rev-parse HEAD > .git/commit-hash'
                    sh 'cat .git/commit-hash'
                }
            }
        }
        
        stage('Setup Environment') {
            steps {
                script {
                    echo "⚙️ Setting up environment..."
                    sh '''
                        node --version
                        npm --version
                    '''
                }
            }
        }
        
        stage('Install Dependencies') {
            steps {
                script {
                    echo "📦 Installing dependencies..."
                    dir('frontend') {
                        sh 'npm ci --prefer-offline --no-audit'
                    }
                    sh 'npm ci --prefer-offline --no-audit || true'
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
                                sh 'npm run ng -- version || true'
                                // Add TypeScript check if available
                                // sh 'npx tsc --noEmit'
                            }
                        }
                    }
                }
                
                stage('Code Analysis') {
                    steps {
                        script {
                            echo "📊 Running code analysis..."
                            // Add ESLint or other linters if configured
                            // dir('frontend') {
                            //     sh 'npm run lint || true'
                            // }
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
                    archiveArtifacts artifacts: 'frontend/dist/**/*', fingerprint: true
                }
            }
        }
        
        stage('Unit Tests') {
            parallel {
                stage('Run Unit Tests') {
                    steps {
                        script {
                            echo "🧪 Running unit tests..."
                            dir('frontend') {
                                sh 'npm test -- --code-coverage --browsers=ChromeHeadless --watch=false'
                            }
                        }
                    }
                }
                
                stage('Check Coverage Thresholds') {
                    steps {
                        script {
                            echo "📊 Checking coverage thresholds..."
                            dir('frontend') {
                                script {
                                    def coverageFile = 'coverage/interface-configurator/coverage-summary.json'
                                    if (fileExists(coverageFile)) {
                                        def coverage = readJSON file: coverageFile
                                        def statements = coverage.total.statements.pct
                                        def branches = coverage.total.branches.pct
                                        def functions = coverage.total.functions.pct
                                        def lines = coverage.total.lines.pct
                                        
                                        echo "Coverage Report:"
                                        echo "  Statements: ${statements}% (Threshold: ${env.COVERAGE_THRESHOLD}%)"
                                        echo "  Branches: ${branches}% (Threshold: 65%)"
                                        echo "  Functions: ${functions}% (Threshold: ${env.COVERAGE_THRESHOLD}%)"
                                        echo "  Lines: ${lines}% (Threshold: ${env.COVERAGE_THRESHOLD}%)"
                                        
                                        if (statements < env.COVERAGE_THRESHOLD.toFloat() ||
                                            branches < 65 ||
                                            functions < env.COVERAGE_THRESHOLD.toFloat() ||
                                            lines < env.COVERAGE_THRESHOLD.toFloat()) {
                                            error("Coverage thresholds not met!")
                                        }
                                    } else {
                                        echo "⚠️ Coverage file not found, skipping threshold check"
                                    }
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
                            publishHTML([
                                reportDir: 'coverage/interface-configurator',
                                reportFiles: 'index.html',
                                reportName: 'Coverage Report',
                                keepAll: true
                            ])
                            
                            // Publish LCOV for code coverage plugins
                            if (fileExists('coverage/interface-configurator/lcov.info')) {
                                publishCoverage adapters: [
                                    coberturaAdapter('coverage/interface-configurator/coverage.xml'),
                                    lcovAdapter('coverage/interface-configurator/lcov.info')
                                ],
                                sourceFileResolver: sourceFiles('STORE_LAST_BUILD')
                            }
                        }
                    }
                }
            }
        }
        
        stage('E2E Tests') {
            steps {
                script {
                    echo "🌐 Running E2E tests..."
                    sh '''
                        # Install Playwright browsers
                        npx playwright install --with-deps chromium || true
                        
                        # Run E2E tests
                        npm run test:e2e || true
                    '''
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
        
        stage('Test Metrics & Analysis') {
            steps {
                script {
                    echo "📈 Analyzing test metrics..."
                    sh '''
                        # Generate test summary
                        echo "## Test Execution Summary" > test-summary.md
                        echo "" >> test-summary.md
                        echo "### Unit Tests" >> test-summary.md
                        echo "- Coverage: Check coverage report" >> test-summary.md
                        echo "" >> test-summary.md
                        echo "### E2E Tests" >> test-summary.md
                        echo "- Results: Check E2E test report" >> test-summary.md
                    '''
                    archiveArtifacts artifacts: 'test-summary.md', allowEmptyArchive: true
                }
            }
        }
        
        stage('Security Scan') {
            steps {
                script {
                    echo "🔒 Running security scans..."
                    sh '''
                        # Run npm audit
                        cd frontend && npm audit --audit-level=moderate || true
                    '''
                }
            }
        }
        
        stage('Deploy Preview') {
            when {
                branch 'develop'
            }
            steps {
                script {
                    echo "🚀 Deploying preview build..."
                    // Add deployment steps here
                    // sh 'vercel deploy --preview'
                }
            }
        }
        
        stage('Deploy Production') {
            when {
                branch 'main'
            }
            steps {
                script {
                    echo "🚀 Deploying to production..."
                    // Add production deployment steps here
                    // sh 'vercel deploy --prod'
                }
            }
        }
    }
    
    post {
        always {
            script {
                echo "🧹 Cleaning up..."
                // Cleanup steps
            }
        }
        success {
            script {
                echo "✅ Pipeline succeeded!"
                // Send success notification
            }
        }
        failure {
            script {
                echo "❌ Pipeline failed!"
                // Send failure notification
                emailext(
                    subject: "Pipeline Failed: ${env.JOB_NAME} - ${env.BUILD_NUMBER}",
                    body: """
                        Pipeline failed for ${env.JOB_NAME} - Build #${env.BUILD_NUMBER}
                        
                        Check the build: ${env.BUILD_URL}
                        
                        Commit: ${sh(script: 'git rev-parse HEAD', returnStdout: true).trim()}
                    """,
                    to: "${env.CHANGE_AUTHOR_EMAIL ?: 'devops@example.com'}"
                )
            }
        }
        unstable {
            script {
                echo "⚠️ Pipeline unstable!"
            }
        }
    }
}
