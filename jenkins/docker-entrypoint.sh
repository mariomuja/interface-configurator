#!/bin/bash
set -e

# Fix Docker socket permissions
# Get the GID of the docker socket
if [ -S /var/run/docker.sock ]; then
    DOCKER_GID=$(stat -c '%g' /var/run/docker.sock 2>/dev/null || stat -f '%g' /var/run/docker.sock 2>/dev/null || echo "999")
    
    # If socket is owned by root (GID 0), we need special handling
    if [ "$DOCKER_GID" = "0" ]; then
        # For Docker Desktop on Windows, socket is root:root
        # Add jenkins to root group temporarily (or use docker group with GID 0)
        # Better: change socket permissions to be accessible by docker group
        # First ensure docker group exists
        if ! getent group docker > /dev/null 2>&1; then
            groupadd docker 2>/dev/null || true
        fi
        # Change socket to be owned by root:docker with group read/write
        chown root:docker /var/run/docker.sock 2>/dev/null || true
        chmod 660 /var/run/docker.sock 2>/dev/null || true
        # Add jenkins to docker group
        usermod -aG docker jenkins || true
    else
        # Normal case: create docker group with matching GID
        if ! getent group docker > /dev/null 2>&1; then
            groupadd -g "$DOCKER_GID" docker 2>/dev/null || groupadd docker
        fi
        # Add jenkins user to docker group
        usermod -aG docker jenkins || true
    fi
fi

# Switch to jenkins user and run the original Jenkins entrypoint
# Try gosu first, fall back to su if not available
if command -v gosu > /dev/null 2>&1; then
    exec gosu jenkins /usr/local/bin/jenkins.sh "$@"
else
    # Fallback: use su to switch to jenkins user
    exec su jenkins -c "/usr/local/bin/jenkins.sh $*"
fi

