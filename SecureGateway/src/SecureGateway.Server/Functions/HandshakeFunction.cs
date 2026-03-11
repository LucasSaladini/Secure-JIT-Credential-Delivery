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
    
    public HandshakeFunction(ISecurityService securityService, ILoggerFactory loggerFactory, IAuditService auditService)
    {
        _securityService = securityService;
        _logger = loggerFactory.CreateLogger<HandshakeFunction>();
        _auditService = auditService;
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

        // TODO 3. Searches on Key Vault (Substituir "DUMMY_SECRET_FOR_NOW")

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new CredentialResponse(
            "DUMMY_SECRET_FOR_NOW",
            DateTime.UtcNow.AddMinutes(5),
            auditId.ToString()
        ));

        return response;
    }
}