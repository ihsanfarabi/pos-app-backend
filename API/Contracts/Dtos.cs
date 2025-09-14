namespace PosApi.Contracts;

public record AddLineDto(Guid MenuItemId, int Qty);
public record CreateMenuItemDto(string Name, decimal Price);