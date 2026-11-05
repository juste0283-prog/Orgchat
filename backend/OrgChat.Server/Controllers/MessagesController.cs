using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Server.Services;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/conversations/{conversationId}/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly MessageService _messageService;

    public MessagesController(MessageService messageService)
    {
        _messageService = messageService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    [HttpGet]
    public async Task<IActionResult> GetMessages(int conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var userId = GetCurrentUserId();
        var result = await _messageService.GetMessagesAsync(conversationId, userId, page, pageSize);

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(int conversationId, [FromBody] MessageCreateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var senderId = GetCurrentUserId();

        try
        {
            var message = await _messageService.SendMessageAsync(conversationId, senderId, request);
            return Ok(message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
