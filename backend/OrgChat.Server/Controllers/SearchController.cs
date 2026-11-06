using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrgChat.Server.Services;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly MessageService _messageService;

    public SearchController(MessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpGet("messages")]
    public async Task<IActionResult> SearchMessages([FromQuery] string q, [FromQuery] int take = 50)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(Array.Empty<object>());

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = userIdClaim != null ? int.Parse(userIdClaim) : 0;
        var results = await _messageService.SearchAsync(userId, q, take);
        return Ok(results);
    }
}
