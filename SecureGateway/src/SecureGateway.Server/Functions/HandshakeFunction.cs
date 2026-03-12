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
            _logger.LogWarning("Security Check: {Result}. Reason: {Reason}", "BadRequest", "Empty Body");
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var clientIp = req.Headers.TryGetValues("X-Forwarded-For", out var values) 
                       ? values.FirstOrDefault() ?? "0.0.0.0" 
                       : "0.0.0.0";

        const string mockClientSecret = "SHARED_MASTER_KEY";

        if (!_securityService.IsValidSignature(request, mockClientSecret))
        {
            await _auditService.LogAccessAsync(request, false, "Invalid Signature", clientIp);
            _logger.LogWarning("Invalid signature for ClientId: {ClientId}", request.ClientId);
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["ClientId"] = request.ClientId,
            ["ClientIP"] = clientIp,
            ["ResourceKey"] = request.ResourceKey
        }))

        if (!_securityService.IsValidSignature(request, mockClientSecret))
        {
            await _auditService.LogAccessAsync(request, false, "Invalid Signature", clientIp);
            
            _logger.LogWarning("Security Check: {Result}. Reason: {Reason}", "Unauthorized", "Invalid Signature");
            
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var auditId = await _auditService.LogAccessAsync(request, true, "Access Granted", clientIp);

        try 
        {
            string mfaSeed = await _vaultService.GetSecretAsync($"MFA-SEED-{request.ClientId}");

            if(!_securityService.IsValidMfa(mfaSeed, request.OneTimePassword ?? ""))
            {
                await _auditService.LogAccessAsync(request, false, "MFA Validation Failed", clientIp);
                _logger.LogWarning("MFA Failed for ClientId: {ClientId}", request.ClientId);

                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }
             
            string secretValue = await _vaultService.GetSecretAsync(request.ResourceKey);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new CredentialResponse(
                secretValue,
                DateTime.UtcNow.AddMinutes(5),
                auditId.ToString()
            ));
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG ERROR: {ex.Message} | {ex.StackTrace}");
            _logger.LogError("Key Vault Retrieval Failed for ClientIp: {clientIp}", clientIp);
            await _auditService.LogAccessAsync(request, false, "Key Vault Retrieval Failed", clientIp);
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}