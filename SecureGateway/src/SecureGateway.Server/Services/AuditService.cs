using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SecureGateway.Shared.DTOs;

namespace SecureGateway.Server.Services;

public interface IAuditService
{
    Task<Guid> LogAccessAsync(CredentialRequest request, bool success, string message, string ipAddress);
}

public class AuditService(IConfiguration configuration, ILogger<AuditService> logger) : IAuditService
{
    public async Task<Guid> LogAccessAsync(CredentialRequest request, bool success, string reason, string clientIp)
    {
        var auditId = Guid.NewGuid();
        var sql = @"INSERT INTO SecurityAudits (Id, ClientId, ResourceKey, Success, Reason, ClientIp, RequestTimestamp)
                    VALUES (@Id, @ClientId, @ResourceKey, @Success, @Reason, @ClientIp, @RequestTimestamp)";

        try
        {
            using var connection = new SqlConnection(configuration.GetConnectionString("DefaultConnection"));
            await connection.ExecuteAsync(sql, new
            {
                Id = auditId,
                request.ClientId,
                request.ResourceKey,
                Success = success,
                Reason = reason,
                ClientIp = clientIp,
                RequestTimestamp = DateTime.UtcNow
            });
            
            return auditId;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Critical: Failed to persist audit log to SQL for ClientId: {ClientId}", request.ClientId);
            return auditId; 
        }
    }
}