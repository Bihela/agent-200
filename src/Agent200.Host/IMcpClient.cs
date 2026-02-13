using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Client;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Agent200.Host;

/// <summary>
/// Represents a client for the Model Context Protocol (MCP).
/// This interface abstracts the underlying McpClient to allow for easier testing and mocking.
/// </summary>
public interface IMcpClient
{
    /// <summary>
    /// Invokes a tool on the MCP server.
    /// </summary>
    /// <param name="name">The name of the tool to call.</param>
    /// <param name="arguments">The arguments to pass to the tool.</param>
    /// <param name="progress">An optional progress reporter for long-running tools.</param>
    /// <param name="options">Optional request configuration.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the tool invocation.</returns>
    ValueTask<CallToolResult> CallToolAsync(string name, ReadOnlyDictionary<string, object?>? arguments = null, IProgress<ProgressNotificationValue>? progress = null, RequestOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the tools available on the MCP server.
    /// </summary>
    /// <param name="options">Optional request configuration (e.g., for pagination).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A collection of available tools.</returns>
    ValueTask<IEnumerable<McpClientTool>> ListToolsAsync(RequestOptions? options = null, CancellationToken cancellationToken = default);
}
