namespace PosApp.Infrastructure.Idempotency;

public sealed class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }

    public required string RequestName { get; set; }

    public required string Key { get; set; }

    public required string RequestHash { get; set; }

    public string? ResponseContent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }
}
