namespace PosApi.Domain;

public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TicketLine> Lines { get; set; } = new();
}

public class TicketLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid MenuItemId { get; set; }
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; } // snapshot of MenuItem.Price at time of add
}