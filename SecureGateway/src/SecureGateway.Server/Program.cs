using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SecureGateway.Server.Interfaces;
using SecureGateway.Server.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) => {
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
            return new CachedVaultService(inner, cache, logger);
        });
    })
    .Build();

host.Run();