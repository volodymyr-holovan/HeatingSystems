using System.Reflection;
using Microsoft.Data.Sqlite;

namespace HeatingSystems.Data;

/// <summary>
/// Prepares the per-user working database.
/// The application ships a read-only catalogue (<c>heating_catalog.db</c>). On first start it is copied to the
/// user's data folder. When a newer catalogue is shipped, the user's projects and edited tariffs are migrated
/// into a fresh copy so that catalogue updates never destroy user data.
/// </summary>
public static class DatabaseInitializer
{
    public const string CatalogFileName = "heating_catalog.db";

    public static string DefaultUserDatabasePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeatingSystems", "heating.db");

    /// <summary>Ensures the user database exists and is up to date. Returns its path.</summary>
    public static string EnsureUserDatabase(string bundledCatalogPath, string userDatabasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(userDatabasePath)!);

        if (!File.Exists(bundledCatalogPath))
        {
            // Development fallback: an empty database with the schema only.
            CreateSchema(userDatabasePath);
            return userDatabasePath;
        }

        if (!File.Exists(userDatabasePath))
        {
            File.Copy(bundledCatalogPath, userDatabasePath);
            return userDatabasePath;
        }

        var bundledVersion = ReadMeta(bundledCatalogPath, "catalog_version");
        var userVersion = ReadMeta(userDatabasePath, "catalog_version");
        if (bundledVersion is not null && bundledVersion != userVersion)
            Migrate(bundledCatalogPath, userDatabasePath);

        return userDatabasePath;
    }

    public static void CreateSchema(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = ReadEmbeddedSchema();
        command.ExecuteNonQuery();
    }

    public static string ReadEmbeddedSchema()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HeatingSystems.Data.schema.sql")
                           ?? throw new InvalidOperationException("Embedded schema.sql not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string? ReadMeta(string path, string key)
    {
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM meta WHERE key = $key";
            command.Parameters.AddWithValue("$key", key);
            return command.ExecuteScalar() as string;
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    private static void Migrate(string bundledCatalogPath, string userDatabasePath)
    {
        var staging = userDatabasePath + ".new";
        File.Copy(bundledCatalogPath, staging, overwrite: true);

        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = staging, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            // ATTACH is not allowed inside a transaction.
            command.CommandText = "ATTACH DATABASE $old AS old";
            command.Parameters.AddWithValue("$old", userDatabasePath);
            command.ExecuteNonQuery();
            command.Parameters.Clear();

            using var tx = connection.BeginTransaction();
            command.Transaction = tx;

            if (TableExists(command, "old", "projects"))
            {
                command.CommandText = """
                    INSERT INTO projects (id, name, city, design_heat_load, annual_demand, payload, created_at, updated_at)
                    SELECT id, name, city, design_heat_load, annual_demand, payload, created_at, updated_at FROM old.projects
                    """;
                command.ExecuteNonQuery();
            }

            if (TableExists(command, "old", "settings"))
            {
                command.CommandText = "INSERT OR REPLACE INTO settings (key, value) SELECT key, value FROM old.settings";
                command.ExecuteNonQuery();
            }

            if (TableExists(command, "old", "energy_carriers"))
            {
                // Keep tariffs edited by the user (newer than the shipped ones).
                command.CommandText = """
                    UPDATE energy_carriers
                    SET price_per_unit = (SELECT o.price_per_unit FROM old.energy_carriers o WHERE o.code = energy_carriers.code),
                        co2_per_kwh    = (SELECT o.co2_per_kwh    FROM old.energy_carriers o WHERE o.code = energy_carriers.code),
                        updated_at     = (SELECT o.updated_at     FROM old.energy_carriers o WHERE o.code = energy_carriers.code)
                    WHERE EXISTS (SELECT 1 FROM old.energy_carriers o WHERE o.code = energy_carriers.code AND o.updated_at > energy_carriers.updated_at)
                    """;
                command.ExecuteNonQuery();
            }

            tx.Commit();
            command.Transaction = null;
            command.CommandText = "DETACH DATABASE old";
            command.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
        var backup = userDatabasePath + ".bak";
        File.Copy(userDatabasePath, backup, overwrite: true);
        File.Move(staging, userDatabasePath, overwrite: true);
    }

    private static bool TableExists(SqliteCommand command, string schema, string table)
    {
        command.CommandText = $"SELECT COUNT(*) FROM {schema}.sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$name", table);
        var exists = Convert.ToInt64(command.ExecuteScalar()) > 0;
        command.Parameters.Clear();
        return exists;
    }
}
