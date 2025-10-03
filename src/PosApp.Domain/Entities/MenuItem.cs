using PosApp.Domain.Exceptions;

namespace PosApp.Domain.Entities;

public class MenuItem
{
    private MenuItem()
    {
    }

    private MenuItem(Guid id, string name, decimal price)
    {
        Id = id;
        Name = name;
        Price = price;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = default!;

    public decimal Price { get; private set; }

    public static MenuItem Create(string name, decimal price)
    {
        var normalizedName = NormalizeName(name);
        EnsureValid(normalizedName, price);
        return new MenuItem(Guid.NewGuid(), normalizedName, price);
    }

    public void Update(string name, decimal price)
    {
        var normalizedName = NormalizeName(name);
        EnsureValid(normalizedName, price);
        Name = normalizedName;
        Price = price;
    }

    private static void EnsureValid(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Menu item name is required.", "name");
        }

        if (price < 0)
        {
            throw new DomainException("Menu item price cannot be negative.", "price");
        }
    }

    private static string NormalizeName(string name) => name.Trim();
}
