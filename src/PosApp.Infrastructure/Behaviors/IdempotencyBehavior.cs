using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PosApp.Application.Abstractions.Idempotency;
using PosApp.Domain.Exceptions;
using PosApp.Infrastructure.Idempotency;

namespace PosApp.Infrastructure.Behaviors;

public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IIdempotencyContext idempotencyContext,
    IRequestManager requestManager,
    ILogger<IdempotencyBehavior<TRequest, TResponse>> logger,
    IOptions<JsonOptions> jsonOptions)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IIdempotentRequest<TResponse>
{
    private readonly JsonSerializerOptions _serializerOptions = jsonOptions.Value.SerializerOptions;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!idempotencyContext.IsEnabled || string.IsNullOrWhiteSpace(idempotencyContext.Key))
        {
            return await next();
        }

        var requestName = typeof(TRequest).Name;
        var key = idempotencyContext.Key!;
        var userId = idempotencyContext.UserId;
        var requestHash = ComputeHash(request);

        logger.LogDebug(
            "Handling idempotent request {RequestName} with key {IdempotencyKey} for user {UserId}",
            requestName,
            key,
            userId);

        var existing = await requestManager.FindAsync(requestName, key, userId, cancellationToken);
        if (existing is not null)
        {
            logger.LogDebug("Idempotency record found for {RequestName} with key {IdempotencyKey}", requestName, key);
            return HandleExistingResponse(existing, requestHash);
        }

        IdempotencyRecord pendingRecord;
        try
        {
            pendingRecord = await requestManager.CreatePendingAsync(requestName, key, requestHash, userId, cancellationToken);
            logger.LogDebug("Created pending idempotency record for {RequestName} with key {IdempotencyKey}", requestName, key);
        }
        catch (DbUpdateException ex)
        {
            logger.LogDebug(
                ex,
                "Failed to create idempotency record for {RequestName} with key {IdempotencyKey}, attempting to reuse existing record",
                requestName,
                key);

            existing = await requestManager.FindAsync(requestName, key, userId, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return HandleExistingResponse(existing, requestHash);
        }

        var response = await next();

        var responseContent = JsonSerializer.Serialize(response, _serializerOptions);
        await requestManager.MarkAsCompletedAsync(pendingRecord, responseContent, cancellationToken);
        logger.LogDebug(
            "Stored idempotent response for {RequestName} with key {IdempotencyKey}",
            requestName,
            key);

        return response;
    }

    private TResponse HandleExistingResponse(IdempotencyRecord record, string requestHash)
    {
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new DomainException("Idempotency key conflict: payload does not match previous request.", "Idempotency-Key");
        }

        if (record.ResponseContent is null)
        {
            throw new DomainException("Request with this idempotency key is currently being processed. Please retry later.", "Idempotency-Key");
        }

        var response = JsonSerializer.Deserialize<TResponse>(record.ResponseContent, _serializerOptions);
        if (response is null)
        {
            throw new DomainException("Failed to deserialize cached idempotent response.", "Idempotency-Key");
        }

        return response;
    }

    private string ComputeHash(TRequest request)
    {
        var payload = JsonSerializer.Serialize(request, _serializerOptions);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hashBytes = SHA256.HashData(payloadBytes);
        return Convert.ToHexString(hashBytes);
    }
}
