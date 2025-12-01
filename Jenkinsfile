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
    }

    stages {
        stage('Build .NET') {
            when {
                expression { isReadyOrMain() }
            }
            steps {
                sh '''
                  export PATH="/usr/bin:/usr/local/bin:$PATH"
                  echo "Restoring NuGet packages..."
                  /usr/bin/docker run --rm -v "$PWD:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet restore "$SOLUTION_PATH"
                  echo "Building solution..."
                  /usr/bin/docker run --rm -v "$PWD:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet build "$SOLUTION_PATH" --configuration "$BUILD_CONFIGURATION" --no-restore
                  echo "Build completed successfully"
                '''
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
                    sh '''
                      export PATH="/usr/bin:/usr/local/bin:$PATH"
                      echo "Installing Node.js dependencies..."
                      /usr/bin/docker run --rm -v "$PWD:/workspace" -w /workspace node:22 npm ci
                      echo "Building Angular frontend..."
                      /usr/bin/docker run --rm -v "$PWD:/workspace" -w /workspace node:22 npm run build:prod
                      echo "Frontend build completed successfully"
                    '''
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
                sh '''
                  export PATH="/usr/bin:/usr/local/bin:$PATH"
                  echo "Running all unit tests (excluding integration tests)..."
                  mkdir -p test-results
                  /usr/bin/docker run --rm -v "$PWD:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet test "$TEST_PROJECT" \
                    --configuration "$BUILD_CONFIGURATION" \
                    --verbosity normal \
                    --filter "FullyQualifiedName!~Integration" \
                    --logger "trx;LogFileName=junit-unit.trx"
                  echo "Unit tests completed"
                '''
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
                sh '''
                  export PATH="/usr/bin:/usr/local/bin:$PATH"
                  echo "Packaging .NET artifacts..."
                  mkdir -p "$ARTIFACTS_PATH"
                  /usr/bin/docker run --rm -v "$PWD:/workspace" -w /workspace/azure-functions/main mcr.microsoft.com/dotnet/sdk:8.0 dotnet publish --configuration "$BUILD_CONFIGURATION" --output "../../$ARTIFACTS_PATH/azure-functions"
                  /usr/bin/docker run --rm -v "$PWD:/workspace" -w /workspace/main.Core mcr.microsoft.com/dotnet/sdk:8.0 dotnet publish --configuration "$BUILD_CONFIGURATION" --output "../$ARTIFACTS_PATH/main.Core"
                  echo "Packaging completed"
                '''
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
                sh '''
                  echo "Packaging frontend artifacts..."
                  mkdir -p "$ARTIFACTS_PATH"
                  cp -r frontend/dist "$ARTIFACTS_PATH/frontend"
                  echo "Frontend packaging completed"
                '''
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
                sh '''
                  export PATH="/usr/bin:/usr/local/bin:$PATH"
                  echo "Setting up deployment environment..."
                  /usr/bin/docker run --rm -v "$PWD:/workspace" -w /workspace \
                    -e AZURE_CLIENT_ID="$AZURE_CLIENT_ID" \
                    -e AZURE_CLIENT_SECRET="$AZURE_CLIENT_SECRET" \
                    -e AZURE_TENANT_ID="$AZURE_TENANT_ID" \
                    -e AZURE_SUBSCRIPTION_ID="$AZURE_SUBSCRIPTION_ID" \
                    -e AZURE_FUNCTION_APP_NAME="$AZURE_FUNCTION_APP_NAME" \
                    -e AZURE_RESOURCE_GROUP="$AZURE_RESOURCE_GROUP" \
                    mcr.microsoft.com/dotnet/sdk:8.0 bash -c "
                    apt-get update && apt-get install -y curl gnupg lsb-release
                    echo 'Installing Azure CLI...'
                    curl -sL https://aka.ms/InstallAzureCLIDeb | bash
                    echo 'Installing Azure Functions Core Tools...'
                    curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > /etc/apt/trusted.gpg.d/microsoft.gpg
                    sh -c 'echo \"deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-\$(lsb_release -cs)-prod \$(lsb_release -cs) main\" > /etc/apt/sources.list.d/dotnetdev.list'
                    apt-get update && apt-get install -y azure-functions-core-tools-4
                    echo 'Logging in to Azure...'
                    if [ -n \"\$AZURE_CLIENT_ID\" ] && [ -n \"\$AZURE_CLIENT_SECRET\" ] && [ -n \"\$AZURE_TENANT_ID\" ]; then
                      az login --service-principal -u \"\$AZURE_CLIENT_ID\" -p \"\$AZURE_CLIENT_SECRET\" --tenant \"\$AZURE_TENANT_ID\"
                      if [ -n \"\$AZURE_SUBSCRIPTION_ID\" ]; then
                        az account set --subscription \"\$AZURE_SUBSCRIPTION_ID\"
                      fi
                      echo 'Azure login successful'
                      az account show
                    else
                      echo 'Azure credentials not set. Please configure Jenkins credentials.'
                      exit 1
                    fi
                    echo 'Deploying Azure Function App to production...'
                    cd azure-functions/main
                    echo 'Step 1: Publishing Function App (--self-contained false)...'
                    dotnet publish --self-contained false --configuration Release --output ./publish
                    echo 'Publish completed'
                    if [ -z \"\$AZURE_FUNCTION_APP_NAME\" ] || [ -z \"\$AZURE_RESOURCE_GROUP\" ]; then
                      echo 'Required variables not set: AZURE_FUNCTION_APP_NAME, AZURE_RESOURCE_GROUP'
                      exit 1
                    fi
                    echo 'Step 2: Deploying to Azure Function App...'
                    echo \"Function App: \$AZURE_FUNCTION_APP_NAME\"
                    echo \"Resource Group: \$AZURE_RESOURCE_GROUP\"
                    func azure functionapp publish \"\$AZURE_FUNCTION_APP_NAME\" --dotnet-isolated
                    echo 'Deployment completed successfully'
                    echo \"Function App URL: https://\${AZURE_FUNCTION_APP_NAME}.azurewebsites.net\"
                  "
                '''
            }
        }

        stage('Auto-merge ready/* into main') {
            when {
                expression { isReadyBranch() }
            }
            steps {
                sh '''
                          set -e

                          BRANCH_NAME="${BRANCH_NAME:-${GIT_BRANCH}}"
                          # Entferne ggf. origin/ Präfix
                          BRANCH_NAME="${BRANCH_NAME#origin/}"

                          echo "Detected ready branch: ${BRANCH_NAME}"

                          if [[ ! "${BRANCH_NAME}" =~ ^ready/ ]]; then
                            echo "Not a ready/* branch, skipping auto-merge."
                            exit 0
                          fi

                          echo "Merging ${BRANCH_NAME} into main via GitHub API..."
                          MERGE_RESPONSE=$(curl -sS -w "%{http_code}" -X PUT \
                            -H "Authorization: Bearer ${GIT_PASSWORD}" \
                            -H "Accept: application/vnd.github+json" \
                            https://api.github.com/repos/${GITHUB_OWNER}/${GITHUB_REPO}/merges \
                            -d "{\"base\":\"main\",\"head\":\"${BRANCH_NAME}\",\"commit_message\":\"chore: merge ${BRANCH_NAME} into main (Jenkins auto-merge)\"}")

                          HTTP_CODE="${MERGE_RESPONSE: -3}"
                          echo "GitHub merge API HTTP status: ${HTTP_CODE}"

                          if [ "${HTTP_CODE}" != "201" ] && [ "${HTTP_CODE}" != "200" ]; then
                            echo "Merge failed or not needed. Response: ${MERGE_RESPONSE}"
                            exit 1
                          fi

                          echo "Deleting branch ${BRANCH_NAME} via GitHub API..."
                          REF="heads/${BRANCH_NAME}"
                          # / in REF muss URL-enkodiert werden
                          ENCODED_REF=$(printf "%s" "${REF}" | sed 's#/#%2F#g')

                          DELETE_RESPONSE=$(curl -sS -w "%{http_code}" -X DELETE \
                            -H "Authorization: Bearer ${GIT_PASSWORD}" \
                            -H "Accept: application/vnd.github+json" \
                            "https://api.github.com/repos/${GITHUB_OWNER}/${GITHUB_REPO}/git/refs/${ENCODED_REF}" || true)

                          DELETE_CODE="${DELETE_RESPONSE: -3}"
                          echo "GitHub delete ref HTTP status: ${DELETE_CODE}"

                          if [ "${DELETE_CODE}" = "204" ] || [ "${DELETE_CODE}" = "200" ]; then
                            echo "Branch ${BRANCH_NAME} deleted successfully."
                          else
                            echo "Warning: could not delete branch ${BRANCH_NAME}. Response: ${DELETE_RESPONSE}"
                          fi
                        '''
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



