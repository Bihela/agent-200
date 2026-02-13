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
    private readonly IMcpService _mcpService;
    private readonly IHealthEvaluator _healthEvaluator;
    private readonly IInvestigatorAgent _investigator;
    
    private const int PollingIntervalSeconds = 60;

    public WatchdogService(
        ILogger<WatchdogService> logger, 
        IConfiguration config, 
        IMcpService mcpService,
        IHealthEvaluator healthEvaluator,
        IInvestigatorAgent investigator)
    {
        _logger = logger;
        _config = config;
        _mcpService = mcpService;
        _healthEvaluator = healthEvaluator;
        _investigator = investigator;
    }

    /// <summary>
    /// Background service that polls Azure metrics and evaluates system health.
    /// Polling interval is currently set to 60 seconds.
    /// </summary>
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

    /// <summary>
    /// Core monitoring logic. Connects to the Azure MCP monitor tool to retrieve
    /// CPU usage metrics and passes them to the HealthEvaluator for analysis.
    /// </summary>
    internal async Task CheckMetricsAsync(CancellationToken ct)
    {
        _logger.LogInformation("🔍 Checking Azure metrics...");

        var tenant = _config["Azure:TenantId"];
        var subscription = _config["Azure:SubscriptionId"];
        
        if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(subscription))
        {
            _logger.LogWarning("⚠️ Azure:TenantId or Azure:SubscriptionId missing in config. Skipping metrics check.");
            return;
        }

        var client = await _mcpService.GetAzureClientAsync(subscription, tenant);
        
        // Resource name of the App Service Plan to monitor.
        var targetResource = "asp-cpuspiker-free-central";

        // Arguments for the 'monitor_metrics_query' tool.
        // We use a 5-minute interval (PT5M) and look at the last 1 hour (PT1H) to avoid throttling and bucket limits.
        var toolArgs = new Dictionary<string, object?>
        {
            ["intent"] = "metrics",
            ["command"] = "monitor_metrics_query",
            ["parameters"] = new Dictionary<string, object?> {
                ["subscription"] = subscription,
                ["tenant"] = tenant,
                ["resource-group"] = "rg-opsweaver-hackathon",
                ["resource-type"] = "Microsoft.Web/serverfarms",
                ["resource"] = targetResource, 
                ["metric-names"] = "CpuPercentage",
                ["metric-namespace"] = "Microsoft.Web/serverfarms",
                ["interval"] = "PT5M",
                ["aggregation"] = "Average",
                ["timespan"] = "PT1H" // Last 1 hour
            }
        };
        
        try 
        {
             // Invoke the MCP monitor tool.
             var result = await client.CallToolAsync("monitor", new ReadOnlyDictionary<string, object?>(toolArgs), null, null, ct);
             
             var text = string.Join("\n", result.Content.Select(c => c is TextContentBlock t ? t.Text : c.ToString()));
             
             // Log the first 500 characters of the response for debugging purposes.
             var logText = text.Length > 500 ? text.Substring(0, 500) + "..." : text;
             _logger.LogInformation($"📊 Metric Response (first 500 chars):\n{logText}");

             // Evaluate health based on the metric result.
             bool isHealthy = _healthEvaluator.IsHealthy(result, targetResource);
             
             if (isHealthy)
             {
                 _logger.LogInformation("✅ Watchdog: System is healthy.");
             }
             else
             {
                 _logger.LogWarning("🚨 Watchdog: CPU SPIKE DETECTED! Triggering Investigator...");
                 
                 // TRIGGER TIER 2 HANDOFF:
                 // The Watchdog (Tier 1) has detected a rule-based anomaly.
                 // We now "awaken" the Investigator (Tier 2) for autonomous Root Cause Analysis.
                 // This tiered approach saves cost by only invoking expensive AI reasoning when necessary.
                 var rcaResponse = await _investigator.InvestigateAnomalyAsync($"CPU spike detected on {targetResource}. Metrics: {logText}");
                 
                 _logger.LogInformation("📄 Investigator RCA Output:\n{RCA}", rcaResponse);
                 
                 // FUTURE: Tier 3 (Fixer) handoff could be implemented here to automatically draft a PR.
             }
        }
        catch(Exception ex)
        {
             _logger.LogError(ex, "Failed to call monitor_metrics_query tool.");
        }
    }
}
