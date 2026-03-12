using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using SecureGateway.Server.Interfaces;
using SecureGateway.Server.Services;
using System.Diagnostics.Metrics;
using OpenTelemetry.Resources;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;
using System.Threading.RateLimiting;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) => {
        const string meterName = "SecureGateway.Handshake";
        
        var meter = new Meter(meterName);
        services.AddSingleton(meter);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("SecureGateway"))
            .WithMetrics(metrics => metrics
                .AddMeter(meterName)
                .AddRuntimeInstrumentation()
                .AddHttpClientInstrumentation()
                .AddPrometheusHttpListener()
            );

        services.AddMemoryCache(options => {
            options.SizeLimit = 1024;
        });

        var cbCounter = meter.CreateCounter<long>("vault_circuit_breaker_open_total");

        services.AddResiliencePipeline("vault-strategy", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2)
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                BreakDuration = TimeSpan.FromSeconds(15),
                OnOpened = args => 
                {
                    cbCounter.Add(1, new TagList { { "strategy", "vault-strategy" } });
                    return default;
                },
                OnClosed = args =>
                {
                    return default;
                }
            })
            .AddRateLimiter(new FixedWindowRateLimiter( new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100, 
                Window = TimeSpan.FromSeconds(1),
                QueueLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst 
            }));
        });

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<VaultService>();

        services.AddSingleton<IVaultService>(sp =>
        {
            var inner = sp.GetRequiredService<VaultService>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<CachedVaultService>>();
            var m = sp.GetRequiredService<Meter>();
            
            return new CachedVaultService(inner, cache, config, logger, m);
        });
    })
    .Build();

host.Run();