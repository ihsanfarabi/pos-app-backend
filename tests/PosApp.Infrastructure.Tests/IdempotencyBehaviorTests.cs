namespace PosApp.Infrastructure.Tests;

public sealed class IdempotencyBehaviorTests
{
    [Fact]
    public async Task Handle_WhenRecordExists_ReturnsCachedResponse()
    {
        var jsonOptions = new JsonOptions();
        var options = Microsoft.Extensions.Options.Options.Create(jsonOptions);

        var key = "test-key";
        var userId = Guid.NewGuid();

        var context = Substitute.For<IIdempotencyContext>();
        context.IsEnabled.Returns(true);
        context.Key.Returns(key);
        context.UserId.Returns(userId);

        var request = new TestRequest("value", 42);
        var expectedResponse = new TestResponse(Guid.NewGuid(), "ok");
        var requestHash = ComputeHash(request, jsonOptions.SerializerOptions);

        var record = new IdempotencyRecord
        {
            RequestName = nameof(TestRequest),
            Key = key,
            UserId = userId,
            RequestHash = requestHash,
            ResponseContent = JsonSerializer.Serialize(expectedResponse, jsonOptions.SerializerOptions),
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        var requestManager = Substitute.For<IRequestManager>();
        requestManager
            .FindAsync(nameof(TestRequest), key, userId, Arg.Any<CancellationToken>())
            .Returns(record);

        var logger = NullLogger<IdempotencyBehavior<TestRequest, TestResponse>>.Instance;
        var behavior = new IdempotencyBehavior<TestRequest, TestResponse>(context, requestManager, logger, options);

        var nextCalled = false;
        RequestHandlerDelegate<TestResponse> next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new TestResponse(Guid.NewGuid(), "unexpected"));
        };

        var result = await behavior.Handle(request, next, CancellationToken.None);

        Assert.False(nextCalled);
        Assert.Equal(expectedResponse, result);
        await requestManager.DidNotReceive().CreatePendingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPayloadDoesNotMatch_ThrowsDomainException()
    {
        var jsonOptions = new JsonOptions();
        var options = Microsoft.Extensions.Options.Options.Create(jsonOptions);

        var key = "conflict-key";
        var userId = Guid.NewGuid();

        var context = Substitute.For<IIdempotencyContext>();
        context.IsEnabled.Returns(true);
        context.Key.Returns(key);
        context.UserId.Returns(userId);

        var request = new TestRequest("value", 1);
        var requestHash = ComputeHash(request, jsonOptions.SerializerOptions);

        var record = new IdempotencyRecord
        {
            RequestName = nameof(TestRequest),
            Key = key,
            UserId = userId,
            RequestHash = "different-hash",
            ResponseContent = JsonSerializer.Serialize(new TestResponse(Guid.NewGuid(), "ok"), jsonOptions.SerializerOptions),
            CreatedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        var requestManager = Substitute.For<IRequestManager>();
        requestManager
            .FindAsync(nameof(TestRequest), key, userId, Arg.Any<CancellationToken>())
            .Returns(record);

        var logger = NullLogger<IdempotencyBehavior<TestRequest, TestResponse>>.Instance;
        var behavior = new IdempotencyBehavior<TestRequest, TestResponse>(context, requestManager, logger, options);

        Task<TestResponse> Next() => Task.FromResult(new TestResponse(Guid.NewGuid(), "unexpected"));

        var exception = await Assert.ThrowsAsync<DomainException>(() => behavior.Handle(request, Next, CancellationToken.None));

        Assert.Equal("Idempotency key conflict: payload does not match previous request.", exception.Message);
        Assert.Equal("Idempotency-Key", exception.PropertyName);
    }

    private static string ComputeHash(TestRequest request, JsonSerializerOptions options)
    {
        var payload = JsonSerializer.Serialize(request, options);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var hashBytes = SHA256.HashData(payloadBytes);
        return Convert.ToHexString(hashBytes);
    }

    private sealed record TestRequest(string Name, int Value) : IIdempotentRequest<TestResponse>;

    private sealed record TestResponse(Guid Id, string Status);
}
