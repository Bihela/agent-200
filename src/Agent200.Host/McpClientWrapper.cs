using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Collections.ObjectModel;

namespace Agent200.Host;

/// <summary>
/// A wrapper for the <see cref="McpClient"/> that implements <see cref="IMcpClient"/>.
/// This allows the concreted MCP client library to be used while still supporting testing through the interface.
/// </summary>
public class McpClientWrapper : IMcpClient
{
    private readonly McpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpClientWrapper"/> class.
    /// </summary>
    /// <param name="client">The underlying MCP client to wrap.</param>
    public McpClientWrapper(McpClient client)
    {
        _client = client;
    }

    /// <inheritdoc />
    public ValueTask<CallToolResult> CallToolAsync(string name, ReadOnlyDictionary<string, object?>? arguments = null, IProgress<ProgressNotificationValue>? progress = null, RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return _client.CallToolAsync(name, arguments, progress, options, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IEnumerable<McpClientTool>> ListToolsAsync(RequestOptions? options = null, CancellationToken cancellationToken = default)
    {
        return await _client.ListToolsAsync(options, cancellationToken);
    }
}
