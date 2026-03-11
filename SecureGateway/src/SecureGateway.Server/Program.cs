using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SecureGateway.Server.Services;
using Microsoft.Extensions.Azure;
using Azure.Identity;
using SecureGateway.Server.Interfaces;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddAzureClients(clientBuilder => {
            var kvUri = Environment.GetEnvironmentVariable("KeyVaultUri") 
                ?? throw new InvalidOperationException("The environment variable 'KeyVaultUri' was not found.");
            clientBuilder.UseCredential(new DefaultAzureCredential());
        });

        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddScoped<IAuditService, AuditService>();
    })
    .Build();

host.Run();
