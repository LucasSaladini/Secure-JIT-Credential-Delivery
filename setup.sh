#!/bin/bash

# ============================================================================
# FASE 2: CRIAR ESTRUTURA DE PASTAS
# ============================================================================
echo -e "\n\e[33m[2/7] Criando estrutura de pastas...\e[0m"

rootPath="SecureGateway"
mkdir -p "$rootPath"
cd "$rootPath" || exit

# Criar estrutura básica
mkdir -p src infra/bicep scripts/sql scripts/deploy docs

# ============================================================================
# FASE 3: CRIAR SOLUTION E PROJETOS (COM FALLBACK)
# ============================================================================
echo -e "\n\e[33m[3/7] Criando solution e projetos .NET...\e[0m"

dotnet new sln -n SecureGateway
cd src

# 1. Shared
echo -e "  \e[36m• Criando SecureGateway.Shared...\e[0m"
dotnet new classlib -n SecureGateway.Shared -f net8.0
dotnet sln ../SecureGateway.sln add SecureGateway.Shared/SecureGateway.Shared.csproj

# 2. Server (Azure Functions Isolated .NET 8)
echo -e "  \e[36m• Preparando templates Modernos (Isolated)...\e[0m"
# TROCA: Instalando o template de Worker em vez do WebJobs (Legacy)
dotnet new install Microsoft.Azure.Functions.Worker.ProjectTemplates --force > /dev/null

echo -e "  \e[36m• Tentando criar SecureGateway.Server via template 'func' (Isolated)...\e[0m"

# Usando as flags que seu 'dotnet new func -h' confirmou: -F em vez de --target-framework
if dotnet new func -n SecureGateway.Server -F net8.0 --output SecureGateway.Server; then
    # FORÇA O MODO ISOLADO: Adiciona OutputType Exe se não existir
    if ! grep -q "<OutputType>Exe</OutputType>" SecureGateway.Server/SecureGateway.Server.csproj; then
        sed -i '/<TargetFramework>/a \    <OutputType>Exe</OutputType>' SecureGateway.Server/SecureGateway.Server.csproj
    fi
    echo -e "  \e[32m✓ Projeto Server criado com sucesso (Isolated .NET 8).\e[0m"
else
    echo -e "  \e[33m! Fallback: Criando como console e convertendo...\e[0m"
    dotnet new console -n SecureGateway.Server -f net8.0
fi

dotnet sln ../SecureGateway.sln add SecureGateway.Server/SecureGateway.Server.csproj

# ============================================================================
# FASE 4: ADICIONAR DEPENDÊNCIAS E RECURSOS
# ============================================================================
echo -e "\n\e[33m[4/7] Configurando dependências e Isolated Worker...\e[0m"

serverProj="SecureGateway.Server/SecureGateway.Server.csproj"

if [ -f "$serverProj" ]; then
    # Limpando pacotes antigos (In-Process) se houverem
    dotnet remove "$serverProj" package Microsoft.NET.Sdk.Functions > /dev/null 2>&1

    # Adicionando pacotes ESTÁVEIS para .NET 8 Isolated
    dotnet add "$serverProj" package Microsoft.Azure.Functions.Worker --version 1.21.0
    dotnet add "$serverProj" package Microsoft.Azure.Functions.Worker.Sdk --version 1.17.2
    dotnet add "$serverProj" package Microsoft.Azure.Functions.Worker.Extensions.Http --version 3.1.0
    
    # Seus pacotes de segurança e dados
    dotnet add "$serverProj" package Azure.Security.KeyVault.Secrets
    dotnet add "$serverProj" package Azure.Identity
    dotnet add "$serverProj" package OtpNet
    dotnet add "$serverProj" package Microsoft.Data.SqlClient
    dotnet add "$serverProj" reference SecureGateway.Shared/SecureGateway.Shared.csproj
    
    # Criando o Program.cs (Obrigatório para o modelo Isolated)
    cat <<EOF > SecureGateway.Server/Program.cs
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services => {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();
EOF

    mkdir -p SecureGateway.Server/Functions SecureGateway.Server/Services
else
    echo -e "  \e[31m✘ ERRO FATAL: O projeto Server não foi encontrado.\e[0m"
    exit 1
fi
