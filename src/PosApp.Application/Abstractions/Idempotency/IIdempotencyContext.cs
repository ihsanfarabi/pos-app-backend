namespace PosApp.Application.Abstractions.Idempotency;

public interface IIdempotencyContext
{
    bool IsEnabled { get; }

    string? Key { get; }

    Guid? UserId { get; }

    void Set(string key, Guid? userId);
}
