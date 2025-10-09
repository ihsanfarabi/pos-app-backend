using PosApp.Application.Abstractions.Idempotency;

namespace PosApp.Infrastructure.Idempotency;

public sealed class IdempotencyContext : IIdempotencyContext
{
    private bool _initialized;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Key);

    public string? Key { get; private set; }

    public Guid? UserId { get; private set; }

    public void Set(string key, Guid? userId)
    {
        if (_initialized)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        Key = key.Trim();
        UserId = userId;
        _initialized = true;
    }
}
