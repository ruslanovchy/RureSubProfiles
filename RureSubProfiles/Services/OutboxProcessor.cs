using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RureSubProfiles.Models;

namespace RureSubProfiles.Services;

public class OutboxProcessor : BackgroundService
{
    private readonly ProducerConfig config;
    private readonly ILogger<OutboxProcessor> logger;
    private readonly IServiceScopeFactory scopeFactory;

    public OutboxProcessor(
        [FromServices] ProducerConfig config, 
        [FromServices] ILogger<OutboxProcessor> logger, 
        [FromServices] IServiceScopeFactory scopeFactory)
    {
        this.config = config;
        this.logger = logger;
        this.scopeFactory = scopeFactory;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var producer = new ProducerBuilder<string, string>(config).Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();

            var messages = await db.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.OccuredAt)
                .Take(20)
                .ToListAsync(stoppingToken);

            foreach (var message in messages)
            {
                try
                {
                    await producer.ProduceAsync(message.Topic, new Message<string, string> { Key = message.Id.ToString(), Value = message.Content }, stoppingToken);
                    message.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    message.Error = ex.Message;
                }
            }

            await db.SaveChangesAsync(stoppingToken);
            await Task.Delay(1000, stoppingToken);
        }
    }
}
