using Connecto.Infrastructure.Services.Data;
using Microsoft.EntityFrameworkCore;

namespace Connecto.Api.Media;

public sealed class MediaCleanupWorker(IServiceScopeFactory scopes, ILogger<MediaCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do {
            try { await Cleanup(stoppingToken); }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError(error, "Media cleanup failed; will retry"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task Cleanup(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConnectoDbContext>();
        var store = scope.ServiceProvider.GetRequiredService<IMediaObjectStore>();
        if (!store.IsAvailable) return;
        var expired = DateTime.UtcNow.AddHours(-1);
        var rows = await db.MediaAssets.Where(x => x.DeletedAt == null &&
            (x.Stage == "withdrawn" || x.Stage == "rejected" ||
             x.Stage == "pending" && x.UploadExpiresAt < expired ||
             x.Stage == "ready" && x.QuarantineKey != "" && x.ProcessedAt < expired))
            .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
        foreach (var asset in rows)
        {
            try {
                await store.DeleteAsync(asset.QuarantineKey, ct);
                if (asset.Stage != "ready" && asset.DisplayKey is not null)
                    await store.DeleteAsync(asset.DisplayKey, ct);
                if (asset.Stage != "ready") asset.DeletedAt = DateTime.UtcNow;
                else asset.QuarantineKey = "";
                await db.SaveChangesAsync(ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            { logger.LogWarning(error, "Media cleanup retry needed for asset {AssetId}", asset.Id); }
        }
    }
}
