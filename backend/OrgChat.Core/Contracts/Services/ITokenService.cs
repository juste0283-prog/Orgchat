using OrgChat.Core.Entities;

namespace OrgChat.Core.Contracts.Services;

public interface ITokenService
{
    /// <summary>
    /// Génère un jeton JWT signé avec un identifiant de session (claim jti),
    /// le rôle principal et l'appartenance à une organisation.
    /// </summary>
    string GenerateToken(User user, string sessionTokenId);
}
