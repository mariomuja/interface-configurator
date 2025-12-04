pipeline {
    agent any

    triggers {
        // Poll SCM every minute for new commits
        pollSCM('* * * * *')
    }

    options {
        timestamps()
        buildDiscarder(logRotator(numToKeepStr: '10'))  // Keep only last 10 builds
        disableConcurrentBuilds()  // Prevent concurrent builds of same branch
        skipDefaultCheckout()  // We'll do a shallow checkout for speed
    }

    environment {
        DOTNET_VERSION       = "8.0"
        NODE_VERSION         = "22"
        NPM_VERSION          = "10"
        SOLUTION_PATH        = "azure-functions/azure-functions.sln"
        TEST_PROJECT         = "tests/main.Core.Tests/main.Core.Tests.csproj"
        FRONTEND_PATH        = "frontend"
        BUILD_CONFIGURATION  = "Release"
        ARTIFACTS_PATH       = "artifacts"
        COVERAGE_PATH        = "coverage"

        // GitHub repository information for auto-merge of ready/* → main
        GITHUB_OWNER         = "mariomuja"
        GITHUB_REPO          = "interface-configurator"
        
        // Set FORCE_ALL_STAGES=true to run all deployment stages regardless of branch
        // Useful for testing the complete pipeline on ready/* branches
        // All Azure credentials are now configured in Jenkins
        FORCE_ALL_STAGES     = "true"
        
        // Pipeline optimization flags
        USE_PARALLEL_TESTS   = "true"   // Run tests in parallel (fast + slow categories)
        USE_SELECTIVE_TESTS  = "false"  // Only run tests for changed code (experimental)
        // Azure SQL Server credentials updated
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "📥 CHECKOUT: Cloning repository with shallow fetch"
                    echo "   - Depth: 1 (latest commit only)"
                    echo "   - Tags: Skipped for speed"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                }
                checkout([
                    $class: 'GitSCM',
                    branches: scm.branches,
                    extensions: [
                        [$class: 'CloneOption', depth: 1, shallow: true, noTags: true]  // Shallow clone for speed
                        // Note: CleanBeforeCheckout removed - Docker creates root-owned files that Jenkins can't delete
                    ],
                    userRemoteConfigs: scm.userRemoteConfigs
                ])
            }
        }

        stage('Parallel Builds') {
            when {
                expression { isReadyOrMain() }
            }
            parallel {
                stage('Build .NET') {
                    steps {
                        script {
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                            echo "🔨 BUILD .NET: Compiling Azure Functions & Libraries"
                            echo "   - Solution: azure-functions.sln"
                            echo "   - Projects: main.Core, adapters, services, main"
                            echo "   - Configuration: Release"
                            echo "   - NuGet: Cached in .nuget/packages/"
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                        }
                        sh 'bash jenkins/scripts/build-dotnet.sh'
                    }
                }

                stage('Build Frontend') {
                    when {
                        anyOf {
                            changeset "**/frontend/**,package.json,package-lock.json,Jenkinsfile"
                            expression { env.FORCE_ALL_STAGES == 'true' }
                        }
                    }
                    steps {
                        script {
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                            echo "🎨 BUILD FRONTEND: Compiling Angular Application"
                            echo "   - Framework: Angular (latest)"
                            echo "   - Output: frontend/dist/"
                            echo "   - Target: Azure Static Web App"
                            echo "   - npm cache: Cached in .npm-cache/"
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                        }
                        // Run from workspace root, not from frontend directory
                        sh 'bash jenkins/scripts/build-frontend.sh'
                    }
                }
            }
        }

        stage('Test .NET unit') {
            when {
                expression { isReadyOrMain() }
            }
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "🧪 TEST UNIT: Running C# Unit Tests (xUnit)"
                    echo "   - Total: 158 tests"
                    echo "   - Excludes: Integration & Performance tests (on ready/*)"
                    echo "   - Framework: xUnit.net"
                    echo "   - Execution: Parallel (fast + slow categories)"
                    echo "   - Expected time: ~20-30s"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    
                    // Determine test execution strategy
                    def useSelective = env.USE_SELECTIVE_TESTS == 'true'
                    def useParallel = env.USE_PARALLEL_TESTS != 'false'
                    
                    if (useSelective) {
                        echo "Strategy: Selective (only changed code)"
                        sh 'bash jenkins/scripts/selective-test-runner.sh'
                    } else if (useParallel) {
                        echo "Strategy: Parallel execution"
                        sh 'bash jenkins/scripts/test-dotnet-unit-parallel.sh'
                    } else {
                        echo "Strategy: Sequential execution"
                        sh 'bash jenkins/scripts/test-dotnet-unit.sh'
                    }
                }
            }
            post {
                always {
                    junit allowEmptyResults: true, testResults: 'test-results/**/*junit*.xml'
                }
            }
        }

        stage('Integration Tests') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.FORCE_ALL_STAGES == 'true' }
                }
            }
            parallel {
                stage('Integration: Blob Storage') {
                    environment {
                        AZURE_STORAGE_CONNECTION_STRING = credentials('AZURE_STORAGE_CONNECTION_STRING')
                    }
                    steps {
                        script {
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                            echo "☁️  INTEGRATION: Blob Storage Tests"
                            echo "   - Tests: 8 tests (containers, blobs, metadata)"
                            echo "   - Requires: AZURE_STORAGE_CONNECTION_STRING"
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                        }
                        sh 'bash jenkins/scripts/test-integration-blob-storage.sh'
                    }
                    post {
                        always {
                            junit allowEmptyResults: true, testResults: 'test-results/**/*blob*.xml'
                        }
                    }
                }

                stage('Integration: Service Bus') {
                    environment {
                        AZURE_SERVICE_BUS_CONNECTION_STRING = credentials('AZURE_SERVICE_BUS_CONNECTION_STRING')
                    }
                    steps {
                        script {
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                            echo "📨 INTEGRATION: Service Bus Tests"
                            echo "   - Tests: 10 tests (messaging, topics, subscriptions)"
                            echo "   - Requires: AZURE_SERVICE_BUS_CONNECTION_STRING"
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                        }
                        sh 'bash jenkins/scripts/test-integration-service-bus.sh'
                    }
                    post {
                        always {
                            junit allowEmptyResults: true, testResults: 'test-results/**/*servicebus*.xml'
                        }
                    }
                }

                stage('Integration: SQL Server') {
                    environment {
                        AZURE_SQL_SERVER = credentials('AZURE_SQL_SERVER')
                        AZURE_SQL_DATABASE = credentials('AZURE_SQL_DATABASE')
                        AZURE_SQL_USER = credentials('AZURE_SQL_USER')
                        AZURE_SQL_PASSWORD = credentials('AZURE_SQL_PASSWORD')
                    }
                    steps {
                        script {
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                            echo "🗄️  INTEGRATION: SQL Server Tests"
                            echo "   - Tests: 18 tests (tables, indexes, queries)"
                            echo "   - Requires: SQL credentials (4 values)"
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                        }
                        sh 'bash jenkins/scripts/test-integration-sql-server.sh'
                    }
                    post {
                        always {
                            junit allowEmptyResults: true, testResults: 'test-results/**/*sql*.xml'
                        }
                    }
                }

                stage('Integration: Adapters') {
                    environment {
                        AZURE_STORAGE_CONNECTION_STRING = credentials('AZURE_STORAGE_CONNECTION_STRING')
                        AZURE_SERVICE_BUS_CONNECTION_STRING = credentials('AZURE_SERVICE_BUS_CONNECTION_STRING')
                    }
                    steps {
                        script {
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                            echo "🔄 INTEGRATION: Adapter Pipeline Tests"
                            echo "   - Tests: 23 tests (adapters, containers)"
                            echo "   - Requires: Storage + Service Bus"
                            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                        }
                        sh 'bash jenkins/scripts/test-integration-adapters.sh'
                    }
                    post {
                        always {
                            junit allowEmptyResults: true, testResults: 'test-results/**/*adapters*.xml'
                        }
                    }
                }
            }
        }

        stage('Test E2E') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.FORCE_ALL_STAGES == 'true' }
                }
            }
            environment {
                E2E_BASE_URL = "${env.STATIC_WEB_APP_URL ?: 'https://your-static-web-app.azurestaticapps.net'}"
                E2E_API_URL  = "${env.AZURE_FUNCTION_APP_URL ?: 'https://func-integration-main.azurewebsites.net'}"
            }
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "🌐 TEST E2E: End-to-End Browser Testing (Playwright)"
                    echo "   - Framework: Playwright (TypeScript)"
                    echo "   - Tests: UI workflows, API endpoints, authentication"
                    echo "   - Target: Deployed applications (Static Web App + Functions)"
                    echo "   - Expected time: ~1-3 minutes"
                    echo "   - Files: tests/end-to-end/*.spec.ts"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                }
                sh 'bash jenkins/scripts/test-e2e.sh'
            }
        }

        stage('Terraform Apply (Infrastructure)') {
            when {
                anyOf {
                    allOf {
                        branch 'main'
                        anyOf {
                            changeset "**/terraform/**"
                            expression { env.FORCE_TERRAFORM == 'true' }
                        }
                    }
                    expression { env.FORCE_ALL_STAGES == 'true' }
                }
            }
            environment {
                ARM_CLIENT_ID       = credentials('AZURE_CLIENT_ID')
                ARM_CLIENT_SECRET   = credentials('AZURE_CLIENT_SECRET')
                ARM_TENANT_ID       = credentials('AZURE_TENANT_ID')
                ARM_SUBSCRIPTION_ID = credentials('AZURE_SUBSCRIPTION_ID')
            }
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "🏗️  TERRAFORM: Deploying Azure Infrastructure"
                    echo "   - Provider: Azure (azurerm)"
                    echo "   - Resources: Function App, Storage, SQL, Service Bus"
                    echo "   - Actions: init → validate → plan → apply"
                    echo "   - Only runs when: terraform/*.tf files changed"
                    echo "   - Expected time: ~2-5 minutes"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                }
                sh 'bash jenkins/scripts/terraform-apply.sh'
            }
        }

        stage('Package .NET') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.GIT_TAG && env.GIT_TAG.trim() != '' }
                    expression { env.FORCE_ALL_STAGES == 'true' }
                }
            }
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "📦 PACKAGE .NET: Preparing deployment artifacts"
                    echo "   - Output: artifacts/ directory"
                    echo "   - Contents: Compiled Function App binaries"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                }
                sh 'bash jenkins/scripts/package-dotnet.sh'
            }
        }

        stage('Package Frontend') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.GIT_TAG && env.GIT_TAG.trim() != '' }
                    expression { env.FORCE_ALL_STAGES == 'true' }
                }
            }
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "📦 PACKAGE FRONTEND: Preparing web artifacts"
                    echo "   - Output: artifacts/frontend/"
                    echo "   - Contents: Compiled Angular app (HTML, JS, CSS)"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                }
                sh 'bash jenkins/scripts/package-frontend.sh'
            }
        }

        stage('Deploy Static Web App (Frontend)') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.GIT_TAG && env.GIT_TAG.trim() != '' }
                    expression { env.FORCE_ALL_STAGES == 'true' }
                }
            }
            environment {
                AZURE_STATIC_WEB_APP_TOKEN = credentials('AZURE_STATIC_WEB_APP_TOKEN')
            }
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "🚀 DEPLOY FRONTEND: Deploying to Azure Static Web App"
                    echo "   - Source: frontend/dist/interface-configuration/browser/"
                    echo "   - Tool: Azure Static Web Apps CLI (swa)"
                    echo "   - Target: Production environment"
                    echo "   - CDN: Automatic global distribution"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                }
                sh 'bash jenkins/scripts/deploy-static-web-app.sh'
            }
        }

        stage('Deploy Function App (Azure)') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.GIT_TAG && env.GIT_TAG.trim() != '' }
                    expression { env.FORCE_ALL_STAGES == 'true' }
                }
            }
            environment {
                AZURE_CLIENT_ID       = credentials('AZURE_CLIENT_ID')
                AZURE_CLIENT_SECRET   = credentials('AZURE_CLIENT_SECRET')
                AZURE_TENANT_ID       = credentials('AZURE_TENANT_ID')
                AZURE_SUBSCRIPTION_ID = credentials('AZURE_SUBSCRIPTION_ID')
                AZURE_FUNCTION_APP_NAME = "${AZURE_FUNCTION_APP_NAME}"
                AZURE_RESOURCE_GROUP    = "${AZURE_RESOURCE_GROUP}"
            }
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "⚡ DEPLOY FUNCTION APP: Deploying Backend to Azure"
                    echo "   - Target: ${env.AZURE_FUNCTION_APP_NAME}"
                    echo "   - Runtime: .NET 8.0 Isolated Worker"
                    echo "   - Tool: Azure Functions Core Tools (func)"
                    echo "   - Method: Direct publish (--dotnet-isolated)"
                    echo "   - Expected time: ~2-4 minutes"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                }
                sh 'bash jenkins/scripts/deploy-function-app.sh'
            }
        }

        stage('Build and Push Adapter Images') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.GIT_TAG && env.GIT_TAG.trim() != '' }
                    expression { env.FORCE_ALL_STAGES == 'true' }
                }
            }
            environment {
                ACR_NAME     = "${env.ACR_NAME ?: 'myacr'}"
                ACR_USERNAME = credentials('ACR_USERNAME')
                ACR_PASSWORD = credentials('ACR_PASSWORD')
            }
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "🐳 BUILD ADAPTER IMAGES: Docker → Azure Container Registry"
                    echo "   - Registry: ${env.ACR_NAME}.azurecr.io"
                    echo "   - Images: CSV adapter, SQL Server adapter (future)"
                    echo "   - Note: Currently no-op (adapters in Function App)"
                    echo "   - Ready for: Future containerization"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                }
                sh 'bash jenkins/scripts/build-and-push-adapter-images.sh'
            }
        }

        stage('Auto-merge ready/* into main') {
            when {
                allOf {
                    expression { isReadyBranch() }
                    expression { currentBuild.result == null || currentBuild.result == 'SUCCESS' }
                }
            }
            environment {
                GITHUB_TOKEN = credentials('github-token')
            }
            steps {
                script {
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    echo "🔀 AUTO-MERGE: Merging to main branch"
                    echo "   - Source: ${env.BRANCH_NAME}"
                    echo "   - Target: main"
                    echo "   - Method: GitHub API merge"
                    echo "   - Trigger: All tests passed"
                    echo "   - Next: Triggers deployment pipeline on main"
                    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
                    
                    // Double-check: only merge if all previous stages succeeded
                    if (currentBuild.result != null && currentBuild.result != 'SUCCESS') {
                        error("Cannot auto-merge: Build status is ${currentBuild.result}")
                    }
                }
                sh 'bash jenkins/scripts/auto-merge.sh'
            }
        }
    }

    post {
        failure {
            echo "Pipeline failed for branch ${env.BRANCH_NAME ?: env.GIT_BRANCH}"
        }
    }
}

// Helper: determine if this is main or ready/* branch
def isReadyOrMain() {
    def branch = env.BRANCH_NAME ?: env.GIT_BRANCH ?: ""
    return branch == "main" || branch.startsWith("ready/")
}

// Helper: determine if this is a ready/* branch
def isReadyBranch() {
    def branch = env.BRANCH_NAME ?: env.GIT_BRANCH ?: ""
    return branch.startsWith("ready/")
}



