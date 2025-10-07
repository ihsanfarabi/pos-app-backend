using PosApp.Domain.Entities;
using PosApp.Domain.Exceptions;

namespace PosApp.Domain.Tests;

public class MenuItemTests
{
    [Fact]
    public void Create_WithValidValues_ReturnsConfiguredMenuItem()
    {
        var menuItem = MenuItem.Create("  Latte  ", 4.50m);

        Assert.NotEqual(Guid.Empty, menuItem.Id);
        Assert.Equal("Latte", menuItem.Name);
        Assert.Equal(4.50m, menuItem.Price);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Create_WithMissingName_ThrowsDomainException(string invalidName)
    {
        var exception = Assert.Throws<DomainException>(() => MenuItem.Create(invalidName, 2.50m));

        Assert.Equal("Menu item name is required.", exception.Message);
        Assert.Equal("name", exception.PropertyName);
    }

    [Fact]
    public void Create_WithNegativePrice_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => MenuItem.Create("Espresso", -1m));

        Assert.Equal("Menu item price cannot be negative.", exception.Message);
        Assert.Equal("price", exception.PropertyName);
    }

    [Fact]
    public void Update_WithValidValues_ChangesState()
    {
        var menuItem = MenuItem.Create("Espresso", 3m);

        menuItem.Update("  Iced Espresso ", 3.50m);

        Assert.Equal("Iced Espresso", menuItem.Name);
        Assert.Equal(3.50m, menuItem.Price);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Update_WithMissingName_ThrowsDomainException(string invalidName)
    {
        var menuItem = MenuItem.Create("Latte", 4m);

        var exception = Assert.Throws<DomainException>(() => menuItem.Update(invalidName, 4m));

        Assert.Equal("Menu item name is required.", exception.Message);
        Assert.Equal("name", exception.PropertyName);
    }

    [Fact]
    public void Update_WithNegativePrice_ThrowsDomainException()
    {
        var menuItem = MenuItem.Create("Mocha", 5m);

        var exception = Assert.Throws<DomainException>(() => menuItem.Update("Mocha", -0.5m));

        Assert.Equal("Menu item price cannot be negative.", exception.Message);
        Assert.Equal("price", exception.PropertyName);
    }
}
