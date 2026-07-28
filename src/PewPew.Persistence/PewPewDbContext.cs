using Microsoft.EntityFrameworkCore;

namespace PewPew.Persistence;

public sealed class PewPewDbContext(DbContextOptions<PewPewDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Type).HasMaxLength(200).IsRequired();
            entity.Property(message => message.Payload).IsRequired();
            entity.Property(message => message.Version).IsConcurrencyToken();
        });
    }
}

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; init; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public long Version { get; private set; }
    public void MarkProcessed(DateTimeOffset now) { if (ProcessedAtUtc is not null) { throw new InvalidOperationException("Outbox message was already processed."); } ProcessedAtUtc = now; Version++; }
}
