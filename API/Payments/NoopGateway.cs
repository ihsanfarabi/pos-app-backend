using PosApi.Payments;

namespace PosApi.API.Payments;

public class NoopGateway : IPaymentGateway
{
    public Task<string> ChargeAsync(decimal amount, string method, CancellationToken ct)
    {
        // simulate external gateway success
        return Task.FromResult($"{method.ToUpperInvariant()}-TX-{Guid.NewGuid().ToString("N")[..8]}");
    }
}


