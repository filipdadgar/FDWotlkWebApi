namespace FDWotlkWebApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using FDWotlkWebApi.Models;
using FDWotlkWebApi.Services;

[ApiController]
[Route("api/wotlk")]
public class WotlkDbController : ControllerBase
{
    private readonly IMySqlService _mySqlService;
    private readonly ILogger<WotlkDbController> _logger;

    public WotlkDbController(IMySqlService mySqlService, ILogger<WotlkDbController> logger)
    {
        _mySqlService = mySqlService;
        _logger = logger;
    }

    // GET api/wotlk/players
    [HttpGet("players")]
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
    
    // Create a player
}