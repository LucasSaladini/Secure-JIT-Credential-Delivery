# Project: Secure Credential Delivery (SCD)

## Status: 🚧 Developing (Initial Architecture)

### 📋 Overview
This project establishes a layer of abstraction of security for applications that need access to sensitive resources. The solution acts as a credential provider **Just-In-Time (JIT)**, assuring that the secret (being a Connection String, API Key or Token) is never stored on a static way on the client side.

The architecture focus on reducing the attack surface by centralizing the authentication and the secret delivery on an controlled and auditable environment.

### 🏗️ Architecture & Technologies
- **Core**: .NET (C#) following the Clean Architecture and SOLID principles.

- **Infrastructure**: Cloud Native (Azure), using Key Vault for secrets and Managed Identitie.

- **Security Patterns**: 
    - **Zero Trust Architecture**: Explicit verification of every request 

    - **JIT Credentialing**: Minimal exposition time of credentials.

- **Quality Assurance**: Unit and integration tests to ensure the security flow integrity.

- **DevOps**: Automation through CI/CD Pipelines focusing on security (DevSecOps).

### 📂 Roadmap
- **Core API**: Implementation of Security Gateway.

- **Vault Integration**: Conector for secrets providers on cloud

- **Auditoria**: Credentials access and delivery record logs.