using PosApp.Application.Common;

namespace PosApp.Application.Features.Tickets;

public sealed record TicketListItemResponse(Guid Id, string Status, DateTime CreatedAt);

public sealed record TicketListResult(IReadOnlyList<TicketListItemResponse> Items, PaginationMetadata Pagination);

public sealed record TicketLineResponse(Guid Id, string ItemName, int Qty, decimal UnitPrice, decimal LineTotal);

public sealed record TicketDetailsResponse(Guid Id, string Status, DateTime CreatedAt, IReadOnlyList<TicketLineResponse> Lines, decimal Total);

public sealed record TicketPaymentResponse(Guid Id, string Status, decimal Total);
