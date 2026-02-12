using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Collections.ObjectModel;

namespace Agent200.Host.Services;

public class WatchdogService : BackgroundService
{
    private readonly ILogger<WatchdogService> _logger;
    private readonly IConfiguration _config;
    private readonly McpService _mcpService;
    private readonly IHealthEvaluator _healthEvaluator;
    
    private const int PollingIntervalSeconds = 60;

    public WatchdogService(
        ILogger<WatchdogService> logger, 
        IConfiguration config, 
        McpService mcpService,
        IHealthEvaluator healthEvaluator)
    {
        _logger = logger;
        _config = config;
        _mcpService = mcpService;
        _healthEvaluator = healthEvaluator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🐶 Watchdog Service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Watchdog cycle.");
            }

            await Task.Delay(TimeSpan.FromSeconds(PollingIntervalSeconds), stoppingToken);
        }
    }

    private async Task CheckMetricsAsync(CancellationToken ct)
    {
        _logger.LogInformation("🔍 Checking Azure metrics (Demonstration mode using Cognitive Services)...");

        var tenant = _config["Azure:TenantId"];
        var subscription = _config["Azure:SubscriptionId"];
        
        if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(subscription))
        {
            _logger.LogWarning("⚠️ Azure:TenantId or Azure:SubscriptionId missing in config. Skipping metrics check.");
            return;
        }

        var client = await _mcpService.GetClientAsync(subscription, tenant);
        
        var targetResource = "rg-opsweaver-hackathon";

        // Using Cognitive Services as a demonstration because App Service Free Tier fails with 404 on metric queries
        var toolArgs = new Dictionary<string, object?>
        {
            ["intent"] = "metrics",
            ["command"] = "monitor_metrics_query",
            ["parameters"] = new Dictionary<string, object?> {
                ["subscription"] = subscription,
                ["tenant"] = tenant,
                ["resource-group"] = targetResource,
                ["resource-type"] = "Microsoft.CognitiveServices/accounts",
                ["resource"] = "opsweaver-GPT", 
                ["metric-names"] = "ModelAvailabilityRate",
                ["metric-namespace"] = "Microsoft.CognitiveServices/accounts",
                ["interval"] = "PT1M",
                ["aggregation"] = "Average"
            }
        };
        
        try 
        {
             var result = await client.CallToolAsync("monitor", new ReadOnlyDictionary<string, object?>(toolArgs), null, null, ct);
             
             var text = string.Join("\n", result.Content.Select(c => c is TextContentBlock t ? t.Text : c.ToString()));
             _logger.LogInformation($"📊 Metric Response START:\n{text}\nMetric Response END");

             if (!string.IsNullOrEmpty(text) && (text.Contains("ModelAvailabilityRate") || text.Contains("Success")))
             {
                 _logger.LogInformation("✅ Watchdog: Metric retrieved successfully (Demonstration).");
             }
             else
             {
                 _logger.LogWarning($"⚠️ Watchdog: Unexpected response from monitor tool. Length: {text.Length}");
             }
        }
        catch(Exception ex)
        {
             _logger.LogError(ex, "Failed to call monitor_metrics_query tool.");
        }
    }
}
