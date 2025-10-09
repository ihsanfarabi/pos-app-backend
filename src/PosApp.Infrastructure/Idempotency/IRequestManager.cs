namespace PosApp.Infrastructure.Idempotency;

public interface IRequestManager
{
    Task<IdempotencyRecord?> FindAsync(string requestName, string key, Guid? userId, CancellationToken cancellationToken);

    Task<IdempotencyRecord> CreatePendingAsync(
        string requestName,
        string key,
        string requestHash,
        Guid? userId,
        CancellationToken cancellationToken);

    Task MarkAsCompletedAsync(IdempotencyRecord record, string responseContent, CancellationToken cancellationToken);
}
