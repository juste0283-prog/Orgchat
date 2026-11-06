using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace OrgChat.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expirationMinutes;

    public TokenService(IConfiguration configuration)
    {
        var section = configuration.GetSection("JwtSettings");
        _key = section["Key"] ?? throw new InvalidOperationException("JwtSettings:Key est requis dans la configuration.");
        _issuer = section["Issuer"] ?? "OrgChat";
        _audience = section["Audience"] ?? "OrgChat";
        _expirationMinutes = int.TryParse(section["ExpirationMinutes"], out var minutes) ? minutes : 120;
    }

    public string GenerateToken(User user, string sessionTokenId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.Jti, sessionTokenId),
            new("org", user.OrganizationId.ToString()),
        };

        // Un claim rôle par rôle attribué ; [Authorize(Roles=...)] s'appuie dessus.
        foreach (var userRole in user.UserRoles)
        {
            if (userRole.Role != null)
                claims.Add(new Claim(ClaimTypes.Role, userRole.Role.Key));
        }

        if (user.UserRoles.Count == 0)
            claims.Add(new Claim(ClaimTypes.Role, RoleKeys.User));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
