# Secure-JIT-Credential-Delivery 🛡️

[![CI/CD Pipeline](https://github.com/LucasSaladini/Secure-JIT-Credential-Delivery/actions/workflows/main_deploy.yml/badge.svg)](https://github.com/LucasSaladini/Secure-JIT-Credential-Delivery/actions)
![.NET 8/10](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-blue)

### 📋 Overview
A high-performance security abstraction layer designed to deliver **Just-In-Time (JIT) credentials**. This solution acts as a centralized provider for sensitive resources (Connection Strings, API Keys, Tokens), ensuring secrets are never stored statically on the client side.

**The goal:** Reduce the attack surface by implementing a Zero Trust approach with high availability and observability.

---

### 🏗️ Architecture & Engineering Highlights

This project goes beyond a simple API; it implements sophisticated design patterns to ensure enterprise-grade reliability:

* **Decorator Pattern**: Implemented to decouple the core Vault logic from the Caching and Metrics layers, keeping the code compliant with **SOLID (Open/Closed Principle)**.
* **Intelligent Caching**: Uses `IMemoryCache` with a dynamic TTL configuration to minimize Key Vault throttling and reduce latency.
* **Resilience & Fault Tolerance**: Integrated with **Polly** to handle transient cloud failures using Retry and Circuit Breaker policies.
* **Observability (OpenTelemetry ready)**: Custom metrics using `System.Diagnostics.Metrics` to track Cache Hits/Misses in real-time.
* **Isolated Worker Model**: Built on **Azure Functions (.NET 8 Isolated)** for better performance and separation of concerns.

---

### 🛠️ Tech Stack
- **Languages/Frameworks**: .NET 8.0 & .NET 10.0 (C#)
- **Cloud**: Azure (Key Vault, Managed Identity, App Insights)
- **Testing**: xUnit, Moq, FluentAssertions
- **DevOps**: GitHub Actions (CI/CD), `act` for local pipeline validation.

---

### 🧪 Quality Assurance
The project maintains a rigorous testing standard, validated through a CI/CD pipeline that ensures integrity across different .NET SDK versions.
- **17 Unit Tests**: Covering cache expiration, resilience policies, and dependency injection.
- **Local Pipeline Validation**: Full CI simulation using Docker/Act.


# How to run tests
```bash
dotnet test ./SecureGateway/SecureGateway.Tests
```

# 🛠️ Local Development & CI/CD Validation
To ensure the highest code quality and pipeline reliability, this project uses act to simulate GitHub Actions locally.

Prerequisites for CI/CD:
The pipeline is designed to build, test, and deploy. However, for a successful local simulation or remote execution, the following GitHub Secrets are required:

AZURE_FUNCTIONAPP_PUBLISH_PROFILE: The XML publish profile from your Azure Function App.

Running Pipeline Locally:
If you have Docker and act installed, you can validate the entire build and test flow (skipping the deploy step) with:

## Validates Restore, Build, and 17 Unit Tests
```Bash
act -j build-and-deploy
```

Note: The Run Azure Functions Action step will intentionally fail locally if no Azure credentials are provided, but the Build and Test phases will validate the code integrity.
