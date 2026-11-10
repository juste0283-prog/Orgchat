using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Departments;
using OrgChat.Core.Contracts.Services;
using OrgChat.Infrastructure.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace OrgChat.Tests;

/// <summary>
/// Tests d'intégration des ressources plateforme : organisations,
/// départements, RBAC et isolation stricte entre organisations.
/// </summary>
public class PlatformResourceIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly OrgChatTestFactory _factory;

    public PlatformResourceIntegrationTests(OrgChatTestFactory factory)
    {
        _factory = factory;
    }

    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..8];

    private HttpClient NewClient() => _factory.CreateClient();

    private static HttpClient WithToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<AuthResponse> RegisterAsync(string username)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Email = $"{username}@example.org",
            DisplayName = username,
            Password = "Password123!"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<AuthResponse> LoginAsync(string username)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = "Password123!"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task PromoteToSuperAdminAsync(int userId)
    {
        using var scope = _factory.Services.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<IUserRoleService>();
        var user = await roleService.GetUserWithRolesAsync(userId);
        Assert.NotNull(user);
        await roleService.SetOrganizationRoleAsync(user!, RoleKeys.SuperAdmin);
    }

    private async Task PromoteToRoleAsync(int userId, string roleKey)
    {
        using var scope = _factory.Services.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<IUserRoleService>();
        var user = await roleService.GetUserWithRolesAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");
        await roleService.SetOrganizationRoleAsync(user, roleKey);
    }

    /// <summary>
    /// Crée une seconde organisation (SuperAdmin) et un OrgAdmin dedans.
    /// Renvoie un jeton FRAIS (login après attribution du rôle).
    /// </summary>
    private async Task<(AuthResponse OrgAdmin, int OrganizationId)> CreateForeignOrganizationAsync()
    {
        var suffix = NewSuffix();
        var superAdminIdentity = await RegisterAsync($"rootsa{suffix}");
        await PromoteToSuperAdminAsync(superAdminIdentity.UserId);
        var superAdminAuth = await LoginAsync(superAdminIdentity.Username);

        using var superAdminClient = WithToken(NewClient(), superAdminAuth.Token);
        var createResponse = await superAdminClient.PostAsJsonAsync("/api/organizations", new
        {
            Name = $"Org B {suffix}",
        });
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())!;
        var organizationId = created.GetProperty("id").GetInt32();

        // L'OrgAdmin de l'organisation B doit y appartenir : transfert direct
        // en base (l'INVITATION fera ce travail plus tard), puis rôle OrgAdmin.
        var candidate = await RegisterAsync($"orgb{suffix}");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var candidateUser = await db.Users.FindAsync(candidate.UserId);
            Assert.NotNull(candidateUser);
            candidateUser!.OrganizationId = organizationId;
            await db.SaveChangesAsync();

            await PromoteToRoleWithOrganizationAsync(db, candidate.UserId, RoleKeys.OrgAdmin, organizationId);
        }

        return (await LoginAsync(candidate.Username), organizationId);
    }

    private static async Task PromoteToRoleWithOrganizationAsync(AppDbContext db, int userId, string roleKey, int organizationId)
    {
        // Retire les rôles précédents du même périmètre, applique le nouveau.
        var existing = await db.UserRoles.Where(ur => ur.UserId == userId && ur.OrganizationId == organizationId).ToListAsync();
        db.UserRoles.RemoveRange(existing);

        var roleId = await db.Roles.Where(r => r.Key == roleKey).Select(r => r.Id).FirstAsync();
        db.UserRoles.Add(new OrgChat.Core.Entities.UserRole
        {
            UserId = userId,
            RoleId = roleId,
            OrganizationId = organizationId,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SuperAdmin_CanCreateOrganization()
    {
        var suffix = NewSuffix();
        var identity = await RegisterAsync($"platadmin{suffix}");
        await PromoteToSuperAdminAsync(identity.UserId);
        var adminAuth = await LoginAsync(identity.Username);

        using var superAdminClient = WithToken(NewClient(), adminAuth.Token);

        var createResponse = await superAdminClient.PostAsJsonAsync("/api/organizations", new
        {
            Name = $"Org créée {suffix}",
            Email = $"org{suffix}@example.org",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = (await createResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())!;

        Assert.Equal($"Org créée {suffix}", created.GetProperty("name").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task CrossOrganization_Isolation_Department_Access_Is_Denied()
    {
        var (orgAdmin, foreignOrgId) = await CreateForeignOrganizationAsync();
        var suffix = NewSuffix();

        int foreignDepartmentId;
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var department = new OrgChat.Core.Entities.Department
            {
                OrganizationId = foreignOrgId,
                Name = $"Secrétariat {suffix}",
                IsActive = true,
            };
            db.Departments.Add(department);
            await db.SaveChangesAsync();
            foreignDepartmentId = department.Id;
        }

        // L'OrgAdmin de l'organisation B accède à son propre département...
        using var foreignClient = WithToken(NewClient(), orgAdmin.Token);
        var foreignRead = await foreignClient.GetAsync($"/api/departments/{foreignDepartmentId}");
        Assert.Equal(HttpStatusCode.OK, foreignRead.StatusCode);

        // ...mais un utilisateur de l'organisation 1 ne doit JAMAIS y accéder.
        var localIdentity = await RegisterAsync($"local{suffix}");
        await PromoteToRoleAsync(localIdentity.UserId, RoleKeys.OrgAdmin);
        var localAuth = await LoginAsync(localIdentity.Username);
        using var localClient = WithToken(NewClient(), localAuth.Token);

        var crossRead = await localClient.GetAsync($"/api/departments/{foreignDepartmentId}");
        Assert.Equal(HttpStatusCode.NotFound, crossRead.StatusCode);

        var crossStats = await localClient.GetAsync($"/api/departments/{foreignDepartmentId}/stats");
        Assert.Equal(HttpStatusCode.NotFound, crossStats.StatusCode);
    }

    [Fact]
    public async Task Department_Crud_Members_And_PrimaryAssignment()
    {
        var (alice, bob) = ("deptalice" + NewSuffix(), "deptbob" + NewSuffix());
        var aliceIdentity = await RegisterAsync(alice);
        var bobIdentity = await RegisterAsync(bob);
        await PromoteToRoleAsync(aliceIdentity.UserId, RoleKeys.OrgAdmin);
        var aliceAuth = await LoginAsync(alice);

        using var adminClient = WithToken(NewClient(), aliceAuth.Token);

        var createResponse = await adminClient.PostAsJsonAsync("/api/departments", new DepartmentCreateRequest
        {
            Name = "Informatique",
            Description = "Support et infrastructures",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var department = (await createResponse.Content.ReadFromJsonAsync<DepartmentDto>())!;

        Assert.Equal("Informatique", department.Name);
        Assert.True(department.IsActive);

        var updateResponse = await adminClient.PutAsJsonAsync($"/api/departments/{department.Id}",
            new DepartmentUpdateRequest { ManagerUserId = aliceIdentity.UserId });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<DepartmentDto>())!;
        Assert.Equal(aliceIdentity.UserId, updated.ManagerUserId);

        var addResponse = await adminClient.PostAsJsonAsync($"/api/departments/{department.Id}/members",
            new AddDepartmentMemberRequest { UserId = bobIdentity.UserId, IsPrimary = true });
        Assert.Equal(HttpStatusCode.NoContent, addResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bobUser = await db.Users.FindAsync(bobIdentity.UserId);
        Assert.Equal(department.Id, bobUser!.DepartmentId);

        var stats = (await adminClient.GetFromJsonAsync<DepartmentStatsDto>(
            $"/api/departments/{department.Id}/stats"))!;
        Assert.Equal(1, stats.MemberCount);
        Assert.Equal(1, stats.ActiveUsers);

        // Un membre non responsable ne peut pas gérer les affectations.
        using var bobClient = WithToken(NewClient(), bobIdentity.Token);
        var addAsBob = await bobClient.PostAsJsonAsync($"/api/departments/{department.Id}/members",
            new AddDepartmentMemberRequest { UserId = bobIdentity.UserId, IsPrimary = false });
        Assert.Equal(HttpStatusCode.Forbidden, addAsBob.StatusCode);

        var removeResponse = await adminClient.DeleteAsync($"/api/departments/{department.Id}/members/{bobIdentity.UserId}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);
        await db.Entry(bobUser!).ReloadAsync();
        Assert.Null(bobUser!.DepartmentId);
    }

    [Fact]
    public async Task Current_Organization_Is_The_Default_Org()
    {
        var suffix = NewSuffix();
        var identity = await RegisterAsync($"orgver{suffix}");
        using var client = WithToken(NewClient(), identity.Token);

        var read = await client.GetAsync("/api/organizations/current");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var org = (await read.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>())!;
        Assert.Equal(1, org.GetProperty("id").GetInt32());
        Assert.Equal("Organisation principale", org.GetProperty("name").GetString());
    }
}
