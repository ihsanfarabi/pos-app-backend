namespace PosApi.Contracts;

public record AddLineDto(Guid MenuItemId, int Qty);
public record CreateMenuItemDto(string Name, decimal Price);
public record RegisterDto(string Email, string Password, string? Role);
public record LoginDto(string Email, string Password);