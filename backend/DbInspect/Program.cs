using Microsoft.EntityFrameworkCore;using OrgChat.Core.Entities;using OrgChat.Infrastructure.Data;var dbPath = "e:/OrgChat/backend/OrgChat.Server/OrgChat.db";
using var db = new AppDbContext(options);
Console.WriteLine("=== ROLES ===");
foreach (var r in await db.Roles.OrderBy(r => r.Id).ToListAsync())
