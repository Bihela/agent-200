using ModelContextProtocol.Protocol;

namespace Agent200.Host.Services;

/// <summary>
/// Defines a contract for evaluating the health of a resource based on MCP tool results.
/// </summary>
public interface IHealthEvaluator
{
    /// <summary>
    /// Evaluates if a resource is healthy based on the provided tool results.
    /// </summary>
    /// <param name="toolResult">The result returned from an MCP monitor tool call.</param>
    /// <param name="targetResourceName">The name of the resource to evaluate.</param>
    /// <returns>True if the resource is healthy, false otherwise.</returns>
    bool IsHealthy(CallToolResult toolResult, string targetResourceName);
}
