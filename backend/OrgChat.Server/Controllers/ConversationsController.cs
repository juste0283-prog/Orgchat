using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OrgChat.Core.Contracts.Conversations;
using OrgChat.Core.Contracts.Messages;
using OrgChat.Server.Hubs;
using OrgChat.Server.Services;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly ConversationService _conversationService;
    private readonly IHubContext<ChatHub> _hub;

    public ConversationsController(ConversationService conversationService, IHubContext<ChatHub> hub)
    {
        _conversationService = conversationService;
        _hub = hub;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    private async Task BroadcastConversationUpdatedAsync(int conversationId, IEnumerable<int> participantIds)
    {
        foreach (var participantId in participantIds.Distinct())
        {
            var conversation = await _conversationService.GetConversationByIdAsync(conversationId, participantId);
            if (conversation is not null)
            {
                await _hub.Clients.Group(ChatHub.UserGroup(participantId))
                    .SendAsync("ConversationUpdated", conversation);
            }
        }
    }

    private async Task RemoveConversationGroupAsync(int conversationId, IEnumerable<int> participantIds)
    {
        foreach (var participantId in participantIds.Distinct())
            await ChatHub.RemoveUserFromGroupAsync(_hub, participantId, conversationId);
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

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = GetCurrentUserId();
        var receipt = await _conversationService.MarkAsReadAsync(id, userId);
        if (receipt is null)
            return NotFound(new { error = "Conversation introuvable ou accès refusé." });

        await _hub.Clients.Group(id.ToString()).SendAsync("ConversationRead", receipt);
        return Ok(receipt);
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

        var participantIds = await _conversationService.GetParticipantUserIdsAsync(id);
        await BroadcastConversationUpdatedAsync(id, participantIds);

        return NoContent();
    }

    [HttpPost("{id}/leave")]
    public async Task<IActionResult> LeaveConversation(int id)
    {
        var userId = GetCurrentUserId();
        var participantIds = await _conversationService.GetParticipantUserIdsAsync(id);

        if (!participantIds.Contains(userId))
            return NotFound(new { error = "Conversation introuvable ou accès refusé." });

        try
        {
            var result = await _conversationService.LeaveConversationAsync(id, userId);
            var deleted = new ConversationDeletedDto
            {
                Id = id,
                DeletedAt = DateTime.UtcNow
            };

            if (result.Deleted)
            {
                foreach (var participantId in participantIds)
                {
                    await _hub.Clients.Group(ChatHub.UserGroup(participantId))
                        .SendAsync("ConversationDeleted", deleted);
                }
            }
            else
            {
                // Le partant retire immédiatement la conversation de sa liste.
                await _hub.Clients.Group(ChatHub.UserGroup(userId))
                    .SendAsync("ConversationDeleted", deleted);

                // Les autres participants voient la liste des membres se mettre à jour.
                await BroadcastConversationUpdatedAsync(id, participantIds.Where(p => p != userId));
            }

            // Dans tous les cas, la connexion SignalR du partant ne doit plus
            // recevoir les messages de la conversation.
            await RemoveConversationGroupAsync(id, participantIds);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteConversation(int id)
    {
        var userId = GetCurrentUserId();
        var participantIds = await _conversationService.GetParticipantUserIdsAsync(id);

        try
        {
            var success = await _conversationService.DeleteConversationAsync(id, userId);
            if (!success)
                return NotFound(new { error = "Conversation introuvable ou accès refusé." });

            var deleted = new ConversationDeletedDto
            {
                Id = id,
                DeletedAt = DateTime.UtcNow
            };

            foreach (var participantId in participantIds)
            {
                await _hub.Clients.Group(ChatHub.UserGroup(participantId))
                    .SendAsync("ConversationDeleted", deleted);
            }

            await RemoveConversationGroupAsync(id, participantIds);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    [HttpDelete("{id}/participants/{userId}")]
    public async Task<IActionResult> RemoveParticipant(int id, int userId)
    {
        var currentUserId = GetCurrentUserId();
        var participantIds = await _conversationService.GetParticipantUserIdsAsync(id);

        var success = await _conversationService.RemoveParticipantAsync(id, currentUserId, userId);
        if (!success)
            return BadRequest(new { error = "Impossible de supprimer ce participant." });

        // Le participant retiré ne doit plus recevoir les messages du groupe.
        await ChatHub.RemoveUserFromGroupAsync(_hub, userId, id);

        var removed = new ConversationDeletedDto
        {
            Id = id,
            DeletedAt = DateTime.UtcNow
        };

        await _hub.Clients.Group(ChatHub.UserGroup(userId))
            .SendAsync("ConversationDeleted", removed);

        await BroadcastConversationUpdatedAsync(id, participantIds.Where(p => p != userId));

        return NoContent();
    }
}
