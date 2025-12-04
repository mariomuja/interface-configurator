#!/bin/bash
set -e

export PATH="/usr/bin:/usr/local/bin:$PATH"

# We're already in the frontend directory via dir() in Jenkinsfile
echo "Installing Node.js dependencies..."
/usr/bin/docker run --rm --volumes-from interface-configurator-jenkins -w "$PWD" node:22 npm ci

echo "Building Angular frontend..."
/usr/bin/docker run --rm --volumes-from interface-configurator-jenkins -w "$PWD" node:22 npm run build:prod

echo "Frontend build completed successfully"

