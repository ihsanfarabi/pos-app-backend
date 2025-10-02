using Microsoft.EntityFrameworkCore;
using PosApi.Infrastructure;
using PosApi.Payments;

namespace PosApi.API.Payments;

public class EfTicketRepository : ITicketRepository
{
    private readonly AppDbContext _db;
    public EfTicketRepository(AppDbContext db) => _db = db;

    public async Task<TicketData?> GetAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? null : new TicketData(t.Id, t.Status);
    }

    public async Task UpdateStatusAsync(Guid id, string status, CancellationToken ct)
    {
        var t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        t.Status = status;
        await _db.SaveChangesAsync(ct);
    }

    public Task<decimal> GetTotalAsync(Guid id, CancellationToken ct)
        => _db.TicketLines.Where(l => l.TicketId == id).SumAsync(l => l.Qty * l.UnitPrice, ct);
}


