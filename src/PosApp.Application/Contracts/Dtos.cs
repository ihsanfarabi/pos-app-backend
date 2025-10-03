namespace PosApp.Application.Contracts;

public record AddLineDto(Guid MenuItemId, int Qty);
public record CreateMenuItemDto(string Name, decimal Price);
public record UpdateMenuItemDto(string Name, decimal Price);
public record RegisterDto(string Email, string Password, string? Role);
public record LoginDto(string Email, string Password);
public record MenuQueryDto(string? Q, int? Page, int? PageSize);
public record TicketListQueryDto(int? Page, int? PageSize);
