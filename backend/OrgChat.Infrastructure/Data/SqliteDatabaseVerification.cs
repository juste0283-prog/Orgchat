using System.Data;
using Microsoft.EntityFrameworkCore;

namespace OrgChat.Infrastructure.Data;

public sealed record SqliteDatabaseVerificationResult(
    string IntegrityCheck,
    int ForeignKeyViolationCount,
    IReadOnlyList<string> MissingRequiredIndexes,
    IReadOnlyList<string> DuplicateUsernames,
    IReadOnlyList<string> DuplicateEmails)
{
    public bool IsValid =>
        string.Equals(IntegrityCheck, "ok", StringComparison.OrdinalIgnoreCase)
        && ForeignKeyViolationCount == 0
        && MissingRequiredIndexes.Count == 0
        && DuplicateUsernames.Count == 0
        && DuplicateEmails.Count == 0;
}

public static class SqliteDatabaseVerification
{
    private static readonly string[] RequiredIndexes =
    [
        "IX_Users_Username",
        "IX_Users_Email",
        "IX_ConversationParticipants_ConversationId_UserId",
        "IX_Messages_ConversationId_CreatedAt"
    ];

    public static async Task<SqliteDatabaseVerificationResult> VerifyAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (!string.Equals(
                db.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("SQLite verification requires the SQLite EF Core provider.");
        }

        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await db.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            var integrityCheck = await GetIntegrityCheckAsync(connection, cancellationToken);
            var foreignKeyViolationCount = await GetForeignKeyViolationCountAsync(connection, cancellationToken);
            var missingIndexes = await GetMissingIndexesAsync(connection, cancellationToken);
            var duplicateUsernames = await db.Users
                .AsNoTracking()
                .GroupBy(user => user.Username)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToListAsync(cancellationToken);
            var duplicateEmails = await db.Users
                .AsNoTracking()
                .GroupBy(user => user.Email)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToListAsync(cancellationToken);

            return new SqliteDatabaseVerificationResult(
                integrityCheck,
                foreignKeyViolationCount,
                missingIndexes,
                duplicateUsernames,
                duplicateEmails);
        }
        finally
        {
            if (openedHere)
                await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<string> GetIntegrityCheckAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToString(result) ?? string.Empty;
    }

    private static async Task<int> GetForeignKeyViolationCountAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var count = 0;
        while (await reader.ReadAsync(cancellationToken))
            count++;

        return count;
    }

    private static async Task<IReadOnlyList<string>> GetMissingIndexesAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index';";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var existingIndexes = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
            existingIndexes.Add(reader.GetString(0));

        return RequiredIndexes
            .Where(index => !existingIndexes.Contains(index))
            .ToArray();
    }
}
