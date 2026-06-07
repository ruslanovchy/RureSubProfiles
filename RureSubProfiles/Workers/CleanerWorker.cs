using Microsoft.EntityFrameworkCore;
using RureSubProfiles.Models;

namespace RureSubProfiles.Workers;

public class CleanerWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(new Random().Next(0, 2000)), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();

            await db.OutboxMessages
                .Where(m => m.ProcessedAt != null && (DateTime.UtcNow - m.ProcessedAt).Value.Days >= 7)
                .ExecuteDeleteAsync(stoppingToken);

            await db.InboxMessages
                .Where(m => (DateTime.UtcNow - m.ProcessedAt).Days >= 7)
                .ExecuteDeleteAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
        }
    }
}
