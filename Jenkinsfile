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
                        dir(env.FRONTEND_PATH) {
                            sh 'bash jenkins/scripts/build-frontend.sh'
                        }
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
                    // Determine test execution strategy
                    def useSelective = env.USE_SELECTIVE_TESTS == 'true'
                    def useParallel = env.USE_PARALLEL_TESTS != 'false'
                    
                    if (useSelective) {
                        echo "Using selective test execution based on changed files"
                        sh 'bash jenkins/scripts/selective-test-runner.sh'
                    } else if (useParallel) {
                        echo "Using parallel test execution for faster results"
                        sh 'bash jenkins/scripts/test-dotnet-unit-parallel.sh'
                    } else {
                        echo "Using standard sequential test execution"
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

        stage('Test .NET integration') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.FORCE_ALL_STAGES == 'true' }
                }
            }
            environment {
                AZURE_STORAGE_CONNECTION_STRING = credentials('AZURE_STORAGE_CONNECTION_STRING')
                AZURE_SERVICE_BUS_CONNECTION_STRING = credentials('AZURE_SERVICE_BUS_CONNECTION_STRING')
                AZURE_SQL_SERVER = credentials('AZURE_SQL_SERVER')
                AZURE_SQL_DATABASE = credentials('AZURE_SQL_DATABASE')
                AZURE_SQL_USER = credentials('AZURE_SQL_USER')
                AZURE_SQL_PASSWORD = credentials('AZURE_SQL_PASSWORD')
            }
            steps {
                sh 'bash jenkins/scripts/test-dotnet-integration.sh'
            }
            post {
                always {
                    junit allowEmptyResults: true, testResults: 'test-results/**/*integration*.xml'
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
                    echo "🏗️  Terraform changes detected. Deploying infrastructure..."
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
                    // Double-check: only merge if all previous stages succeeded
                    if (currentBuild.result != null && currentBuild.result != 'SUCCESS') {
                        error("Cannot auto-merge: Build status is ${currentBuild.result}")
                    }
                    echo "✅ All tests passed. Auto-merging ${env.BRANCH_NAME} into main..."
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



