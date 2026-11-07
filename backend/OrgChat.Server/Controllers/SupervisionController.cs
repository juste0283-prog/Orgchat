using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgChat.Core.Constants;
using OrgChat.Core.Contracts.Supervision;
using OrgChat.Core.Entities;
using OrgChat.Core.Enums;
using OrgChat.Infrastructure.Data;
using OrgChat.Server.Hubs;
using System.Diagnostics;
using System.Security.Claims;

namespace OrgChat.Server.Controllers;

/// <summary>
/// Module de supervision : statistiques, utilisateurs, départements, activité,
/// santé technique, connexions simultanées, incidents et rapports.
///
/// Périmètre : SuperAdmin (global), OrgAdmin et Superviseur (organisation
/// courante). Toutes les réponses sont des métadonnées ; aucun contenu de
/// conversation privée n'est lu ni exposé par ce contrôleur.
/// </summary>
[ApiController]
[Route("api/supervision")]
[Authorize(Roles = "SuperAdmin,OrgAdmin,Supervisor")]
public class SupervisionController : ControllerBase
{
    private readonly AppDbContext _db;

    public SupervisionController(AppDbContext db)
    {
        _db = db;
    }

    private int CurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : 0;
    }

    private bool IsGlobal => User.IsSuperAdmin();

    private int CurrentOrganizationId => User.GetUserOrganizationId();

    /// <summary>Utilisateurs visibles : tout pour le SuperAdmin, l'organisation
    /// courante pour les autres rôles (isolation stricte).</summary>
    private IQueryable<User> UsersQuery()
    {
        var query = _db.Users.AsNoTracking();
        return IsGlobal
            ? query
            : query.Where(u => u.OrganizationId == CurrentOrganizationId);
    }

    private IQueryable<Message> MessagesQuery()
    {
        var query = _db.Messages.AsNoTracking();
        return IsGlobal
            ? query
            : query.Where(m => m.Sender.OrganizationId == CurrentOrganizationId);
    }

    private IQueryable<Conversation> ConversationsQuery()
    {
        var query = _db.Conversations.AsNoTracking();
        return IsGlobal
            ? query
            : query.Where(c => c.Participants.Any(p => p.User.OrganizationId == CurrentOrganizationId));
    }

    private IQueryable<AuditLog> AuditQuery()
    {
        var query = _db.AuditLogs.AsNoTracking().Include(a => a.Actor).AsQueryable();
        if (IsGlobal)
            return query;

        var organizationId = CurrentOrganizationId;
        return query.Where(a => a.Actor != null && a.Actor.OrganizationId == organizationId);
    }

    private static string EffectiveStatus(User user, DateTime now)
    {
        if (!user.IsActive)
            return nameof(AccountStatus.Disabled);

        return user.Status switch
        {
            AccountStatus.Suspended when user.SuspendedUntil == null || user.SuspendedUntil > now
                => nameof(AccountStatus.Suspended),
            AccountStatus.Disabled => nameof(AccountStatus.Disabled),
            _ => nameof(AccountStatus.Active),
        };
    }

    private static string PrimaryRole(User user) =>
        RoleKeys.HighestRole(user.UserRoles.Select(ur => ur.Role?.Key ?? RoleKeys.User));

    private static SupervisionUserDto MapUser(User user, DateTime now, ISet<int> online)
    {
        var role = PrimaryRole(user);
        return new SupervisionUserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            JobTitle = user.JobTitle,
            Role = role,
            DepartmentName = user.Department?.Name,
            Status = EffectiveStatus(user, now),
            IsActive = user.IsActive && user.Status != AccountStatus.Disabled,
            IsOnline = online.Contains(user.Id),
            LastLoginAt = user.LastLoginAt,
            SuspendedUntil = user.SuspendedUntil,
            CreatedAt = user.CreatedAt,
        };
    }

    // ------------------------------------------------------------ Vue d'ensemble

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var now = DateTime.UtcNow;
        var users = UsersQuery();
        var userIds = await users.Select(u => u.Id).ToListAsync();
        var online = ChatHub.OnlineUserIds().ToHashSet();

        var totalUsers = userIds.Count;
        var activeUsers = await users.CountAsync(u =>
            u.IsActive && u.Status != AccountStatus.Disabled &&
            (u.Status != AccountStatus.Suspended || (u.SuspendedUntil != null && u.SuspendedUntil <= now)));
        var suspendedUsers = await users.CountAsync(u =>
            u.IsActive && u.Status == AccountStatus.Suspended &&
            (u.SuspendedUntil == null || u.SuspendedUntil > now));
        var disabledUsers = await users.CountAsync(u =>
            !u.IsActive || u.Status == AccountStatus.Disabled);

        var global = IsGlobal;
        var organizationId = CurrentOrganizationId;
        var departments = await _db.Departments.AsNoTracking()
            .CountAsync(d => global || d.OrganizationId == organizationId);

        var conversations = await ConversationsQuery().CountAsync();

        var since24 = now.AddHours(-24);
        var messages24 = await MessagesQuery()
            .CountAsync(m => !m.IsDeleted && m.CreatedAt >= since24);

        var since7 = now.AddDays(-7);
        var newUsers7 = await users.CountAsync(u => u.CreatedAt >= since7);

        var activeSessions = await _db.Sessions.AsNoTracking()
            .CountAsync(s => s.RevokedAt == null && s.ExpiresAt > now && userIds.Contains(s.UserId));

        var scopedOnline = userIds.Where(online.Contains).ToList();

        return Ok(new SupervisionSummaryDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            SuspendedUsers = suspendedUsers,
            DisabledUsers = disabledUsers,
            OnlineUsers = scopedOnline.Count,
            Departments = departments,
            Conversations = conversations,
            MessagesLast24Hours = messages24,
            NewUsersLast7Days = newUsers7,
            ActiveSessions = activeSessions,
            ConcurrentConnections = ChatHub.ConcurrentConnectionCountForUsers(scopedOnline),
            GeneratedAt = now,
        });
    }

    // ---------------------------------------------------------------- Utilisateurs

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? role = null,
        [FromQuery] int? departmentId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var now = DateTime.UtcNow;
        var query = UsersQuery()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Include(u => u.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.DisplayName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(role) && RoleKeys.IsValid(role))
            query = query.Where(u => u.UserRoles.Any(ur => ur.Role.Key == role));

        if (departmentId is > 0)
            query = query.Where(u => u.DepartmentId == departmentId);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AccountStatus>(status, ignoreCase: true, out var parsed))
        {
            query = parsed switch
            {
                AccountStatus.Active => query.Where(u =>
                    u.IsActive && u.Status != AccountStatus.Disabled &&
                    (u.Status != AccountStatus.Suspended || (u.SuspendedUntil != null && u.SuspendedUntil <= now))),
                AccountStatus.Suspended => query.Where(u =>
                    u.IsActive && u.Status == AccountStatus.Suspended &&
                    (u.SuspendedUntil == null || u.SuspendedUntil > now)),
                _ => query.Where(u => !u.IsActive || u.Status == AccountStatus.Disabled),
            };
        }

        var total = await query.CountAsync();
        var size = Math.Clamp(pageSize, 1, 100);
        var currentPage = Math.Max(1, page);

        var users = await query
            .OrderBy(u => u.DisplayName)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync();

        var online = ChatHub.OnlineUserIds().ToHashSet();

        return Ok(new PagedResultDto<SupervisionUserDto>
        {
            Items = users.Select(u => MapUser(u, now, online)).ToList(),
            TotalCount = total,
            Page = currentPage,
            PageSize = size,
        });
    }

    // ---------------------------------------------------------------- Départements

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var since = DateTime.UtcNow.AddDays(-7);
        return Ok(await BuildDepartmentSupervisionAsync(since));
    }

    private async Task<List<DepartmentSupervisionDto>> BuildDepartmentSupervisionAsync(DateTime since)
    {
        var global = IsGlobal;
        var organizationId = CurrentOrganizationId;
        var departments = await _db.Departments.AsNoTracking()
            .Include(d => d.Manager)
            .Where(d => global || d.OrganizationId == organizationId)
            .OrderBy(d => d.Name)
            .ToListAsync();

        if (departments.Count == 0)
            return new List<DepartmentSupervisionDto>();

        var departmentIds = departments.Select(d => d.Id).ToList();
        var assignments = await _db.DepartmentMembers.AsNoTracking()
            .Where(m => departmentIds.Contains(m.DepartmentId))
            .Select(m => new { m.DepartmentId, m.UserId })
            .ToListAsync();

        var users = await UsersQuery()
            .Select(u => new { u.Id, u.DepartmentId, u.IsActive, u.Status })
            .ToListAsync();

        var membersByDepartment = departments.ToDictionary(
            d => d.Id,
            _ => new HashSet<int>());

        foreach (var assignment in assignments)
        {
            if (membersByDepartment.TryGetValue(assignment.DepartmentId, out var members))
                members.Add(assignment.UserId);
        }

        foreach (var user in users)
        {
            if (user.DepartmentId is int departmentId &&
                membersByDepartment.TryGetValue(departmentId, out var members))
            {
                members.Add(user.Id);
            }
        }

        var allMemberIds = membersByDepartment.Values
            .SelectMany(ids => ids)
            .Distinct()
            .ToList();

        var messages = allMemberIds.Count == 0
            ? new List<int>()
            : await _db.Messages.AsNoTracking()
                .Where(m => !m.IsDeleted && m.CreatedAt >= since && allMemberIds.Contains(m.SenderId))
                .Select(m => m.SenderId)
                .ToListAsync();

        var messagesBySender = messages
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());

        var userById = users.ToDictionary(u => u.Id);
        var online = ChatHub.OnlineUserIds().ToHashSet();

        return departments.Select(department =>
        {
            var members = membersByDepartment[department.Id];
            var active = members.Count(id =>
                userById.TryGetValue(id, out var user) &&
                user.IsActive && user.Status != AccountStatus.Disabled);

            var messageCount = members.Sum(id =>
                messagesBySender.TryGetValue(id, out var count) ? count : 0);

            return new DepartmentSupervisionDto
            {
                DepartmentId = department.Id,
                Name = department.Name,
                IsActive = department.IsActive,
                ManagerName = department.Manager?.DisplayName,
                MemberCount = members.Count,
                ActiveUsers = active,
                OnlineUsers = members.Count(online.Contains),
                MessagesLast7Days = messageCount,
            };
        }).ToList();
    }

    // ------------------------------------------------------------------- Activité

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 90);
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var userIds = await UsersQuery().Select(u => u.Id).ToListAsync();

        if (userIds.Count == 0)
            return Ok(BuildEmptyActivity(days));

        var activities = await _db.UserActivities.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.CreatedAt >= from)
            .Select(a => new { a.UserId, a.Type, a.CreatedAt })
            .ToListAsync();

        var newUsers = await UsersQuery()
            .Where(u => u.CreatedAt >= from)
            .Select(u => new { u.Id, u.CreatedAt })
            .ToListAsync();

        var messages = await _db.Messages.AsNoTracking()
            .Where(m => !m.IsDeleted && m.CreatedAt >= from && userIds.Contains(m.SenderId))
            .Select(m => new { m.SenderId, m.CreatedAt })
            .ToListAsync();

        var points = new List<ActivityPointDto>(days);
        for (var i = 0; i < days; i++)
        {
            var day = from.AddDays(i);
            var next = day.AddDays(1);

            var dayActivities = activities
                .Where(a => a.CreatedAt >= day && a.CreatedAt < next)
                .ToList();
            var dayMessages = messages
                .Where(m => m.CreatedAt >= day && m.CreatedAt < next)
                .ToList();

            points.Add(new ActivityPointDto
            {
                Date = day,
                Logins = dayActivities.Count(a => a.Type == ActivityTypes.Login),
                FailedLogins = dayActivities.Count(a => a.Type == ActivityTypes.LoginFailed),
                NewUsers = newUsers.Count(u => u.CreatedAt >= day && u.CreatedAt < next),
                Messages = dayMessages.Count,
                ActiveUsers = dayActivities.Select(a => a.UserId)
                    .Concat(dayMessages.Select(m => m.SenderId))
                    .Distinct()
                    .Count(),
            });
        }

        return Ok(points);
    }

    private static List<ActivityPointDto> BuildEmptyActivity(int days) =>
        Enumerable.Range(0, days)
            .Select(i => new ActivityPointDto { Date = DateTime.UtcNow.Date.AddDays(-(days - 1) + i) })
            .ToList();

    // -------------------------------------------------------------------- Santé

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var now = DateTime.UtcNow;
        var userIds = await UsersQuery().Select(u => u.Id).ToListAsync();
        var onlineIds = ChatHub.OnlineUserIds().Where(userIds.Contains).ToList();

        var databaseHealthy = false;
        try
        {
            databaseHealthy = await _db.Database.CanConnectAsync();
        }
        catch
        {
            databaseHealthy = false;
        }

        var activeSessions = await _db.Sessions.AsNoTracking()
            .CountAsync(s => s.RevokedAt == null && s.ExpiresAt > now && userIds.Contains(s.UserId));

        var uptime = (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
        var version = typeof(SupervisionController).Assembly.GetName().Version?.ToString() ?? "1.0";

        return Ok(new SupervisionHealthDto
        {
            Status = databaseHealthy ? "Healthy" : "Degraded",
            Api = "Healthy",
            Database = databaseHealthy ? "Healthy" : "Unavailable",
            SignalR = "Healthy",
            DatabaseProvider = _db.Database.ProviderName ?? "unknown",
            UptimeSeconds = Math.Max(0, uptime),
            ServerTimeUtc = now,
            Version = version,
            ConcurrentConnections = ChatHub.ConcurrentConnectionCountForUsers(onlineIds),
            OnlineUsers = onlineIds.Count,
            ActiveSessions = activeSessions,
        });
    }

    // -------------------------------------------------------------- Connexions

    [HttpGet("connections")]
    public async Task<IActionResult> GetConnections([FromQuery] int hours = 24)
    {
        hours = Math.Clamp(hours, 1, 168);
        var now = DateTime.UtcNow;
        var rangeEnd = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
        var from = rangeEnd.AddHours(-hours);

        var userIds = await UsersQuery().Select(u => u.Id).ToListAsync();
        var sessions = await _db.Sessions.AsNoTracking()
            .Where(s => s.CreatedAt >= from && userIds.Contains(s.UserId))
            .Select(s => new { s.CreatedAt, s.UserId })
            .ToListAsync();

        var points = new List<ConnectionPointDto>(hours);
        for (var i = 0; i < hours; i++)
        {
            var start = from.AddHours(i);
            var end = start.AddHours(1);
            var bucket = sessions
                .Where(s => s.CreatedAt >= start && s.CreatedAt < end)
                .ToList();

            points.Add(new ConnectionPointDto
            {
                PeriodStart = start,
                Sessions = bucket.Count,
                UniqueUsers = bucket.Select(s => s.UserId).Distinct().Count(),
            });
        }

        var online = ChatHub.OnlineUserIds();
        var scopedOnline = online.Where(userIds.Contains).ToList();

        return Ok(new
        {
            currentConcurrentConnections = ChatHub.ConcurrentConnectionCountForUsers(scopedOnline),
            currentOnlineUsers = scopedOnline.Count,
            points,
        });
    }

    // ------------------------------------------------------------------ Incidents

    [HttpGet("incidents")]
    public async Task<IActionResult> GetIncidents(
        [FromQuery] int days = 7,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        days = Math.Clamp(days, 1, 90);
        var from = DateTime.UtcNow.AddDays(-days);

        var query = AuditQuery()
            .Where(a => a.CreatedAt >= from &&
                (a.Result != "Success" ||
                 a.Action == AuditActions.LoginFailed ||
                 a.Action == AuditActions.AccessDenied));

        var total = await query.CountAsync();
        var size = Math.Clamp(pageSize, 1, 100);
        var currentPage = Math.Max(1, page);

        var incidents = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync();

        return Ok(new PagedResultDto<IncidentDto>
        {
            Items = incidents.Select(MapIncident).ToList(),
            TotalCount = total,
            Page = currentPage,
            PageSize = size,
        });
    }

    private static IncidentDto MapIncident(AuditLog log) => new()
    {
        Id = log.Id,
        CreatedAt = log.CreatedAt,
        ActorUserId = log.ActorUserId,
        ActorName = log.Actor?.DisplayName ?? "Système",
        Action = log.Action,
        Result = log.Result,
        EntityType = log.EntityType,
        EntityId = log.EntityId,
        Details = log.Details,
        IpAddress = log.IpAddress,
    };

    // ------------------------------------------------------------------ Rapports

    [HttpGet("reports")]
    public async Task<IActionResult> GetReport([FromQuery] string period = "daily")
    {
        var now = DateTime.UtcNow;
        var normalized = (period ?? "daily").Trim().ToLowerInvariant();
        var from = normalized switch
        {
            "monthly" => now.AddDays(-30),
            "weekly" => now.AddDays(-7),
            _ => now.AddDays(-1),
        };

        var userIds = await UsersQuery().Select(u => u.Id).ToListAsync();

        var newUsers = await UsersQuery().CountAsync(u => u.CreatedAt >= from);
        var suspended = await UsersQuery().CountAsync(u =>
            u.IsActive && u.Status == AccountStatus.Suspended &&
            (u.SuspendedUntil == null || u.SuspendedUntil > now));

        var activities = new List<(int UserId, string Type, DateTime CreatedAt)>();
        if (userIds.Count > 0)
        {
            var rawActivities = await _db.UserActivities.AsNoTracking()
                .Where(a => userIds.Contains(a.UserId) && a.CreatedAt >= from)
                .Select(a => new { a.UserId, a.Type, a.CreatedAt })
                .ToListAsync();

            activities.AddRange(rawActivities.Select(a => (a.UserId, a.Type, a.CreatedAt)));
        }

        var messages = await _db.Messages.AsNoTracking()
            .Where(m => !m.IsDeleted && m.CreatedAt >= from && userIds.Contains(m.SenderId))
            .Select(m => new { m.SenderId, m.CreatedAt })
            .ToListAsync();

        var conversationsCreated = await ConversationsQuery()
            .CountAsync(c => c.CreatedAt >= from);

        var incidents = await AuditQuery()
            .CountAsync(a => a.CreatedAt >= from &&
                (a.Result != "Success" ||
                 a.Action == AuditActions.LoginFailed ||
                 a.Action == AuditActions.AccessDenied));

        var activityCounts = new Dictionary<int, int>();
        foreach (var activity in activities)
            activityCounts[activity.UserId] = activityCounts.GetValueOrDefault(activity.UserId) + 1;
        foreach (var message in messages)
            activityCounts[message.SenderId] = activityCounts.GetValueOrDefault(message.SenderId) + 1;

        var activeUsers = activityCounts.Count;

        var topIds = activityCounts
            .OrderByDescending(pair => pair.Value)
            .Take(5)
            .Select(pair => pair.Key)
            .ToList();

        var topUsers = topIds.Count == 0
            ? new List<TopActiveUserDto>()
            : await UsersQuery()
                .Where(u => topIds.Contains(u.Id))
                .Select(u => new TopActiveUserDto
                {
                    UserId = u.Id,
                    DisplayName = u.DisplayName,
                    Username = u.Username,
                    ActivityCount = 0,
                })
                .ToListAsync();

        foreach (var user in topUsers)
            user.ActivityCount = activityCounts.GetValueOrDefault(user.UserId);
        topUsers = topUsers.OrderByDescending(u => u.ActivityCount).ToList();

        var departments = await BuildDepartmentSupervisionAsync(from);

        return Ok(new SupervisionReportDto
        {
            Period = normalized is "weekly" or "monthly" ? normalized : "daily",
            From = from,
            To = now,
            NewUsers = newUsers,
            ActiveUsers = activeUsers,
            Logins = activities.Count(a => a.Type == ActivityTypes.Login),
            Messages = messages.Count,
            ConversationsCreated = conversationsCreated,
            SuspendedAccounts = suspended,
            Incidents = incidents,
            TopUsers = topUsers,
            Departments = departments,
        });
    }
}
