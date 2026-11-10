using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrgChat.Server.Services;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/attachments")]
[Authorize]
public class AttachmentsController : ControllerBase
{
    private readonly AttachmentService _attachmentService;

    public AttachmentsController(AttachmentService attachmentService)
    {
        _attachmentService = attachmentService;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var download = await _attachmentService.GetDownloadAsync(id, GetCurrentUserId());
        if (download is null)
            return NotFound(new { error = "Pièce jointe introuvable ou accès refusé." });

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(
            download.FullPath,
            download.Attachment.ContentType,
            download.Attachment.OriginalFileName,
            enableRangeProcessing: true);
    }
}
