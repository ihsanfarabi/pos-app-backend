using PosApp.Domain.Exceptions;

namespace PosApp.Domain.Entities;

public class Ticket
{
    private readonly List<TicketLine> _lines = new();

    private Ticket()
    {
    }

    private Ticket(Guid id, DateTime createdAt)
    {
        Id = id;
        Status = TicketStatus.Open;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Status { get; private set; } = TicketStatus.Open;

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public IReadOnlyCollection<TicketLine> Lines => _lines.AsReadOnly();

    public static Ticket Create()
    {
        return new Ticket(Guid.NewGuid(), DateTime.UtcNow);
    }

    public TicketLine AddLine(Guid menuItemId, decimal unitPrice, int quantity)
    {
        EnsureOpen();

        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("Menu item is required.", "menuItemId");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.", "qty");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Unit price cannot be negative.", "unitPrice");
        }

        var existing = _lines.FirstOrDefault(line => line.Matches(menuItemId, unitPrice));
        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
            return existing;
        }

        var line = TicketLine.Create(Id, menuItemId, unitPrice, quantity);
        _lines.Add(line);
        return line;
    }

    public void PayCash()
    {
        EnsureOpen();
        Status = TicketStatus.Paid;
    }

    public decimal GetTotal()
    {
        return _lines.Sum(line => line.LineTotal);
    }

    private void EnsureOpen()
    {
        if (!string.Equals(Status, TicketStatus.Open, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Ticket is not open.", "ticketId");
        }
    }
}

public static class TicketStatus
{
    public const string Open = "Open";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
}

public class TicketLine
{
    private TicketLine()
    {
    }

    private TicketLine(Guid ticketId, Guid menuItemId, decimal unitPrice, int quantity)
    {
        TicketId = ticketId;
        MenuItemId = menuItemId;
        UnitPrice = unitPrice;
        Qty = quantity;
    }

    public Guid Id { get; private set; }

    public Guid TicketId { get; private set; }

    public Guid MenuItemId { get; private set; }

    public int Qty { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => UnitPrice * Qty;

    internal static TicketLine Create(Guid ticketId, Guid menuItemId, decimal unitPrice, int quantity)
    {
        if (ticketId == Guid.Empty)
        {
            throw new DomainException("Ticket is required for line items.");
        }

        if (menuItemId == Guid.Empty)
        {
            throw new DomainException("Menu item is required.", "menuItemId");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.", "qty");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Unit price cannot be negative.", "unitPrice");
        }

        return new TicketLine(ticketId, menuItemId, unitPrice, quantity);
    }

    internal void IncreaseQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.", "qty");
        }

        Qty += quantity;
    }

    internal bool Matches(Guid menuItemId, decimal unitPrice)
    {
        return MenuItemId == menuItemId && UnitPrice == unitPrice;
    }
}
