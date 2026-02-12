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
        _logger.LogInformation("🔍 Checking Azure metrics...");

        var tenant = _config["Azure:TenantId"];
        var subscription = _config["Azure:SubscriptionId"];
        
        if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(subscription))
        {
            _logger.LogWarning("⚠️ Azure:TenantId or Azure:SubscriptionId missing in config. Skipping metrics check.");
            return;
        }

        var client = await _mcpService.GetClientAsync(subscription, tenant);
        
        var targetResource = "rg-opsweaver-hackathon";

        var args = new Dictionary<string, object?>
        {
            ["tenant"] = tenant,
            ["subscription"] = subscription
        };

        var result = await client.CallToolAsync("group_list", new ReadOnlyDictionary<string, object?>(args), null, null, ct);
        
        if (_healthEvaluator.IsHealthy(result, targetResource))
        {
            _logger.LogInformation("✅ System Healthy: Hackathon Resource Group is visible.");
        }
        else
        {
            _logger.LogWarning("⚠️ HIGH ALERT: Target resource group not found or inaccessible!");
        }
    }
}
