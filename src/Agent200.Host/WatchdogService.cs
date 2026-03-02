using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Collections.ObjectModel;

namespace Agent200.Host.Services;

/// <summary>
/// The Watchdog Service is the "Tier 1" agent. It runs continuously in the background,
/// low-cost monitoring for system anomalies (e.g., CPU spikes).
/// When an anomaly is found, it orchestrates the transition to "Tier 2" (Investigator)
/// and "Tier 3" (Fixer).
/// </summary>
public class WatchdogService : BackgroundService
{
    private readonly ILogger<WatchdogService> _logger;
    private readonly IConfiguration _config;
    private readonly IMcpService _mcpService;
    private readonly IHealthEvaluator _healthEvaluator;
    private readonly IInvestigatorAgent _investigator;
    private readonly IFixerAgent _fixer;
    
    private const int PollingIntervalSeconds = 60; // Increased to 60s for demo safety

    public WatchdogService(
        ILogger<WatchdogService> logger, 
        IConfiguration config, 
        IMcpService mcpService,
        IHealthEvaluator healthEvaluator,
        IInvestigatorAgent investigator,
        IFixerAgent fixer)
    {
        _logger = logger;
        _config = config;
        _mcpService = mcpService;
        _healthEvaluator = healthEvaluator;
        _investigator = investigator;
        _fixer = fixer;
    }

    /// <summary>
    /// Background service that polls Azure metrics and evaluates system health.
    /// Polling interval is currently set to 60 seconds.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start the background polling loop.
        _logger.LogInformation("[Watchdog] Service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_config["Watchdog:Enabled"]?.ToLower() == "false")
                {
                    _logger.LogInformation("[Watchdog] Service is disabled via configuration.");
                }
                else
                {
                    await CheckMetricsAsync(stoppingToken);
                }
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
        _logger.LogInformation("[Watchdog] Checking Azure metrics...");

        var tenant = _config["Azure:TenantId"];
        var subscription = _config["Azure:SubscriptionId"];
        
        if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(subscription))
        {
            _logger.LogWarning("[Watchdog] Azure:TenantId or Azure:SubscriptionId missing in config. Skipping metrics check.");
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
             _logger.LogInformation($"[Watchdog] Metric Response (first 500 chars):\n{logText}");

             // Evaluate health based on the metric result.
             bool isHealthy = _healthEvaluator.IsHealthy(result, targetResource);
             
             if (isHealthy)
             {
                 _logger.LogInformation("[Watchdog] System is healthy.");
             }
             else
             {
                  _logger.LogWarning("[Watchdog] CPU SPIKE DETECTED! Awakening Tier 2 (Investigator) and Tier 3 (Fixer) via Agent Workflow...");
                  
                  // TRIGGER MULTI-AGENT WORKFLOW:
                  // 1. Investigator evaluates Root Cause (RCA).
                  // 2. Fixer applies remediation (PR).
                  
                  // Manual Handoff Workflow (Fallback due to Framework Preview Issues)
                  // ------------------------------------------------------------------
                  // Due to current limitations in the preview version of Microsoft.Agents.AI.Workflows 
                  // (specifically with HandoffsWorkflowBuilder), we are manually orchestrating the agent loop here.
                  // 
                  // 1. Investigator Agent: Analyze Root Cause
                  _logger.LogInformation("[Watchdog] Waiting 5s for quota reset...");
                  await Task.Delay(5000, ct); // Safety delay for 429 prevention
                  
                  _logger.LogInformation("[Watchdog] Starting Investigation...");
                  string rcaReport = await _investigator.InvestigateAnomalyAsync($"Anomaly detected: CPU spike on {targetResource}. Metrics: {logText}");
                  _logger.LogInformation($"[Watchdog] Investigation Complete. RCA: {rcaReport}");

                  // 2. Fixer Agent: Apply Remediation (if RCA is valid)
                  //    - Only triggers if the Investigator found a plausible root cause.
                  //    - Uses the RCA to determine the correct fix (e.g., revert commit, update config).
                  //    - This is the Tier 3 agent that performs autonomous "writes" to GitHub.
                  if (!string.IsNullOrWhiteSpace(rcaReport) && !rcaReport.Contains("No root cause identified"))
                  {
                      _logger.LogInformation("[Watchdog] Starting Remediation...");
                      string remediationSummary = await _fixer.RemediateAsync(rcaReport);
                      _logger.LogInformation($"[Watchdog] Remediation Complete. Summary: {remediationSummary}");
                  }
                  else
                  {
                      _logger.LogWarning("[Watchdog] Skipping Remediation: No valid root cause identified.");
                  }
              }
        }
        catch(Exception ex)
        {
             _logger.LogError(ex, "Failed to call monitor_metrics_query tool.");
        }
    }
}
