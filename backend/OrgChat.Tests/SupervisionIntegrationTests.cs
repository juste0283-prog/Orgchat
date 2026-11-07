using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Auth;
using OrgChat.Core.Contracts.Departments;
using OrgChat.Core.Contracts.Services;
using OrgChat.Core.Contracts.Supervision;
using OrgChat.Core.Entities;
using OrgChat.Infrastructure.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace OrgChat.Tests;

/// <summary>
/// Tests d'intégration du module de supervision et du journal d'audit :
/// RBAC, métadonnées (jamais le contenu des messages), rapports et isolation
/// stricte entre organisations.
/// </summary>
public class SupervisionIntegrationTests : IClassFixture<OrgChatTestFactory>
{
    private readonly OrgChatTestFactory _factory;

    public SupervisionIntegrationTests(OrgChatTestFactory factory)
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

    private async Task<AuthResponse> LoginAsync(string username, string password = "Password123!")
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task PromoteAsync(int userId, string roleKey)
    {
        using var scope = _factory.Services.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<IUserRoleService>();
        var user = await roleService.GetUserWithRolesAsync(userId)
            ?? throw new InvalidOperationException("Utilisateur introuvable.");
        await roleService.SetOrganizationRoleAsync(user, roleKey);
    }

    private async Task<AuthResponse> CreateUserWithRoleAsync(string roleKey)
    {
        var username = $"sup{roleKey.ToLowerInvariant()}{NewSuffix()}";
        var identity = await RegisterAsync(username);
        await PromoteAsync(identity.UserId, roleKey);
        return await LoginAsync(username);
    }

    [Fact]
    public async Task Supervision_Is_Reserved_To_Supervision_Roles()
    {
        var supervisor = await CreateUserWithRoleAsync(RoleKeys.Supervisor);
        using var supervisorClient = WithToken(NewClient(), supervisor.Token);

        var summary = await supervisorClient.GetAsync("/api/supervision/summary");
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);

        var normalUsername = $"simple{NewSuffix()}";
        var normalIdentity = await RegisterAsync(normalUsername);
        using var normalClient = WithToken(NewClient(), normalIdentity.Token);

        Assert.Equal(HttpStatusCode.Forbidden, (await normalClient.GetAsync("/api/supervision/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await normalClient.GetAsync("/api/audit")).StatusCode);
    }

    [Fact]
    public async Task SupervisionUsers_Are_Isolated_Between_Organizations()
    {
        var orgAdmin = await CreateUserWithRoleAsync(RoleKeys.OrgAdmin);
        using var adminClient = WithToken(NewClient(), orgAdmin.Token);

        // Un utilisateur rattaché à une autre organisation ne doit jamais
        // apparaître dans la supervision de l'organisation courante.
        var suffix = NewSuffix();
        var foreignUsername = $"etranger{suffix}";
        var foreign = await RegisterAsync(foreignUsername);

        int foreignOrganizationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var organization = new Organization
            {
                Name = $"Organisation distante {suffix}",
                IsActive = true,
            };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync();
            foreignOrganizationId = organization.Id;

            var user = await db.Users.FindAsync(foreign.UserId);
            Assert.NotNull(user);
            user!.OrganizationId = foreignOrganizationId;
            await db.SaveChangesAsync();
        }

        var foreignRead = await adminClient.GetFromJsonAsync<PagedResultDto<SupervisionUserDto>>(
            $"/api/supervision/users?search={Uri.EscapeDataString(foreignUsername)}");
        Assert.NotNull(foreignRead);
        Assert.Equal(0, foreignRead!.TotalCount);

        var summary = await adminClient.GetFromJsonAsync<SupervisionSummaryDto>("/api/supervision/summary");
        Assert.NotNull(summary);
        Assert.True(summary!.TotalUsers >= 1);
        Assert.DoesNotContain(
            foreignUsername,
            string.Join(",", (await adminClient.GetFromJsonAsync<PagedResultDto<SupervisionUserDto>>(
                "/api/supervision/users?pageSize=100"))!.Items.Select(u => u.Username)));
    }

    [Fact]
    public async Task Audit_Logs_Are_Readable_And_Filterable()
    {
        var orgAdmin = await CreateUserWithRoleAsync(RoleKeys.OrgAdmin);
        using var adminClient = WithToken(NewClient(), orgAdmin.Token);

        var departmentName = $"Audit {NewSuffix()}";
        var create = await adminClient.PostAsJsonAsync("/api/departments", new DepartmentCreateRequest
        {
            Name = departmentName,
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var page = await adminClient.GetFromJsonAsync<PagedResultDto<AuditLogDto>>(
            "/api/audit?action=DepartmentCreated&pageSize=50");
        Assert.NotNull(page);
        Assert.Contains(page!.Items, log =>
            log.EntityType == "Department" &&
            log.Action == AuditActions.DepartmentCreated &&
            log.Result == "Success" &&
            log.ActorName.Length > 0);

        var facets = await adminClient.GetAsync("/api/audit/facets");
        Assert.Equal(HttpStatusCode.OK, facets.StatusCode);
    }

    [Fact]
    public async Task Health_Activity_Connections_And_Reports_Return_Metadata()
    {
        var supervisor = await CreateUserWithRoleAsync(RoleKeys.Supervisor);
        using var client = WithToken(NewClient(), supervisor.Token);

        var health = await client.GetFromJsonAsync<SupervisionHealthDto>("/api/supervision/health");
        Assert.NotNull(health);
        Assert.Equal("Healthy", health!.Api);
        Assert.Equal("Healthy", health.Database);
        Assert.Equal("Healthy", health.SignalR);
        Assert.False(string.IsNullOrWhiteSpace(health.Version));

        var activity = await client.GetFromJsonAsync<List<ActivityPointDto>>("/api/supervision/activity?days=7");
        Assert.NotNull(activity);
        Assert.Equal(7, activity!.Count);

        var departments = await client.GetFromJsonAsync<List<DepartmentSupervisionDto>>("/api/supervision/departments");
        Assert.NotNull(departments);

        var report = await client.GetFromJsonAsync<SupervisionReportDto>("/api/supervision/reports?period=weekly");
        Assert.NotNull(report);
        Assert.Equal("weekly", report!.Period);
        Assert.True(report.From < report.To);

        var connections = await client.GetAsync("/api/supervision/connections?hours=6");
        Assert.Equal(HttpStatusCode.OK, connections.StatusCode);
    }

    [Fact]
    public async Task Failed_Logins_Appear_In_Incidents()
    {
        var supervisor = await CreateUserWithRoleAsync(RoleKeys.Supervisor);
        var victim = await RegisterAsync($"incident{NewSuffix()}");

        using (var anonymous = NewClient())
        {
            var failed = await anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = victim.Username,
                Password = "MauvaisMotDePasse!",
            });
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        using var client = WithToken(NewClient(), supervisor.Token);
        var incidents = await client.GetFromJsonAsync<PagedResultDto<IncidentDto>>(
            "/api/supervision/incidents?days=7&pageSize=100");

        Assert.NotNull(incidents);
        Assert.Contains(incidents!.Items, incident =>
            incident.Action == AuditActions.LoginFailed &&
            incident.ActorUserId == victim.UserId);
    }
}
