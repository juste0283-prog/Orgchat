using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Server.Hubs;
using OrgChat.Server.Services;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/conversations/{conversationId}/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly MessageService _messageService;
    private readonly AttachmentService _attachmentService;
    private readonly IHubContext<ChatHub> _hub;

    public MessagesController(
        MessageService messageService,
        AttachmentService attachmentService,
        IHubContext<ChatHub> hub)
    {
        _messageService = messageService;
        _attachmentService = attachmentService;
        _hub = hub;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            await _hub.Clients.Group(conversationId.ToString()).SendAsync("ReceiveMessage", message);
            return Ok(message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("attachments")]
    [RequestSizeLimit(22 * 1024 * 1024)]
    public async Task<IActionResult> SendAttachment(
        int conversationId,
        [FromForm] IFormFile? file,
        [FromForm] string? content)
    {
        if (file is null)
            return BadRequest(new { error = "Aucun fichier n'a été fourni." });

        try
        {
            var message = await _attachmentService.SendAttachmentAsync(
                conversationId, GetCurrentUserId(), file, content);

            await _hub.Clients.Group(conversationId.ToString()).SendAsync("ReceiveMessage", message);
            return Ok(message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
