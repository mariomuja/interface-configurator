FROM jenkins/jenkins:lts-jdk21

USER root

# Install Docker CLI and Azure CLI
RUN apt-get update && \
    apt-get install -y ca-certificates curl gnupg lsb-release && \
    # Docker CLI
    mkdir -p /etc/apt/keyrings && \
    curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg && \
    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian \
      $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null && \
    apt-get update && \
    apt-get install -y docker-ce-cli && \
    # Azure CLI (for Bicep deployments and Azure resource management)
    curl -sL https://aka.ms/InstallAzureCLIDeb | bash && \
    # Cleanup to reduce image size
    rm -rf /var/lib/apt/lists/*

# Add jenkins user to docker group to allow access to Docker socket
# Create docker group and add jenkins user to it
# The GID will be matched when the socket is mounted
RUN groupadd -g 999 docker 2>/dev/null || groupadd docker && \
    usermod -aG docker jenkins

# Install Jenkins plugins including:
# - workflow-multibranch: multibranch pipelines for ready/* and main
# - git, github: Git and GitHub integration with commit status support
# - configuration-as-code: load YAML config on startup
# - blueocean: Modern UI for better pipeline visualization
# - monitoring: System monitoring (CPU, memory, disk, network)
RUN jenkins-plugin-cli --plugins \
    workflow-aggregator \
    workflow-multibranch \
    git \
    github \
    github-branch-source \
    configuration-as-code \
    job-dsl \
    credentials \
    credentials-binding \
    blueocean \
    monitoring

# Copy Jenkins Configuration as Code (JCasC)
# Build-Kontext ist das Projekt-Root (..), daher liegt die Datei unter jenkins/jenkins.yml
COPY jenkins/jenkins.yml /usr/share/jenkins/ref/jenkins.yml

# Copy and set up Docker entrypoint script to fix socket permissions
COPY jenkins/docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

ENV CASC_JENKINS_CONFIG=/usr/share/jenkins/ref/jenkins.yml

# Keep as root - entrypoint will switch to jenkins user after fixing permissions
# Use custom entrypoint that fixes Docker permissions
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]


