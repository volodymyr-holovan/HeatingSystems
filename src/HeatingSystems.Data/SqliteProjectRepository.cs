using System.Globalization;
using HeatingSystems.Core.Abstractions;

namespace HeatingSystems.Data;

/// <summary>Stores building projects (input data as JSON plus key results for the project list).</summary>
public sealed class SqliteProjectRepository(SqliteConnectionFactory factory) : IProjectRepository
{
    private static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public IReadOnlyList<ProjectSummary> ListProjects()
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, name, updated_at, city, design_heat_load, annual_demand FROM projects ORDER BY updated_at DESC";
        var list = new List<ProjectSummary>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var saved = DateTime.Parse(r.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            list.Add(new ProjectSummary(r.GetInt32(0), r.GetString(1), saved.ToLocalTime(), r.GetString(3), r.GetDouble(4), r.GetDouble(5)));
        }
        return list;
    }

    public int SaveProject(string name, string payloadJson, string city, double designHeatLoad, double annualDemand, int? id = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Project name is required.", nameof(name));
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        var now = Now();
        if (id is { } existing)
        {
            cmd.CommandText = """
                UPDATE projects SET name = $name, city = $city, design_heat_load = $load, annual_demand = $demand,
                                    payload = $payload, updated_at = $now
                WHERE id = $id
                """;
            cmd.Parameters.AddWithValue("$id", existing);
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO projects (name, city, design_heat_load, annual_demand, payload, created_at, updated_at)
                VALUES ($name, $city, $load, $demand, $payload, $now, $now);
                SELECT last_insert_rowid();
                """;
        }
        cmd.Parameters.AddWithValue("$name", name.Trim());
        cmd.Parameters.AddWithValue("$city", city);
        cmd.Parameters.AddWithValue("$load", designHeatLoad);
        cmd.Parameters.AddWithValue("$demand", annualDemand);
        cmd.Parameters.AddWithValue("$payload", payloadJson);
        cmd.Parameters.AddWithValue("$now", now);

        if (id is { } updated)
        {
            if (cmd.ExecuteNonQuery() == 0) throw new KeyNotFoundException($"Project {updated} not found.");
            return updated;
        }
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public string? LoadProjectPayload(int id)
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT payload FROM projects WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteScalar() as string;
    }

    public void DeleteProject(int id)
    {
        using var c = factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM projects WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
