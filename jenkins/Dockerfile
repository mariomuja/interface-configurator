FROM jenkins/jenkins:lts-jdk17

USER root

# Optional: install Docker CLI to allow Jenkins to run Docker-based agents/builds
RUN apt-get update && \
    apt-get install -y ca-certificates curl gnupg lsb-release && \
    mkdir -p /etc/apt/keyrings && \
    curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg && \
    echo \
      "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian \
      $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null && \
    apt-get update && \
    apt-get install -y docker-ce-cli && \
    rm -rf /var/lib/apt/lists/*

# Install Jenkins plugins including:
# - workflow-multibranch: multibranch pipelines for ready/* and main
# - git, gitlab-plugin: Git integration
# - configuration-as-code: load YAML config on startup
RUN jenkins-plugin-cli --plugins \
    workflow-aggregator \
    workflow-multibranch \
    git \
    gitlab-plugin \
    configuration-as-code \
    job-dsl \
    credentials \
    credentials-binding

# Copy Jenkins Configuration as Code (JCasC)
# Build-Kontext ist das Projekt-Root (..), daher liegt die Datei unter jenkins/jenkins.yml
COPY jenkins/jenkins.yml /usr/share/jenkins/ref/jenkins.yml

ENV CASC_JENKINS_CONFIG=/usr/share/jenkins/ref/jenkins.yml

USER jenkins


