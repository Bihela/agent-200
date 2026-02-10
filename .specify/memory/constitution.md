# Agent 200 Constitution

## Core Principles

**Zero Cost First**: Always prefer local models (Phi-3) and free tier resources. Only escalate to Azure OpenAI when reasoning requires it.

**Security**: Never commit API keys. Use `DefaultAzureCredential`.

**Architecture**: Use Microsoft Agent Framework (.NET 9) for orchestration and MCP for tool connectivity.
