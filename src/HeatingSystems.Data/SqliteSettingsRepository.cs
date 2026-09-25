using HeatingSystems.Core.Abstractions;

namespace HeatingSystems.Data;

/// <summary>Key/value user settings (e.g. UI language) stored in the user database.</summary>
public sealed class SqliteSettingsRepository(SqliteConnectionFactory factory) : ISettingsRepository
{
    public string? Get(string key)
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL); SELECT value FROM settings WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    public void Set(string key, string value)
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT (key) DO UPDATE SET value = excluded.value;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }
}
