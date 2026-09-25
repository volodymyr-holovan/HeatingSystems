using Microsoft.Data.Sqlite;

namespace HeatingSystems.Data;

/// <summary>Creates connections to the application database.</summary>
public sealed class SqliteConnectionFactory(string databasePath)
{
    public string DatabasePath { get; } = databasePath;

    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        ForeignKeys = true,
        Cache = SqliteCacheMode.Shared,
    }.ToString();

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
