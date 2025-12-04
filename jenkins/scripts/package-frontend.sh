#!/bin/bash
set -e

echo "Packaging frontend artifacts..."
mkdir -p "$ARTIFACTS_PATH"
cp -r frontend/dist "$ARTIFACTS_PATH/frontend"
echo "Frontend packaging completed"


