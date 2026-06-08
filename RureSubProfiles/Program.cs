using Amazon.S3;
using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RureSubProfiles.Models;
using RureSubProfiles.Services;
using RureSubProfiles.Workers;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IImageResizer, ImageResizer>();

#region Db

var connectionString = builder.Configuration.GetConnectionString("Db");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("Connection string is null or empty!");
}

builder.Services.AddDbContext<ProfilesDbContext>(options =>
{
    options.UseNpgsql(connectionString).UseLoggerFactory(LoggerFactory.Create(b => b.AddFilter((_,_) => false)));
});

#endregion

#region Jwt

string? jwtKey = builder.Configuration["JWT:Key"];

if (string.IsNullOrEmpty(jwtKey))
{
    throw new Exception("Jwt key is null or empty!");
}

byte[] jwtBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options => 
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,

        IssuerSigningKey = new SymmetricSecurityKey(jwtBytes)
    };
});

#endregion

#region Cors

builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

#endregion

#region Kafka

var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"];
var kafkaGroupId = builder.Configuration["Kafka:BootstrapServers"];

if (string.IsNullOrEmpty(kafkaBootstrapServers) || string.IsNullOrEmpty(kafkaGroupId))
{
    throw new Exception("Kafka did not configured!");
}

var kafkaConfig = new ConsumerConfig
{
    BootstrapServers = kafkaBootstrapServers,
    GroupId = kafkaGroupId,
    AutoOffsetReset = AutoOffsetReset.Earliest,
    EnableAutoCommit = false,
    EnableAutoOffsetStore = false,
};

var kafkaProducerConfig = new ProducerConfig
{
    BootstrapServers = kafkaBootstrapServers
};

builder.Services.AddSingleton(kafkaConfig);
builder.Services.AddSingleton(kafkaProducerConfig);

builder.Services.AddHostedService<CleanerWorker>();
builder.Services.AddHostedService<UserFollowedWorker>();
builder.Services.AddHostedService<CreateProfileWorker>();
builder.Services.AddHostedService<PostChangedWorker>();
builder.Services.AddHostedService<OutboxWorker>();

#endregion

#region S3

var s3ServiceURL = builder.Configuration["S3:ServiceURL"];

if (string.IsNullOrEmpty(s3ServiceURL))
{
    throw new Exception("S3 was not configured!");
}

var s3Config = new AmazonS3Config
{
    ServiceURL = s3ServiceURL,
    ForcePathStyle = true
};

var s3Client = new AmazonS3Client("minioadmin", "minioadmin", s3Config);

builder.Services.AddSingleton(s3Client);

#endregion

#region Redis

var redisFollowersConnectionString = builder.Configuration["Redis:FollowersConnectionString"];

if (string.IsNullOrEmpty(redisFollowersConnectionString))
{
    throw new Exception("Redis was not configured!");
}

builder.Services.AddKeyedSingleton<IConnectionMultiplexer>("followers", (sp, key) =>
{
    return ConnectionMultiplexer.Connect(redisFollowersConnectionString);
});

#endregion

builder.Services.AddSingleton<ISnowflakeIdGenerator>(sp =>
{
    return new SnowflakeIdGenerator(0);
});

builder.Services.AddSingleton<IFollowersService, FollowersService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
