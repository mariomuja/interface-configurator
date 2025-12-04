pipeline {
    agent any

    options {
        timestamps()
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
        // Test: periodic scanning should detect this change automatically
    }

    stages {
        stage('Build .NET') {
            when {
                expression { isReadyOrMain() }
            }
            steps {
                sh 'bash jenkins/scripts/build-dotnet.sh'
            }
        }

        stage('Build Frontend') {
            when {
                allOf {
                    expression { isReadyOrMain() }
                    changeset "**/frontend/**,package.json,package-lock.json,Jenkinsfile"
                }
            }
            steps {
                dir(env.FRONTEND_PATH) {
                    sh 'bash jenkins/scripts/build-frontend.sh'
                }
            }
        }

        stage('Test .NET unit') {
            when {
                allOf {
                    expression { isReadyOrMain() }
                    changeset "tests/**,main.Core/**,azure-functions/**,adapters/**,Jenkinsfile"
                }
            }
            steps {
                sh 'bash jenkins/scripts/test-dotnet-unit.sh'
            }
            post {
                always {
                    junit 'test-results/**/*.xml'
                }
            }
        }

        stage('Package .NET') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.GIT_TAG && env.GIT_TAG.trim() != '' }
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
                }
            }
            steps {
                sh 'bash jenkins/scripts/package-frontend.sh'
            }
        }

        stage('Deploy Function App (Azure)') {
            when {
                anyOf {
                    branch 'main'
                    expression { env.GIT_TAG && env.GIT_TAG.trim() != '' }
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

        stage('Auto-merge ready/* into main') {
            when {
                expression { isReadyBranch() }
            }
            steps {
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



