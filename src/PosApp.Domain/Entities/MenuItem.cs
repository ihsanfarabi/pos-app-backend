namespace PosApp.Domain.Entities;

public class MenuItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public decimal Price { get; set; } // decimal(9,2) semantics
}
