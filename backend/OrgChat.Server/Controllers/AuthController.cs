using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Server.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthController(AppDbContext db, IPasswordHasher passwordHasher, ITokenService tokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Tous les champs sont requis." });
        }

        if (await _db.Users.AnyAsync(u => u.Username == request.Username))
            return BadRequest(new { error = "Ce nom d'utilisateur est déjà utilisé." });

        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { error = "Cet e-mail est déjà utilisé." });

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username.Trim() : request.DisplayName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user.Id, user.Username, user.Role.ToString());

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString()
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Unauthorized(new { error = "Identifiants invalides." });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null || !user.IsActive)
            return Unauthorized(new { error = "Identifiants invalides." });

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Identifiants invalides." });

        var token = _tokenService.GenerateToken(user.Id, user.Username, user.Role.ToString());

        return Ok(new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString()
        });
    }
}
