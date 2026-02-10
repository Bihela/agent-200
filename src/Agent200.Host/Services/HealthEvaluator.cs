using ModelContextProtocol.Protocol;
using System.Linq;

namespace Agent200.Host.Services;

public class HealthEvaluator : IHealthEvaluator
{
    public bool IsHealthy(CallToolResult toolResult, string targetResourceName)
    {
        if (toolResult == null || toolResult.Content == null)
        {
            return false;
        }

        var text = string.Join("\n", toolResult.Content
            .Select(c => c is TextContentBlock t ? t.Text : c.ToString()));

        return text.Contains(targetResourceName);
    }
}
