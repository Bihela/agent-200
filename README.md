# Agent 200: Autonomous SRE with Tiered Intelligence

![build-passing](https://img.shields.io/badge/build-passing-brightgreen?style=for-the-badge&logo=github)
![dotnet-9](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=.net&logoColor=white)

**Turning 500 Internal Server Errors into 200 OKs automatically.**

## The Problem
Modern cloud observability is expensive and noisy. Small teams and solo developers cannot afford enterprise SRE agents (costing $4+/hour) or DataDog subscriptions just to monitor a side project. When an incident occurs, the context switching between Azure Portal logs, GitHub Actions failures, and code editors delays remediation.

## The Solution
Agent 200 is a "Local-First" Autonomous SRE agent that runs on your own hardware (or free-tier compute). It uses a Tiered Intelligence architecture to monitor, diagnose, and fix infrastructure issues at near-zero cost.

Instead of sending terabytes of logs to a central cloud (expensive), Agent 200 brings the AI to the data source.

### Key Features

**Tier 1: Zero-Cost Watchdog**
*   Runs locally using Phi-3-mini or rule-based logic.
*   Polls Azure Monitor metrics 24/7 for free.
*   **Intelligent Handoff**: Only wakes up the "expensive" cloud agent when an anomaly (e.g., CPU spike > 50%) is detected.

**Tier 2: Cloud Investigator (Autonomous RCA)**
*   Powered by Azure OpenAI (**GPT-4o-mini**).
*   **Multi-Platform Reasoning**: Connects to Azure MCP (metrics) and GitHub MCP (logs/repo) to correlate platform events with code changes.
*   **Autonomous Documentation**: Generates a detailed Root Cause Analysis (RCA) report with remediation recommendations.

**Tier 3: The Fixer (Beta)**
*   Correlates infrastructure analysis with source code.
*   **Automated Remediation**: Drafts Pull Requests with fixes using GitHub Copilot Agent Mode logic.
*   **Human-in-the-Loop**: Creates a PR for review, never merges automatically.

## Architecture

```mermaid
graph TD
    subgraph Local_Environment ["Local Machine / Edge"]
        Watchdog["Watchdog"] -->|Polls| Metrics["Azure Monitor Metrics"]
        Watchdog -->|Triggers| Investigator["Investigator"]
    end

    subgraph Cloud_Intelligence ["Azure AI Foundry"]
        Investigator -->|Reasoning| GPT4["GPT-4o-mini"]
    end

    subgraph Tools_Layer ["Model Context Protocol"]
        Investigator -->|MCP| AzureTool["Azure MCP Tools"]
        Investigator -->|MCP| GitHubTool["GitHub MCP Tools"]
    end

    subgraph Remediation
        Investigator -->|Root Cause| Fixer["Fixer"]
        Fixer -->|Create PR| GitHubRepo["GitHub Repository"]
    end

    style Watchdog fill:#e6fffa,stroke:#00b894
    style Investigator fill:#e3f2fd,stroke:#0984e3
    style Fixer fill:#fff0f6,stroke:#fd79a8
```

## Getting Started

### Prerequisites
*   [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   [Node.js](https://nodejs.org/) (for MCP Servers)
*   Azure Subscription (Free Tier works)
*   GitHub Account

### Installation

1.  **Clone the repository**
    ```bash
    git clone https://github.com/YOUR_USERNAME/agent-200.git
    cd agent-200
    ```

2.  **Install MCP Servers**
    ```bash
    npm install -g @azure/mcp
    ```

3.  **Configure Secrets**
    *   Do not commit keys! Use User Secrets for the Host project.
    ```bash
    cd src/Agent200.Host
    dotnet user-secrets init
    
    # Azure OpenAI Configuration
    dotnet user-secrets set "AzureOpenAI:Endpoint" "https://YOUR_RES.openai.azure.com/"
    dotnet user-secrets set "AzureOpenAI:Key" "YOUR_KEY"
    dotnet user-secrets set "AzureOpenAI:Deployment" "gpt-4o-mini"
    
    # Azure Subscription Details (for MCP)
    dotnet user-secrets set "Azure:SubscriptionId" "YOUR_SUBSCRIPTION_ID"
    dotnet user-secrets set "Azure:TenantId" "YOUR_TENANT_ID"
    
    # GitHub Integration
    dotnet user-secrets set "GitHub:Token" "your_actual_github_pat"
    ```

4.  **Run the Agent**
    ```bash
    dotnet run
    ```

5.  **Running Tests**
    Agent 200 includes a comprehensive unit test suite to ensure core logic (metric evaluation, tool mapping) and autonomous handoff remains stable.
    
    *   **Health Evaluator Tests**: Validates metric parsing for various Azure JSON formats.
    *   **Watchdog Handoff Tests**: Uses **Moq** and interface-based abstraction (`IMcpClient`) to verify Tier 1 to Tier 2 transitions without requiring live cloud resources.
    ```bash
    cd src/Agent200.Tests
    dotnet test
    ```

## Usage Example

**Scenario:** A deployment failed 5 minutes ago.

1.  **Watchdog** detects `HTTP 500` spike on `app-service-prod`.
2.  **Agent 200** wakes up and queries Azure MCP: *"What changed in the last 10 minutes?"*
3.  **Azure MCP** reports: *"Resource health is degraded. Last deployment failed."*
4.  **Agent 200** queries GitHub MCP: *"Get logs for the last failed run."*
5.  **Agent 200** identifies: *"Error: NullReferenceException in UserService.cs line 42."*
6.  **Agent 200 (Fixer)** takes action: *"Creating a fix branch and applying null check."*
7.  **Agent 200** outputs: *"Pull Request #5 created for your review."*

## Built With
*   **Orchestration:** [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
*   **Language:** C# / .NET 9
*   **Connectivity:** [Model Context Protocol (MCP)](https://modelcontextprotocol.io/)
*   **AI Models:** Azure OpenAI (GPT-4o-mini), Phi-3
*   **Infrastructure:** Azure Container Apps

## Contributing
This is a hackathon project, but contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
