using FluentAssertions;
using Moq;

namespace PosApi.Payments.Tests;

public class PaymentServiceTests
{
    [Fact]
    public async Task PayAsync_cash_updates_status_and_returns_total()
    {
        var tickets = new Mock<ITicketRepository>();
        tickets.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new TicketData(Guid.NewGuid(), "Open"));
        tickets.Setup(x => x.GetTotalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(123.45m);

        var gateway = new Mock<IPaymentGateway>(MockBehavior.Strict);
        var svc = new PaymentService(tickets.Object, gateway.Object);

        var res = await svc.PayAsync(Guid.NewGuid(), "cash", CancellationToken.None);

        res.Status.Should().Be("Paid");
        res.Total.Should().Be(123.45m);
        tickets.Verify(x => x.UpdateStatusAsync(It.IsAny<Guid>(), "Paid", It.IsAny<CancellationToken>()), Times.Once);
        gateway.VerifyNoOtherCalls();
    }
}