using FDWotlkWebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FDWotlkWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountProvisioner _accountProvisioner;

        public AccountController(IAccountProvisioner accountProvisioner)
        {
            _accountProvisioner = accountProvisioner;
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
                return Ok(new { Message = "Account created successfully.", request.Username });
            }

            return StatusCode(500, new { Message = "Account creation failed.", Error = result.ErrorMessage });
        }
    }

    public class CreateAccountRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
