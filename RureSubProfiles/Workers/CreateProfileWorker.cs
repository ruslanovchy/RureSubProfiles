using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RureSubProfiles.Models;
using RureSubProfiles.Models.Dto;
using RureSubProfiles.Services;
using System.Text.Json;

namespace RureSubProfiles.Workers;



public class CreateProfileWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopefactory;

    private readonly ConsumerConfig config;
    private readonly ISnowflakeIdGenerator snowflakeIdGenerator;
    private readonly ILogger<CreateProfileWorker> logger;

    private static bool IsDuplicateInboxMessage(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException postgres &&
               postgres.SqlState == PostgresErrorCodes.UniqueViolation;
    }

    public CreateProfileWorker(
        IServiceScopeFactory scopeFactory, 
        ConsumerConfig config, 
        ISnowflakeIdGenerator snowflakeIdGenerator,
        ILogger<CreateProfileWorker> logger)
    {
        this.scopefactory = scopeFactory;

        this.config = config;
        this.snowflakeIdGenerator = snowflakeIdGenerator;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        consumer.Subscribe("user-created");

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);

            using var scope = scopefactory.CreateScope();

            using var db = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();

            if (string.IsNullOrEmpty(result.Message.Value))
            {
                consumer.Commit(result);
                continue;   
            }

            try
            {
                var dto = JsonSerializer.Deserialize<CreateProfileDto>(result.Message.Value);

                var messageIdRaw = result.Message.Key;

                if (dto == null || string.IsNullOrEmpty(dto.UserName) ||
                    string.IsNullOrEmpty(messageIdRaw) || !Guid.TryParse(messageIdRaw, out var messageId))
                {
                    consumer.Commit(result);
                    continue;
                }

                if (await db.Profiles.AnyAsync(p => p.UserId == dto.UserId || p.UserName == dto.UserName, cancellationToken: stoppingToken))
                {
                    consumer.Commit(result);
                    continue;
                }

                try
                {
                    var profile = new Profile
                    {
                        UserName = dto.UserName,
                        RedisId = snowflakeIdGenerator.NextId(),
                        DisplayName = dto.UserName,
                        CreatedAt = DateTime.UtcNow,
                        UserId = dto.UserId
                    };

                    db.Profiles.Add(profile);

                    db.InboxMessages.Add(new InboxMessage
                    {
                        Id = messageId,
                        ProcessedAt = DateTime.UtcNow,
                        Topic = "user-created"
                    });

                    db.OutboxMessages.Add(new OutboxMessage
                    {
                        Id = Guid.CreateVersion7(),
                        Topic = "profile-created",
                        Content = JsonSerializer.Serialize(profile),
                    });

                    await db.SaveChangesAsync(stoppingToken);
                }
                catch (DbUpdateException ex) when (IsDuplicateInboxMessage(ex))
                {
                    logger.LogError(ex, "Message {messageId} already was processed!", messageId);

                    consumer.Commit(result);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occured while processing message!");
                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while deserializing data!");
            }
        }
    }
}
