namespace SecureGateway.Shared.DTOs;

public record CredentialResponse(
    string SecretValue,
    DateTime ExpirationUtc,
    string AuditId
);