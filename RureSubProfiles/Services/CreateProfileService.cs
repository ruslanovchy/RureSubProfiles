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
        using var consumer = new ConsumerBuilder<Null, string>(config).Build();

        consumer.Subscribe("user-created");

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = consumer.Consume(stoppingToken);

            using var scope = scopefactory.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ProfilesDbContext>();

            if (string.IsNullOrEmpty(result.Message.Value))
            {
                continue;   
            }

            try
            {
                var dto = JsonSerializer.Deserialize<CreateProfileDto>(result.Message.Value);

                if (dto == null || string.IsNullOrEmpty(dto.UserName))
                {
                    continue;
                }

                if (await db.Profiles.AnyAsync(p => p.UserId == dto.UserId || p.UserName == dto.UserName, cancellationToken: stoppingToken))
                {
                    continue;
                }

                db.Profiles.Add(new Profile
                {
                    UserName = dto.UserName,
                    DisplayName = dto.UserName,
                    CreatedAt = DateTime.UtcNow,
                    UserId = dto.UserId
                });

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error while deserializing data!");
            }
        }
    }
}
