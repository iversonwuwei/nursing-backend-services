using Microsoft.Extensions.Configuration;

namespace NursingBackend.BuildingBlocks.Persistence;

public static class PostgresConnectionStrings
{
	public static string Resolve(IConfiguration configuration, string serviceConnectionName, string defaultDatabaseName)
	{
		var specificConnectionString = configuration.GetConnectionString(serviceConnectionName);
		var sharedConnectionString = configuration.GetConnectionString("Postgres");
		return Resolve(specificConnectionString, sharedConnectionString, defaultDatabaseName);
	}

	public static string Resolve(string? specificConnectionString, string? sharedConnectionString, string defaultDatabaseName)
	{
		if (!string.IsNullOrWhiteSpace(specificConnectionString))
		{
			return specificConnectionString;
		}

		if (!string.IsNullOrWhiteSpace(sharedConnectionString))
		{
			return WithDatabase(sharedConnectionString, defaultDatabaseName);
		}

		return Default(defaultDatabaseName);
	}

	public static string Default(string databaseName)
	{
		return $"Host=localhost;Port=5432;Database={databaseName};Username=nursing;Password=nursing";
	}

	public static string WithDatabase(string connectionString, string databaseName)
	{
		var segments = connectionString
			.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToList();

		var replaced = false;
		for (var index = 0; index < segments.Count; index++)
		{
			if (segments[index].StartsWith("Database=", StringComparison.OrdinalIgnoreCase)
				|| segments[index].StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
			{
				segments[index] = $"Database={databaseName}";
				replaced = true;
			}
		}

		if (!replaced)
		{
			segments.Add($"Database={databaseName}");
		}

		return string.Join(';', segments);
	}
}