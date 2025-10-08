using PosApp.Application.Abstractions.Persistence;
using PosApp.Application.Abstractions.Payments;
using PosApp.Application.Common;
using PosApp.Application.Contracts;
using PosApp.Application.Features.Tickets.Commands;
using PosApp.Application.Features.Tickets.Queries;
using PosApp.Domain.Entities;
using PosApp.Domain.Exceptions;

namespace PosApp.Application.Tests;

public class TicketHandlersTests
{
    [Fact]
    public async Task CreateTicketCommandHandler_PersistsTicket()
    {
        var repository = Substitute.For<ITicketRepository>();
        Ticket? captured = null;
        repository
            .AddAsync(Arg.Do<Ticket>(ticket => captured = ticket), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new CreateTicketCommandHandler(repository);

        var id = await handler.Handle(new CreateTicketCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.NotNull(captured);
        Assert.Equal(id, captured!.Id);
        Assert.Equal(TicketStatus.Open, captured.Status);
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddTicketLineCommandHandler_AddsLineAndSaves()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        var menuRepository = Substitute.For<IMenuRepository>();
        var ticket = Ticket.Create();
        ticketRepository
            .GetWithLinesAsync(ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);
        var menuItem = MenuItem.Create("Latte", 4m);
        menuRepository
            .GetByIdAsync(menuItem.Id, Arg.Any<CancellationToken>())
            .Returns(menuItem);

        var handler = new AddTicketLineCommandHandler(ticketRepository, menuRepository);
        var command = new AddTicketLineCommand(ticket.Id, new AddLineDto(menuItem.Id, 2));

        await handler.Handle(command, CancellationToken.None);

        Assert.Single(ticket.Lines);
        var line = ticket.Lines.First();
        Assert.Equal(menuItem.Id, line.MenuItemId);
        Assert.Equal(2, line.Qty);
        Assert.Equal(menuItem.Price, line.UnitPrice);
        await ticketRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddTicketLineCommandHandler_WhenTicketMissing_Throws()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        ticketRepository
            .GetWithLinesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Ticket?)null);
        var menuRepository = Substitute.For<IMenuRepository>();
        var handler = new AddTicketLineCommandHandler(ticketRepository, menuRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(new AddTicketLineCommand(Guid.NewGuid(), new AddLineDto(Guid.NewGuid(), 1)), CancellationToken.None));
    }

