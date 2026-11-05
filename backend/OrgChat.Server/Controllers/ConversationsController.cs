using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Server.Services;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly ConversationService _conversationService;

    public ConversationsController(ConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        var userId = GetCurrentUserId();
        var conversations = await _conversationService.GetConversationsForUserAsync(userId);
        return Ok(conversations);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetConversation(int id)
    {
        var userId = GetCurrentUserId();
        var conversation = await _conversationService.GetConversationByIdAsync(id, userId);
        if (conversation == null)
            return NotFound(new { error = "Conversation introuvable ou accès refusé." });

        return Ok(conversation);
    }

    [HttpPost]
    public async Task<IActionResult> CreateConversation([FromBody] ConversationCreateRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        try
        {
            var conversation = await _conversationService.CreateConversationAsync(request, userId);
            return CreatedAtAction(nameof(GetConversation), new { id = conversation.Id }, conversation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/participants")]
    public async Task<IActionResult> AddParticipant(int id, [FromBody] AddParticipantRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        var success = await _conversationService.AddParticipantAsync(id, userId, request);
        if (!success)
            return BadRequest(new { error = "Impossible d'ajouter ce participant." });

        return NoContent();
    }

    [HttpDelete("{id}/participants/{userId}")]
    public async Task<IActionResult> RemoveParticipant(int id, int userId)
    {
        var currentUserId = GetCurrentUserId();

        var success = await _conversationService.RemoveParticipantAsync(id, currentUserId, userId);
        if (!success)
            return BadRequest(new { error = "Impossible de supprimer ce participant." });

        return NoContent();
    }
}
