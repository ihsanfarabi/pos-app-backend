using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosApp.Infrastructure.Persistence;

namespace PosApp.Infrastructure.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse>(
    AppDbContext dbContext,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Skip transactions for queries by convention
        if (requestName.EndsWith("Query", StringComparison.Ordinal))
        {
            return await next();
        }

        // If there is an active transaction, continue without starting a new one
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await next();
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        TResponse? response = default;

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                logger.LogInformation("Begin transaction {TransactionId} for {RequestName}", transaction.TransactionId, requestName);

                response = await next();

                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("Commit transaction {TransactionId} for {RequestName}", transaction.TransactionId, requestName);
            }
            catch
            {
                // Rollback is implicit on dispose if not committed
                throw;
            }
        });

        return response!;
    }
}


