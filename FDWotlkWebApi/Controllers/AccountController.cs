using FDWotlkWebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FDWotlkWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountProvisioner _accountProvisioner;
        private readonly ILogger<AccountController> _logger;
        private readonly IMySqlService _mySqlService;

        public AccountController(IAccountProvisioner accountProvisioner, IMySqlService mySqlService, ILogger<AccountController> logger)
        {
            _accountProvisioner = accountProvisioner;
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

            var result = await _accountProvisioner.ProvisionAccountAsync(request.Username, request.Password, cancellationToken);

            if (result.Success)
            {
                // Call MySQL service to update account expansion
                await _mySqlService.UpdateAccountExpansionAsync(request.Username, 2, cancellationToken);
                return Ok(new { Message = "Account created successfully.", request.Username });
            }

            return StatusCode(500, new { Message = "Account creation failed.", Error = result.ErrorMessage });
        }

        [HttpGet("server-info")] // Endpoint: GET api/account/server-info
        public async Task<IActionResult> GetServerInfo()
        {
            try
            {
                var serverInfo = await _accountProvisioner.GetServerInfoAsync();
                return Ok(new { Message = "Server info retrieved successfully.", Data = serverInfo });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Failed to retrieve server info.", Error = ex.Message });
            }
        }
    }

    public class CreateAccountRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
