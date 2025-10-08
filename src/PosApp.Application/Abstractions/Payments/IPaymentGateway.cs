using System.Threading;

namespace PosApp.Application.Abstractions.Payments;

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken);
}

public sealed record PaymentChargeRequest(Guid TicketId, decimal Amount, bool ShouldSucceed);

public sealed record PaymentResult(bool Success, string? ProviderReference, string? Error);


