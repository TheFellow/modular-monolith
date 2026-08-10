using Microsoft.Data.Sqlite;

namespace Mixology.Persistence;

public sealed record StoreSettings
{
    public StoreSettings(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Cache = SqliteCacheMode.Default,
        ForeignKeys = true,
        DefaultTimeout = 5,
        Pooling = true,
    }.ToString();
}
