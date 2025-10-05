namespace PosApp.Application.Contracts;

public record AddLineDto(Guid MenuItemId, int Qty);
public record CreateMenuItemDto(string Name, decimal Price);
public record UpdateMenuItemDto(string Name, decimal Price);
public record RegisterDto(string Email, string Password, string? Role);
public record LoginDto(string Email, string Password);
public record MenuQueryDto(string? Q, int PageIndex = 0, int PageSize = 20);
public record TicketListQueryDto(int PageIndex = 0, int PageSize = 20);
