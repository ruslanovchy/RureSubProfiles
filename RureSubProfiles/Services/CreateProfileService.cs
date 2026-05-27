using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using RureSubProfiles.Models;
using RureSubProfiles.Models.Dto;
using System.Text.Json;

namespace RureSubProfiles.Services;

public class CreateProfileService : BackgroundService
{
    private readonly IServiceScopeFactory scopefactory;

    private readonly ConsumerConfig config;
    private readonly ILogger<CreateProfileService> logger;

    public CreateProfileService(IServiceScopeFactory scopeFactory, ConsumerConfig config, ILogger<CreateProfileService> logger)
    {
        this.scopefactory = scopeFactory;

        this.config = config;
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

                var messageKeyRaw = result.Message.Key;

                if (dto == null || string.IsNullOrEmpty(dto.UserName) ||
                    string.IsNullOrEmpty(messageKeyRaw) || !Guid.TryParse(messageKeyRaw, out var messageKey))
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
                    db.Profiles.Add(new Profile
                    {
                        UserName = dto.UserName,
                        DisplayName = dto.UserName,
                        CreatedAt = DateTime.UtcNow,
                        UserId = dto.UserId
                    });

                    db.InboxMessages.Add(new InboxMessage
                    {
                        Id = messageKey,
                        ProcessedAt = DateTime.UtcNow,
                        Topic = "user-created"
                    });

                    await db.SaveChangesAsync(stoppingToken);
                }
                finally
                {
                    consumer.Commit(result);
                }

            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while deserializing data!");
            }
        }
    }
}
