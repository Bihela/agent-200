# 🚀 Agent 200: Hackathon Judge's Guide

This guide is designed to help hackathon judges quickly set up, deploy, and verify Agent 200 in an Azure environment.

## Overview
Agent 200 is an Autonomous SRE that monitors Azure metrics and automatically performs Root Cause Analysis (RCA) and remediation (via GitHub PRs) when anomalies are detected.

## Cloud Deployment (Quick Start)

### 1. Prerequisites
- Azure Subscription (with a Resource Group).
- Azure Container Apps Environment (Managed Environment).
- Azure Container Registry (ACR).
- Azure OpenAI Service (with `gpt-4o-mini` deployment).
- GitHub Personal Access Token (PAT) with `repo` permissions.

### 2. Setup & Configuration
We provide a helper script to generate the deployment manifest without leaking your secrets to the repository.

1.  **Clone the Repo**:
    ```bash
    git clone https://github.com/Bihela/agent-200.git
    cd agent-200
    ```

2.  **Run the Setup Script**:
    ```powershell
    ./setup-cloud.ps1
    ```
    *This script will prompt you for your Azure IDs, OpenAI keys, and GitHub token. It generates a local `azure-deployment.yaml`.*

### 3. Deploy to Azure
```bash
az containerapp create --name agent200-host --resource-group YOUR_RESOURCE_GROUP --yaml azure-deployment.yaml
```

### 4. Verification & Testing
1.  **Check Logs**: Use the Azure Portal or the following CLI command to see the Watchdog polling:
    ```bash
    az containerapp logs show --name agent200-host --resource-group YOUR_RESOURCE_GROUP --tail 100
    ```
2.  **Simulated Anomaly**: The Watchdog is configured to trigger on CPU spikes or authentication errors (401) from the Azure metrics provider.
3.  **Observability**: If you provided an App Insights connection string, you can see full trace logs of the agent's "Thought Process" in the Azure Portal.

---
> [!NOTE]
> For local development instructions, see the main [README.md](README.md).
