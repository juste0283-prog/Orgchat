using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OrgChat.Core.Contracts.Services;
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

    public string GenerateToken(int userId, string username, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };

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
