using PosApp.Application.Abstractions.Payments;

namespace PosApp.Infrastructure.Payments;

public sealed class MockPaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken)
    {
        if (request.ShouldSucceed)
        {
            var reference = $"MOCK-{Guid.NewGuid():N}";
            return Task.FromResult(new PaymentResult(true, reference, null));
        }

        return Task.FromResult(new PaymentResult(false, null, "Mock payment declined"));
    }
}


