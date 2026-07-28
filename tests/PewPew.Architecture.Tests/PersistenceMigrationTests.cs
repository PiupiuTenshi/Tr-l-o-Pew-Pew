using Microsoft.Data.Sqlite;
using Xunit;

namespace PewPew.Architecture.Tests;

public sealed class PersistenceMigrationTests
{
    [Fact]
    public void MigrationCreatesOutboxSchemaAndReplayIsIdempotent()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var sql = File.ReadAllText(Path.Combine(FindRoot(), "src", "PewPew.Persistence", "Migrations", "001_initial_outbox.sql"));
        using (var command = connection.CreateCommand()) { command.CommandText = sql; command.ExecuteNonQuery(); }
        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO OutboxMessages (Id, Type, Payload, OccurredAtUtc) VALUES ('1','test','{}','now');";
        insert.ExecuteNonQuery();
        using var process = connection.CreateCommand();
        process.CommandText = "UPDATE OutboxMessages SET ProcessedAtUtc='now', Version=Version+1 WHERE Id='1' AND ProcessedAtUtc IS NULL AND Version=0;";
        Assert.Equal(1, process.ExecuteNonQuery());
        Assert.Equal(0, process.ExecuteNonQuery());
    }
    private static string FindRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "PewPew.sln"))) { d = d.Parent; } return d!.FullName; }
}