    [Fact]
    public async Task AddTicketLineCommandHandler_WhenMenuMissing_ThrowsDomainException()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        var ticket = Ticket.Create();
        ticketRepository
            .GetWithLinesAsync(ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);
        var menuRepository = Substitute.For<IMenuRepository>();
        menuRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MenuItem?)null);
        var handler = new AddTicketLineCommandHandler(ticketRepository, menuRepository);
        var command = new AddTicketLineCommand(ticket.Id, new AddLineDto(Guid.NewGuid(), 1));

        var exception = await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Equal("Menu item not found.", exception.Message);
        Assert.Equal("menuItemId", exception.PropertyName);
    }

    [Fact]
    public async Task PayTicketCashCommandHandler_PaysTicketAndReturnsSummary()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        var ticket = Ticket.Create();
        var itemId = Guid.NewGuid();
        ticket.AddLine(itemId, 5m, 2);
        ticketRepository
            .GetWithLinesAsync(ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);

        var handler = new PayTicketCashCommandHandler(ticketRepository);
        var command = new PayTicketCashCommand(ticket.Id);

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(ticket.Id, response.Id);
        Assert.Equal(TicketStatus.Paid.ToString(), response.Status);
        Assert.Equal(10m, response.Total);
        await ticketRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayTicketCashCommandHandler_WhenTicketMissing_Throws()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        ticketRepository
            .GetWithLinesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Ticket?)null);
        var handler = new PayTicketCashCommandHandler(ticketRepository);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(new PayTicketCashCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GetTicketsQueryHandler_NormalizesPaging()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        var tickets = new List<Ticket> { Ticket.Create() };
        var paged = new PagedResult<Ticket>(tickets, 1, 5, 10);
        ticketRepository
            .GetPagedAsync(0, 20, Arg.Any<CancellationToken>())
            .Returns(paged);
        var handler = new GetTicketsQueryHandler(ticketRepository);
        var query = new GetTicketsQuery(new TicketListQueryDto(-1, -1));

        var result = await handler.Handle(query, CancellationToken.None);

        await ticketRepository.Received(1).GetPagedAsync(0, 20, Arg.Any<CancellationToken>());
        Assert.Single(result.Items);
        Assert.Equal(paged.PageIndex, result.PageIndex);
        Assert.Equal(paged.PageSize, result.PageSize);
        Assert.Equal(paged.TotalCount, result.TotalCount);
    }

    [Fact]
    public async Task GetTicketDetailsQueryHandler_MapsLinesWithMenuNames()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        var menuRepository = Substitute.For<IMenuRepository>();
        var ticket = Ticket.Create();
        var menuItem = MenuItem.Create("Mocha", 4m);
        var line = ticket.AddLine(menuItem.Id, menuItem.Price, 1);
        ticketRepository
            .GetWithLinesAsync(ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);
        menuRepository
            .GetByIdsAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ids = ((IEnumerable<Guid>)callInfo[0]).ToArray();
                return Task.FromResult<IReadOnlyDictionary<Guid, MenuItem>>(ids.ToDictionary(id => id, _ => menuItem));
            });

        var handler = new GetTicketDetailsQueryHandler(ticketRepository, menuRepository);

        var result = await handler.Handle(new GetTicketDetailsQuery(ticket.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ticket.Id, result!.Id);
        Assert.Single(result.Lines);
        Assert.Equal("Mocha", result.Lines[0].ItemName);
        Assert.Equal(4m, result.Lines[0].UnitPrice);
        Assert.Equal(line.Qty, result.Lines[0].Qty);
        Assert.Equal(4m, result.Total);
    }

    [Fact]
    public async Task GetTicketDetailsQueryHandler_WhenMissing_ReturnsNull()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        ticketRepository
            .GetWithLinesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Ticket?)null);
        var menuRepository = Substitute.For<IMenuRepository>();
        var handler = new GetTicketDetailsQueryHandler(ticketRepository, menuRepository);

        var result = await handler.Handle(new GetTicketDetailsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task PayTicketWithGatewayCommandHandler_WhenSuccessful_PaysTicketAndReturnsSummary()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        var paymentGateway = Substitute.For<IPaymentGateway>();
        var ticket = Ticket.Create();
        ticket.AddLine(Guid.NewGuid(), 10m, 2);
        ticketRepository
            .GetWithLinesAsync(ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);
        paymentGateway
            .ChargeAsync(Arg.Any<PaymentChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentResult(true, "MOCK-123", null));

        var handler = new PayTicketWithGatewayCommandHandler(ticketRepository, paymentGateway);
        var command = new PayTicketWithGatewayCommand(ticket.Id, true);

        var response = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(ticket.Id, response.Id);
        Assert.Equal(TicketStatus.Paid.ToString(), response.Status);
        Assert.Equal(20m, response.Total);
        await paymentGateway.Received(1).ChargeAsync(Arg.Is<PaymentChargeRequest>(r => r.TicketId == ticket.Id && r.Amount == 20m), Arg.Any<CancellationToken>());
        await ticketRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayTicketWithGatewayCommandHandler_WhenPaymentFails_ThrowsException()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        var paymentGateway = Substitute.For<IPaymentGateway>();
        var ticket = Ticket.Create();
        ticket.AddLine(Guid.NewGuid(), 5m, 1);
        ticketRepository
            .GetWithLinesAsync(ticket.Id, Arg.Any<CancellationToken>())
            .Returns(ticket);
        paymentGateway
            .ChargeAsync(Arg.Any<PaymentChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PaymentResult(false, null, "Insufficient funds"));

        var handler = new PayTicketWithGatewayCommandHandler(ticketRepository, paymentGateway);
        var command = new PayTicketWithGatewayCommand(ticket.Id, false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Equal("Insufficient funds", exception.Message);
        await ticketRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PayTicketWithGatewayCommandHandler_WhenTicketMissing_Throws()
    {
        var ticketRepository = Substitute.For<ITicketRepository>();
        var paymentGateway = Substitute.For<IPaymentGateway>();
        ticketRepository
            .GetWithLinesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Ticket?)null);
        var handler = new PayTicketWithGatewayCommandHandler(ticketRepository, paymentGateway);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(new PayTicketWithGatewayCommand(Guid.NewGuid(), true), CancellationToken.None));

        await paymentGateway.DidNotReceive().ChargeAsync(Arg.Any<PaymentChargeRequest>(), Arg.Any<CancellationToken>());
    }
}
