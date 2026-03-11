namespace SecureGateway.Shared.DTOs;

public record CredentialRequest(
    string ClientId,
    string ResourceKey,
    string RequestHash
);