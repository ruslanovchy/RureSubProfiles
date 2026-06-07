using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using RureSubProfiles.Models;
using RureSubProfiles.Models.Dto;
using System.Text.Json;

namespace RureSubProfiles.Workers;

public class UserFollowedWorker : BackgroundService
{
    private readonly ConsumerConfig config;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<UserFollowedWorker> logger;

    public UserFollowedWorker(
        ConsumerConfig config,
        IServiceScopeFactory scopeFactory,
        ILogger<UserFollowedWorker> logger)
    {
        this.config = config;
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe(["user-followed", "user-unfollowed"]);

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume();

            var messageIdRaw = result.Message.Key;

            if (string.IsNullOrEmpty(messageIdRaw) || !Guid.TryParse(messageIdRaw, out var messageId))
            {
                logger.LogError("Key was not specified on kafka message! Topic: {topic}", result.Topic);

                consumer.Commit(result);
                continue;
            }

            var dto = JsonSerializer.Deserialize<ChangeSubscriptionDto>(result.Message.Value);

            if (dto == null)
            {
                logger.LogError("Could not parse dto while processing kafka message! Topic: {topic}", result.Topic);
                consumer.Commit(result);
                continue;
            }

            using var scope = scopeFactory.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();

            var followerProfile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == dto.FollowerId, stoppingToken);
            var followingProfile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == dto.FollowingId, stoppingToken);

            if (followerProfile == null || followingProfile == null)
            {
                logger.LogError("Profile not found! Topic: {topic}", result.Topic);
                consumer.Commit(result);
                continue;
            }

            if (result.Topic == "user-followed")
            {
                followerProfile.FollowingsCount++;
                followingProfile.FollowersCount++;
            }
            else if (result.Topic == "user-unfollowed")
            {
                followerProfile.FollowingsCount--;
                followingProfile.FollowersCount--;
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
