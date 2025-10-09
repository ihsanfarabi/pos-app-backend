using Microsoft.EntityFrameworkCore;
using PosApp.Infrastructure.Persistence;

namespace PosApp.Infrastructure.Idempotency;

public sealed class RequestManager(AppDbContext dbContext) : IRequestManager
{
    private readonly AppDbContext _dbContext = dbContext;

    public Task<IdempotencyRecord?> FindAsync(string requestName, string key, Guid? userId, CancellationToken cancellationToken)
    {
        return _dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(
                record => record.RequestName == requestName
                       && record.Key == key
                       && record.UserId == userId,
                cancellationToken);
    }

    public async Task<IdempotencyRecord> CreatePendingAsync(
        string requestName,
        string key,
        string requestHash,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var record = new IdempotencyRecord
        {
            RequestName = requestName,
            Key = key,
            RequestHash = requestHash,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.IdempotencyRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return record;
    }

    public async Task MarkAsCompletedAsync(IdempotencyRecord record, string responseContent, CancellationToken cancellationToken)
    {
        record.ResponseContent = responseContent;
        record.CompletedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
