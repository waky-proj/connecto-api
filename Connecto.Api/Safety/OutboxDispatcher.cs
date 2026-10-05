using Connecto.Domain.Entities;
using Connecto.Infrastructure.Services.Data;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Safety;

// The receipt and projection commit together. A crash before commit leaves the outbox row
// pending; a replay after commit sees the receipt and cannot create another journal effect.
public sealed class OutboxDispatcher(IServiceScopeFactory scopes, ILogger<OutboxDispatcher> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchBatchAsync(stoppingToken); }
            catch (Exception error) when (error is not OperationCanceledException)
            { logger.LogError(error, "Safety outbox dispatch failed; pending events will be retried"); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
        var ids = await db.OutboxEvents.AsNoTracking().Where(x => x.DeliveredAt == null)
            .OrderBy(x => x.OccurredAt).Select(x => x.Id).Take(50).ToListAsync(cancellationToken);
        var processed = 0;
        foreach (var id in ids)
        {
            using var eventScope = scopes.CreateScope();
            var eventDb = eventScope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
            var item = await eventDb.OutboxEvents.FindAsync([id], cancellationToken);
            if (item is null || item.DeliveredAt is not null) continue;
            var received = await eventDb.ConsumerReceipts.AnyAsync(x => x.EventId == id && x.Consumer == "journey-v1", cancellationToken);
            if (!received)
            {
                eventDb.JourneyEvents.Add(new JourneyEvent { EventId = id, Type = item.Type,
                    ActorId = item.ActorId, SubjectId = item.SubjectId, AggregateId = item.AggregateId,
                    OccurredAt = item.OccurredAt });
                eventDb.ConsumerReceipts.Add(new ConsumerReceipt { EventId = id, Consumer = "journey-v1" });
            }
            item.Attempts++;
            item.DeliveredAt = DateTime.UtcNow;
            try { await eventDb.SaveChangesAsync(cancellationToken); processed++; }
            catch (DbUpdateException)
            {
                // A competing worker may have committed the same event. The next pass sees
                // its receipt. Other database failures remain visible in the service log.
                eventDb.ChangeTracker.Clear();
                if (!await eventDb.ConsumerReceipts.AnyAsync(x => x.EventId == id && x.Consumer == "journey-v1", cancellationToken))
                    throw;
            }
        }
        return processed;
    }
}
