#!/bin/bash
set -e

BRANCH_NAME="${BRANCH_NAME:-${GIT_BRANCH}}"
# Remove origin/ prefix if present
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
  echo "⚠️  Merge failed or not needed. Response: ${MERGE_RESPONSE}"
  echo ""
  echo "This is likely because:"
  echo "  - GIT_PASSWORD (GitHub PAT) is not configured in Jenkins"
  echo "  - The token doesn't have 'repo' permissions"
  echo "  - The merge is not possible (conflicts, up-to-date, etc.)"
  echo ""
  echo "To enable auto-merge, configure a GitHub Personal Access Token:"
  echo "  1. Go to GitHub → Settings → Developer settings → Personal access tokens"
  echo "  2. Create token with 'repo' scope"
  echo "  3. Add as GIT_PASSWORD in Jenkins credentials"
  echo ""
  echo "⚠️  Skipping auto-merge (non-fatal)"
  exit 0
fi

echo "Deleting branch ${BRANCH_NAME} via GitHub API..."
REF="heads/${BRANCH_NAME}"
# Encode / in REF for URL
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


