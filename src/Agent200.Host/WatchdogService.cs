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

        var client = await _mcpService.GetClientAsync();
        
        var tenant = "c1bf6a72-079d-4859-a0e4-630a4c416f80";
        var subscription = "57eaaae6-c0cf-49b9-b983-8175c001de92";
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
