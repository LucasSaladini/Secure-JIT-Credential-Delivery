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

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<VaultService>();

        services.AddSingleton<IVaultService>(sp =>
        {
            var inner = sp.GetRequiredService<VaultService>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            var logger = sp.GetRequiredService<ILogger<CachedVaultService>>();
            var m = sp.GetRequiredService<Meter>();
            
            return new CachedVaultService(inner, cache, logger, m);
        });
    })
    .Build();

host.Run();