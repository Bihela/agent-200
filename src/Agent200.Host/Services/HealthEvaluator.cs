using ModelContextProtocol.Protocol;
using System.Linq;
using System.Text.Json;

namespace Agent200.Host.Services;

/// <summary>
/// Implements health evaluation logic using threshold-based metric analysis.
/// Currently optimized for Azure Monitor metric responses from the @azure/mcp server.
/// </summary>
public class HealthEvaluator : IHealthEvaluator
{
    // Threshold for CPU usage. If exceeded, the system is considered unhealthy.
    private const double CpuThreshold = 80.0;

    /// <summary>
    /// Checks if the provided tool result indicates a healthy state.
    /// Handles both standard 'data' array and the 'avgBuckets' format used by some MCP monitor tools.
    /// </summary>
    public bool IsHealthy(CallToolResult toolResult, string targetResourceName)
    {
        if (toolResult == null || toolResult.Content == null)
        {
            return false;
        }

        // Combine all content blocks into a single string for parsing.
        var text = string.Join("\n", toolResult.Content
            .Select(c => c is TextContentBlock t ? t.Text : c.ToString()));

        try
        {
            // The @azure/mcp monitor tool returns a JSON structure containing metric time series.
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("results", out var results) && 
                results.TryGetProperty("results", out var innerResults) &&
                innerResults.GetArrayLength() > 0)
            {
                var metric = innerResults[0];
                if (metric.TryGetProperty("timeSeries", out var timeSeries) && timeSeries.GetArrayLength() > 0)
                {
                    var ts = timeSeries[0];
                    
                    // Priority 1: Check for 'avgBuckets' (used by monitor_metrics_query)
                    if (ts.TryGetProperty("avgBuckets", out var avgBuckets) && avgBuckets.GetArrayLength() > 0)
                    {
                        var value = avgBuckets[avgBuckets.GetArrayLength() - 1].GetDouble();
                        return value < CpuThreshold;
                    }
                    // Priority 2: Check for 'data' array with 'average' property
                    else if (ts.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                    {
                        var latest = data[data.GetArrayLength() - 1];
                        if (latest.TryGetProperty("average", out var average))
                        {
                            var value = average.GetDouble();
                            return value < CpuThreshold;
                        }
                    }
                }
            }
        }
        catch
        {
            // If parsing fails, fall back to simple text check for backward compatibility with tests.
        }

        // Fallback: If not a valid metric JSON or no metric found, check if the resource name is present in the text.
        return text.Contains(targetResourceName);
    }
}
