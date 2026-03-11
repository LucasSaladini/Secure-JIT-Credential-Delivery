using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SecureGateway.Shared.DTOs;
using SecureGateway.Server.Interfaces;
using SecureGateway.Server.Services;

namespace SecureGateway.Server.Functions;

public class HandshakeFunction
{
    private readonly ISecurityService _securityService;
    private readonly ILogger<HandshakeFunction> _logger;
    private readonly IAuditService _auditService;
    private readonly IVaultService _vaultService;
    
    public HandshakeFunction(ISecurityService securityService, ILoggerFactory loggerFactory, IAuditService auditService, IVaultService vaultService)
    {
        _securityService = securityService;
        _logger = loggerFactory.CreateLogger<HandshakeFunction>();
        _auditService = auditService;
        _vaultService = vaultService;
    }

    [Function("CredentialHandshake")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var request = await req.ReadFromJsonAsync<CredentialRequest>();
        
        if (request == null)
        {
            _logger.LogWarning("Empty request body received.");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var clientIp = req.Headers.TryGetValues("X-Forwarded-For", out var values) 
                       ? values.FirstOrDefault() ?? "0.0.0.0" 
                       : "0.0.0.0";

        // TODO: Em produção, buscar de uma Secret Store real
        const string mockClientSecret = "SHARED_MASTER_KEY";

        if (!_securityService.IsValidSignature(request, mockClientSecret))
        {
            await _auditService.LogAccessAsync(request, false, "Invalid Signature", clientIp);
            _logger.LogWarning("Invalid signature for ClientId: {ClientId}", request.ClientId);
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var auditId = await _auditService.LogAccessAsync(request, true, "Access Granted", clientIp);

        try 
        {
            string secretValue = await _vaultService.GetSecretAsync(request.ResourceKey);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new CredentialResponse(
                secretValue,
                DateTime.UtcNow.AddMinutes(5),
                auditId.ToString()
            ));
            return response;
        }
        catch (Exception)
        {
            _logger.LogError("Key Vault Retrieval Failed for ClientIp: {clientIp}", clientIp);
            await _auditService.LogAccessAsync(request, false, "Key Vault Retrieval Failed", clientIp);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}