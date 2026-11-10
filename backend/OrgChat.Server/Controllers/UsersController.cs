using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Contracts.Users;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private const int MinimumPasswordLength = 8;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ILogger<UsersController> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userIdClaim != null ? int.Parse(userIdClaim) : 0;
    }

    private bool IsCurrentUserAdmin()
    {
        return User.IsInRole(UserRole.Admin.ToString());
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetCurrentUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            IsActive = user.IsActive
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .OrderBy(u => u.Username)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Role = u.Role.ToString(),
                IsActive = u.IsActive
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Le nom d'utilisateur, l'e-mail et le mot de passe sont requis." });
        }

        if (request.Password.Length < MinimumPasswordLength)
            return BadRequest(new { error = $"Le mot de passe doit contenir au moins {MinimumPasswordLength} caractères." });

        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { error = "Rôle invalide." });

        var username = request.Username.Trim();
        var email = request.Email.Trim();

        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { error = "Ce nom d'utilisateur est déjà utilisé." });

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return BadRequest(new { error = "Cet e-mail est déjà utilisé." });

        var user = new User
        {
            Username = username,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? username : request.DisplayName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Audit admin : {AdminId} a créé le compte {UserId} ({Username}) avec le rôle {Role}.",
            GetCurrentUserId(), user.Id, user.Username, user.Role);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, MapUser(user));
    }

    [HttpPut("{id}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateUserRoleRequest request)
    {
        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { error = "Rôle invalide." });

        var currentUserId = GetCurrentUserId();
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (id == currentUserId && role != UserRole.Admin)
            return BadRequest(new { error = "Vous ne pouvez pas retirer votre propre rôle administrateur." });

        if (user.Role == UserRole.Admin && role != UserRole.Admin)
        {
            var adminCount = await _db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
            if (adminCount <= 1)
                return BadRequest(new { error = "Au moins un administrateur actif est requis." });
        }

        user.Role = role;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Audit admin : {AdminId} a changé le rôle de {UserId} en {Role}.",
            currentUserId, user.Id, user.Role);

        return Ok(MapUser(user));
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { error = "Le mot de passe actuel et le nouveau mot de passe sont requis." });
        }

        if (request.NewPassword.Length < MinimumPasswordLength)
            return BadRequest(new { error = $"Le nouveau mot de passe doit contenir au moins {MinimumPasswordLength} caractères." });

        var user = await _db.Users.FindAsync(GetCurrentUserId());
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { error = "Le mot de passe actuel est incorrect." });

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
            return BadRequest(new { error = "Le nouveau mot de passe doit être différent de l'ancien." });

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Audit : l'utilisateur {UserId} a changé son mot de passe.", user.Id);
        return NoContent();
    }

    [HttpPut("{id}/password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Length < MinimumPasswordLength)
        {
            return BadRequest(new { error = $"Le nouveau mot de passe doit contenir au moins {MinimumPasswordLength} caractères." });
        }

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Audit admin : {AdminId} a réinitialisé le mot de passe de {UserId}.",
            GetCurrentUserId(), user.Id);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (id == currentUserId)
            return BadRequest(new { error = "Vous ne pouvez pas supprimer votre propre compte." });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        // Suppression logique : les messages restent cohérents, l'identité est anonymisée.
        user.Username = $"deleted_{user.Id}";
        user.Email = $"deleted_{user.Id}@invalid.local";
        user.DisplayName = "Compte supprimé";
        user.PasswordHash = _passwordHasher.Hash(Guid.NewGuid().ToString("N"));
        user.Role = UserRole.User;
        user.IsActive = false;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Audit admin : {AdminId} a supprimé/anonymisé le compte {UserId}.",
            currentUserId, user.Id);

        return NoContent();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var currentUserId = GetCurrentUserId();
        var isAdmin = IsCurrentUserAdmin();

        if (currentUserId != id && !isAdmin)
            return Forbid();

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailExists = await _db.Users.AnyAsync(u => u.Email == request.Email.Trim() && u.Id != id);
            if (emailExists)
                return BadRequest(new { error = "Cet e-mail est déjà utilisé." });
            user.Email = request.Email.Trim();
        }

        await _db.SaveChangesAsync();

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            IsActive = user.IsActive
        });
    }

    [HttpPut("{id}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == id)
            return BadRequest(new { error = "Vous ne pouvez pas désactiver votre propre compte." });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        user.IsActive = false;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/reactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReactivateUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { error = "Utilisateur introuvable." });

        user.IsActive = true;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Audit admin : {AdminId} a réactivé le compte {UserId}.",
            GetCurrentUserId(), user.Id);

        return NoContent();
    }

    private static bool TryParseRole(string? value, out UserRole role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            role = UserRole.User;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out role) &&
               Enum.IsDefined(role);
    }

    private static UserDto MapUser(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role.ToString(),
        IsActive = user.IsActive
    };
}
