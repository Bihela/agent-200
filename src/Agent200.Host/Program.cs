using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Agent200.Host;
using Agent200.Host.Services;

var builder = Host.CreateApplicationBuilder(args);

// Add Services
builder.Services.AddSingleton<McpService>();
builder.Services.AddSingleton<IHealthEvaluator, HealthEvaluator>();
builder.Services.AddHostedService<WatchdogService>();

// Configure Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 Agent 200 Host started.");

await host.RunAsync();
