using Microsoft.Data.Sqlite;

namespace OrgChat.Infrastructure.Data;

public static class SqliteDatabaseMaintenance
{
    private static readonly string[] RequiredTables =
    [
        "Users",
        "Conversations",
        "ConversationParticipants",
        "Messages",
        "__EFMigrationsHistory"
    ];

    public static void Backup(string databasePath, string backupPath)
    {
        var sourcePath = GetExistingPath(databasePath);
        var destinationPath = GetDifferentPath(sourcePath, backupPath, nameof(backupPath));

        using var source = CreateConnection(sourcePath, SqliteOpenMode.ReadOnly);
        source.Open();
        ValidateDatabase(source);

        CreateParentDirectory(destinationPath);
        using var destination = CreateConnection(destinationPath, SqliteOpenMode.ReadWriteCreate);
        destination.Open();
        source.BackupDatabase(destination);
        ValidateDatabase(destination);
    }

    public static void RestoreBackup(string backupPath, string databasePath)
    {
        var sourcePath = GetExistingPath(backupPath);
        var destinationPath = GetDifferentPath(sourcePath, databasePath, nameof(databasePath));

        using var source = CreateConnection(sourcePath, SqliteOpenMode.ReadOnly);
        source.Open();
        ValidateDatabase(source);

        CreateParentDirectory(destinationPath);
        using var destination = CreateConnection(destinationPath, SqliteOpenMode.ReadWriteCreate);
        destination.Open();
        source.BackupDatabase(destination);
        ValidateDatabase(destination);
    }

    private static SqliteConnection CreateConnection(string path, SqliteOpenMode mode)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            ForeignKeys = true
        };

        return new SqliteConnection(connectionString.ToString());
    }

    private static void ValidateDatabase(SqliteConnection connection)
    {
        using (var integrityCheck = connection.CreateCommand())
        {
            integrityCheck.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(integrityCheck.ExecuteScalar());
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"SQLite integrity check failed: {result}");
        }

        using (var foreignKeyCheck = connection.CreateCommand())
        {
            foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
            if (foreignKeyCheck.ExecuteScalar() is not null)
                throw new InvalidDataException("SQLite foreign key check failed.");
        }

        using var schemaCheck = connection.CreateCommand();
        schemaCheck.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name IN (
                'Users',
                'Conversations',
                'ConversationParticipants',
                'Messages',
                '__EFMigrationsHistory'
            );
            """;

        var tableCount = Convert.ToInt32(schemaCheck.ExecuteScalar());
        if (tableCount != RequiredTables.Length)
            throw new InvalidDataException("The SQLite file does not contain the OrgChat database schema.");
    }

    private static string GetExistingPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The SQLite database file was not found.", fullPath);

        return fullPath;
    }

    private static string GetDifferentPath(string sourcePath, string destinationPath, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath, parameterName);
        var fullPath = Path.GetFullPath(destinationPath);
        if (string.Equals(sourcePath, fullPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Source and destination paths must be different.", parameterName);

        return fullPath;
    }

    private static void CreateParentDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The database path must include a parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
    }
}
