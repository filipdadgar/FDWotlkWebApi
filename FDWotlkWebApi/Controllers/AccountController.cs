using FDWotlkWebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FDWotlkWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly ISoapAccountProvisioner _soapAccountProvisioner;
        private readonly ILogger<AccountController> _logger;
        private readonly IMySqlService _mySqlService;

        // Maximum accounts allowed per IP within the window
        private const int MaxAccountsPerIp = 2;
        private static readonly TimeSpan AccountLimitWindow = TimeSpan.FromDays(1); // 24 hours

        public AccountController(ISoapAccountProvisioner soapAccountProvisioner, IMySqlService mySqlService, ILogger<AccountController> logger)
        {
            _soapAccountProvisioner = soapAccountProvisioner;
            _mySqlService = mySqlService;
            _logger = logger;
        }

        [HttpPost("create")] // Endpoint: POST api/account/create
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Username and password are required.");
            }

            // Determine client IP (prefer X-Forwarded-For if present)
            var clientIp = GetClientIp();
            _logger.LogInformation("Account create requested from IP {Ip} for username {Username}", clientIp, request.Username);

            try
            {
                // Enforce per-IP limit within the configured window
                var count = await _mySqlService.GetAccountCountByIpAsync(clientIp, AccountLimitWindow, cancellationToken);
                if (count >= MaxAccountsPerIp)
                {
                    _logger.LogWarning("IP {Ip} has already created {Count} accounts in the last {Window}. Rejecting.", clientIp, count, AccountLimitWindow);
                    return StatusCode(429, new { Message = "Account creation limit reached for your IP.", Limit = MaxAccountsPerIp, WindowHours = AccountLimitWindow.TotalHours });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check account creation count for IP {Ip}", clientIp);
                // Fail-open? we'll treat as a server error to be safe
                return StatusCode(500, new { Message = "Failed to validate account creation limit.", Error = ex.Message });
            }

            var result = await _soapAccountProvisioner.ProvisionAccountAsync(request.Username, request.Password, cancellationToken);

            if (result.Success)
            {
                try
                {
                    // Update account expansion
                    await _mySqlService.UpdateAccountExpansionAsync(request.Username, 2, cancellationToken);

                    // Update last_ip for the created account so subsequent counts include this creation
                    await _mySqlService.UpdateAccountLastIpAsync(request.Username, clientIp, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Account was created via SOAP but DB post-processing failed for user {Username}", request.Username);
                    // We don't want to roll back SOAP-created account here; inform the caller.
                    return StatusCode(500, new { Message = "Account created but DB post-processing failed.", Error = ex.Message });
                }

                return Ok(new { Message = "Account created successfully.", request.Username });
            }

            return StatusCode(500, new { Message = "Account creation failed.", Error = result.ErrorMessage });
        }

        [HttpGet("server-info")] // Endpoint: GET api/account/server-info
        public async Task<IActionResult> GetServerInfo()
        {
            try
            {
                var serverInfo = await _soapAccountProvisioner.GetServerInfoAsync();
                return Ok(new { Message = "Server info retrieved successfully.", Data = serverInfo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to retrieve server info.", Error = ex.Message });
            }
        }
        
        [HttpGet("players")] // Endpoint: GET api/account/players
        public async Task<IActionResult> GetPlayers(CancellationToken cancellationToken)
        {
            try
            {
                var players = await _mySqlService.GetPlayersAsync(cancellationToken);
                return Ok(players);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPlayers failed");
                return Problem(detail: "An error occurred while retrieving players.", statusCode: 500);
            }
        }

        private string GetClientIp()
        {
            // Prefer X-Forwarded-For (may contain comma-separated list)
            if (Request.Headers.TryGetValue("X-Forwarded-For", out var xff) && !string.IsNullOrWhiteSpace(xff))
            {
                var first = xff.ToString().Split(',').Select(s => s.Trim()).FirstOrDefault();
                if (!string.IsNullOrEmpty(first))
                    return first!;
            }

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            return string.IsNullOrEmpty(ip) ? "0.0.0.0" : ip!;
        }
    }

    public class CreateAccountRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
