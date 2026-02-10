using ModelContextProtocol.Protocol;

namespace Agent200.Host.Services;

public interface IHealthEvaluator
{
    bool IsHealthy(CallToolResult toolResult, string targetResourceName);
}
