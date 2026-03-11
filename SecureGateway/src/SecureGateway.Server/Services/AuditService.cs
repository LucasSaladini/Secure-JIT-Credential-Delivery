using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SecureGateway.Shared.DTOs;

namespace SecureGateway.Server.Services;

public interface IAuditService
{
    Task<Guid> LogAccessAsync(CredentialRequest request, bool success, string message, string ipAddress);
}

public class AuditService(IConfiguration configuration) : IAuditService
{
    public async Task<Guid> LogAccessAsync(CredentialRequest request, bool success, string message, string ipAddress)
    {
        var connectionString = configuration.GetConnectionString("SqlConnectionString");
        var auditId = Guid.NewGuid();

        using var connection = new SqlConnection(connectionString);

        const string sql = @"INSERT INTO AccessLogs
                                (Id, ClientId, ResourceKey, IsSuccess, AuditMessage, ClientIp)
                                VALUES (@Id, @ClinetId, @ResourceKey, @IsSuccess, @AuditMessage, @ClientIp)
                            ";

        await connection.ExecuteAsync(sql, new {
            Id = auditId,
            ClientId = request.ClientId,
            ResourceKey = request.ResourceKey,
            IsSuccess = success,
            AuditMessage = message,
            ClientIp = ipAddress
        });

        return auditId;
    }
}