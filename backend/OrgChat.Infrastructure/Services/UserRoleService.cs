using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;

namespace OrgChat.Infrastructure.Services;

/// <summary>
/// Gestion centralisée des rôles d'utilisateur (RBAC). Les rôles sont
/// attribués à l'échelle d'une organisation ; le rôle principal est le plus
/// privilégié de la liste. La vérification finale reste effectuée côté
/// serveur (attributs Authorize + règles métier).
/// </summary>
public class UserRoleService : IUserRoleService
{
    private readonly AppDbContext _db;

    public UserRoleService(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetUserWithRolesAsync(int userId) =>
        _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

    public string GetPrimaryRole(User user) =>
        RoleKeys.HighestRole(user.UserRoles.Select(ur => ur.Role?.Key ?? RoleKeys.User));

    public async Task SetOrganizationRoleAsync(User user, string roleKey)
    {
        foreach (var existing in user.UserRoles.Where(ur => ur.OrganizationId == null || ur.OrganizationId == user.OrganizationId))
            _db.UserRoles.Remove(existing);

        var roleId = await GetRoleIdAsync(roleKey);
        _db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = roleId,
            OrganizationId = user.OrganizationId,
        });

        await _db.SaveChangesAsync();
    }

    public async Task AssignDefaultRoleAsync(User user)
    {
        var role = await GetRoleEntityAsync(RoleKeys.User);
        _db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            Role = role,
            OrganizationId = user.OrganizationId,
        });
    }

    public async Task<int> GetRoleIdAsync(string roleKey)
    {
        var id = await _db.Roles
            .Where(r => r.Key == roleKey)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        if (id == 0)
            throw new InvalidOperationException($"Rôle inconnu : {roleKey}");

        return id;
    }

    private Task<Role> GetRoleEntityAsync(string roleKey) =>
        _db.Roles.FirstAsync(r => r.Key == roleKey);
}
