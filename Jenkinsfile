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
        // Test: periodic scanning should detect this change automatically
    }

    stages {
        stage('Checkout') {
            steps {
                checkout([
                    $class: 'GitSCM',
                    branches: scm.branches,
                    extensions: [
                        [$class: 'CloneOption', depth: 1, shallow: true],  // Shallow clone for speed
                        [$class: 'CleanBeforeCheckout']
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
                        changeset "**/frontend/**,package.json,package-lock.json,Jenkinsfile"
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
                sh 'bash jenkins/scripts/test-dotnet-unit.sh'
            }
            post {
                always {
                    junit allowEmptyResults: true, testResults: 'test-results/**/*.xml'
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

        stage('Manual Approval for Merge') {
            when {
                allOf {
                    expression { isReadyBranch() }
                    expression { currentBuild.result == null || currentBuild.result == 'SUCCESS' }
                }
            }
            steps {
                script {
                    echo "⚠️  Ready to merge ${env.BRANCH_NAME} into main"
                    echo "All tests have passed. Waiting for manual approval..."
                    
                    // Manual approval - timeout after 24 hours
                    timeout(time: 24, unit: 'HOURS') {
                        input message: 'Merge into main?', 
                              ok: 'Yes, merge now',
                              submitter: 'admin'
                    }
                }
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
                    echo "✅ Manual approval received. Proceeding with auto-merge..."
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



