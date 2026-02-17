# Dockerfile for Agent 200 Host
# This image includes .NET 9.0 and Node.js to support MCP servers.

# --- Stage 1: Build ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and restore dependencies
COPY src/*.sln ./src/
COPY src/Agent200.Host/*.csproj ./src/Agent200.Host/
COPY src/Agent200.Tests/*.csproj ./src/Agent200.Tests/
RUN dotnet restore src/*.sln

# Copy everything else and build
COPY . .
WORKDIR /app/src/Agent200.Host
RUN dotnet publish -c Release -o /app/publish

# --- Stage 2: Runtime ---
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

# Install Node.js and npm (required for MCP servers via npx)
RUN apt-get update && \
    apt-get install -y curl gnupg && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy the build artifacts
COPY --from=build /app/publish .

# Set environment variables for non-interactive execution
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_URLS=http://+:8080

# The agent runs as a background service, but we can expose a port for health probes if needed.
EXPOSE 8080

ENTRYPOINT ["dotnet", "Agent200.Host.dll"]
