using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using RureSubProfiles.Models;
using RureSubProfiles.Models.Dto;
using System.Text.Json;

namespace RureSubProfiles.Workers;

public class PostChangedWorker : BackgroundService
{
    private readonly ConsumerConfig config;
    private readonly ProducerConfig producerConfig;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<UserFollowedWorker> logger;

    public PostChangedWorker(
        ConsumerConfig config,
        ProducerConfig producerConfig,
        IServiceScopeFactory scopeFactory,
        ILogger<UserFollowedWorker> logger)
    {
        this.config = config;
        this.producerConfig = producerConfig;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new ConsumerBuilder<string, string>(config).Build();
        var producer = new ProducerBuilder<string, string>(producerConfig).Build();

        consumer.Subscribe(["post-created", "post-deleted"]);

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);

            var messageIdRaw = result.Message.Key;

            if (string.IsNullOrEmpty(messageIdRaw) || !Guid.TryParse(messageIdRaw, out var messageId))
            {
                logger.LogError("Key was not specified on kafka message! Topic: {topic}", result.Topic);

                consumer.Commit(result);
                continue;
            }

            var dto = JsonSerializer.Deserialize<PostChangedDto>(result.Message.Value);

            if (dto == null)
            {
                logger.LogError("Could not parse dto while processing kafka message! Topic: {topic}", result.Topic);
                consumer.Commit(result);
                continue;
            }

            using var scope = scopeFactory.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();

            var profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == dto.AuthorId, stoppingToken);

            if (profile == null)
            {
                logger.LogError("Profile not found! Topic: {topic}", result.Topic);
                consumer.Commit(result);
                continue;
            }

            if (result.Topic == "post-created")
            {
                profile.PostsCount++;
            }
            else if (result.Topic == "post-deleted")
            {
                profile.PostsCount--;
            }


            db.InboxMessages.Add(new InboxMessage
            {
                Id = messageId,
                Topic = result.Topic,
                ProcessedAt = DateTime.UtcNow
            });

            try
            {
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception)
            {
                consumer.Commit(result);
                continue;
            }
            consumer.Commit(result);
        }

    }

}
