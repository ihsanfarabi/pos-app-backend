namespace PosApi.Payments;

public interface ITicketRepository
{
    Task<TicketData?> GetAsync(Guid id, CancellationToken ct);
    Task UpdateStatusAsync(Guid id, string status, CancellationToken ct);
    Task<decimal> GetTotalAsync(Guid id, CancellationToken ct);
}

public record TicketData(Guid Id, string Status);

public interface IPaymentGateway
{
    Task<string> ChargeAsync(decimal amount, string method, CancellationToken ct);
}

public interface IPaymentService
{
    Task<PaymentResult> PayAsync(Guid ticketId, string method, CancellationToken ct);
}

public record PaymentResult(Guid TicketId, string Status, decimal Total, string TransactionId);

public class PaymentService : IPaymentService
{
    private readonly ITicketRepository _tickets;
    private readonly IPaymentGateway _gateway;

    public PaymentService(ITicketRepository tickets, IPaymentGateway gateway)
    {
        _tickets = tickets;
        _gateway = gateway;
    }

    public async Task<PaymentResult> PayAsync(Guid ticketId, string method, CancellationToken ct)
    {
        var t = await _tickets.GetAsync(ticketId, ct);
        if (t is null || t.Status != "Open")
            throw new InvalidOperationException("Ticket invalid or not open.");

        var total = await _tickets.GetTotalAsync(ticketId, ct);
        if (total <= 0) throw new InvalidOperationException("Ticket total must be > 0.");

        var tx = method == "cash"
            ? "CASH-" + Guid.NewGuid().ToString("N")[..8]
            : await _gateway.ChargeAsync(total, method, ct);

        await _tickets.UpdateStatusAsync(ticketId, "Paid", ct);
        return new PaymentResult(ticketId, "Paid", total, tx);
    }
}
