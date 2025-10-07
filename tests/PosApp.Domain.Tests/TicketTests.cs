using PosApp.Domain.Entities;
using PosApp.Domain.Exceptions;

namespace PosApp.Domain.Tests;

public class TicketTests
{
    [Fact]
    public void Create_SetsDefaults()
    {
        var before = DateTime.UtcNow;

        var ticket = Ticket.Create();

        var after = DateTime.UtcNow;
        Assert.NotEqual(Guid.Empty, ticket.Id);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Empty(ticket.Lines);
        Assert.InRange(ticket.CreatedAt, before, after);
    }

    [Fact]
    public void AddLine_WithValidValues_ReturnsLineAndStoresIt()
    {
        var ticket = Ticket.Create();
        var menuItemId = Guid.NewGuid();

        var line = ticket.AddLine(menuItemId, 5m, 2);

        Assert.Equal(ticket.Id, line.TicketId);
        Assert.Equal(menuItemId, line.MenuItemId);
        Assert.Equal(2, line.Qty);
        Assert.Equal(5m, line.UnitPrice);
        Assert.Contains(line, ticket.Lines);
    }

    [Fact]
    public void AddLine_WithExistingLine_IncreasesQuantity()
    {
        var ticket = Ticket.Create();
        var menuItemId = Guid.NewGuid();

        var first = ticket.AddLine(menuItemId, 4m, 1);
        var second = ticket.AddLine(menuItemId, 4m, 3);

        Assert.Same(first, second);
        Assert.Single(ticket.Lines);
        Assert.Equal(4, first.Qty);
    }

    [Fact]
    public void AddLine_WithDifferentPrice_AddsSeparateLine()
    {
        var ticket = Ticket.Create();
        var menuItemId = Guid.NewGuid();

        ticket.AddLine(menuItemId, 4m, 1);
        ticket.AddLine(menuItemId, 5m, 1);

        Assert.Equal(2, ticket.Lines.Count);
    }

    [Fact]
    public void AddLine_WithEmptyMenuItemId_ThrowsDomainException()
    {
        var ticket = Ticket.Create();

        var exception = Assert.Throws<DomainException>(() => ticket.AddLine(Guid.Empty, 4m, 1));

        Assert.Equal("Menu item is required.", exception.Message);
        Assert.Equal("menuItemId", exception.PropertyName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddLine_WithInvalidQuantity_ThrowsDomainException(int invalidQuantity)
    {
        var ticket = Ticket.Create();

        var exception = Assert.Throws<DomainException>(() => ticket.AddLine(Guid.NewGuid(), 4m, invalidQuantity));

        Assert.Equal("Quantity must be greater than zero.", exception.Message);
        Assert.Equal("qty", exception.PropertyName);
    }

    [Fact]
    public void AddLine_WithNegativePrice_ThrowsDomainException()
    {
        var ticket = Ticket.Create();

        var exception = Assert.Throws<DomainException>(() => ticket.AddLine(Guid.NewGuid(), -0.5m, 1));

        Assert.Equal("Unit price cannot be negative.", exception.Message);
        Assert.Equal("unitPrice", exception.PropertyName);
    }

    [Fact]
    public void AddLine_WhenTicketNotOpen_ThrowsDomainException()
    {
        var ticket = Ticket.Create();
        ticket.PayCash();

        var exception = Assert.Throws<DomainException>(() => ticket.AddLine(Guid.NewGuid(), 4m, 1));

        Assert.Equal("Ticket is not open.", exception.Message);
        Assert.Equal("ticketId", exception.PropertyName);
    }

    [Fact]
    public void PayCash_ChangesStatusToPaid()
    {
        var ticket = Ticket.Create();

        ticket.PayCash();

        Assert.Equal(TicketStatus.Paid, ticket.Status);
    }

    [Fact]
    public void PayCash_WhenAlreadyPaid_ThrowsDomainException()
    {
        var ticket = Ticket.Create();
        ticket.PayCash();

        var exception = Assert.Throws<DomainException>(ticket.PayCash);

        Assert.Equal("Ticket is not open.", exception.Message);
        Assert.Equal("ticketId", exception.PropertyName);
    }

    [Fact]
    public void GetTotal_SumsLineTotals()
    {
        var ticket = Ticket.Create();
        ticket.AddLine(Guid.NewGuid(), 4m, 2);
        ticket.AddLine(Guid.NewGuid(), 3m, 1);

        var total = ticket.GetTotal();

        Assert.Equal(11m, total);
    }
}
