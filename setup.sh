#!/bin/bash

# ============================================================================
# PHASE 2: CREATE FOLDER STRUCTURE
# ============================================================================
echo -e "\n\e[33m[2/7] Creating folder structure...\e[0m"

rootPath="SecureGateway"
mkdir -p "$rootPath"
cd "$rootPath" || exit

# Creating basic structure
mkdir -p src infra/bicep scripts/sql scripts/deploy docs

# ============================================================================
# PHASE 3: CREATE SOLUTION AND PROJECTS (WITH FALLBACK)
# ============================================================================
echo -e "\n\e[33m[3/7] Creating solution and projects .NET...\e[0m"

dotnet new sln -n SecureGateway
cd src

# 1. Shared
echo -e "  \e[36m Creating SecureGateway.Shared...\e[0m"
dotnet new classlib -n SecureGateway.Shared -f net8.0
dotnet sln ../SecureGateway.sln add SecureGateway.Shared/SecureGateway.Shared.csproj

# 2. Server (Azure Functions Isolated .NET 8)
echo -e "  \e[36m• Preparando templates Modernos (Isolated)...\e[0m"
# EXCHANGE: Installing Worker template instead of WebJobs
dotnet new install Microsoft.Azure.Functions.Worker.ProjectTemplates --force > /dev/null

echo -e "  \e[36m Trying to create SecureGateway.Server through template 'func' (Isolated)...\e[0m"

if dotnet new func -n SecureGateway.Server -F net8.0 --output SecureGateway.Server; then
    # FORCES ISOLATED MODE: Adds OutputType Exe if not exists
    if ! grep -q "<OutputType>Exe</OutputType>" SecureGateway.Server/SecureGateway.Server.csproj; then
        sed -i '/<TargetFramework>/a \    <OutputType>Exe</OutputType>' SecureGateway.Server/SecureGateway.Server.csproj
    fi
    echo -e "  \e[32m✓ Project Server successfully created (Isolated .NET 8).\e[0m"
else
    echo -e "  \e[33m! Fallback: Creating as console and converting...\e[0m"
    dotnet new console -n SecureGateway.Server -f net8.0
fi

dotnet sln ../SecureGateway.sln add SecureGateway.Server/SecureGateway.Server.csproj

# ============================================================================
# PHASE 4: ADD DEPENDENCIES AND RESOURCES
# ============================================================================
echo -e "\n\e[33m[4/7] Setting up dependencies and Isolated Worker...\e[0m"

serverProj="SecureGateway.Server/SecureGateway.Server.csproj"

if [ -f "$serverProj" ]; then
    # Cleaning old packages (In-Process) if they exists
    dotnet remove "$serverProj" package Microsoft.NET.Sdk.Functions > /dev/null 2>&1

    # Adding STABLE packages for .NET 8 Isolates
    dotnet add "$serverProj" package Microsoft.Azure.Functions.Worker --version 1.21.0
    dotnet add "$serverProj" package Microsoft.Azure.Functions.Worker.Sdk --version 1.17.2
    dotnet add "$serverProj" package Microsoft.Azure.Functions.Worker.Extensions.Http --version 3.1.0
    
    # Security and data packages
    dotnet add "$serverProj" package Azure.Security.KeyVault.Secrets
    dotnet add "$serverProj" package Azure.Identity
    dotnet add "$serverProj" package OtpNet
    dotnet add "$serverProj" package Microsoft.Data.SqlClient
    dotnet add "$serverProj" package Dapper
    dotnet add "$serverProj" package Microsoft.Extensions.Logging.ApplicationInsights
    dotnet add "$serverProj" reference SecureGateway.Shared/SecureGateway.Shared.csproj
    dotnet add "$serverProj" package Microsoft.Extensions.Logging.ApplicationInsights
    dotnet add "$serverProj" package Microsoft.Extensions.Caching.Memory
    dotnet add "$serverProj" package Microsoft.Extensions.Configuration.Binder
    dotnet add "$serverProj" package Microsoft.Extensions.Http.Polly
    dotnet add "$serverProj" package System.Diagnostics.DiagnosticSource

    # Creating Program.cs 
    cat <<EOF > SecureGateway.Server/Program.cs
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SecureGateway.Shared;
using SecureGateway.Server.Services;
using Microsoft.Extensions.Azure;
using Azure.Identity;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddScoped<IAuditService, AuditService>();
    })
    .Build()

host.Run();

EOF

    mkdir -p SecureGateway.Server/Functions SecureGateway.Server/Services SecureGateway.Server/Interfaces
else
    echo -e "  \e[31m✘ FATAL ERROR: The Server project was not found.\e[0m"
    exit 1
fi
