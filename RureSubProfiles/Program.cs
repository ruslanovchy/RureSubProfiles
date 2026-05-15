using Amazon.S3;
using Confluent.Kafka;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RureSubProfiles.Models;
using RureSubProfiles.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddSingleton<IImageResizer, ImageResizer>();

#region Db

var connectionString = builder.Configuration.GetConnectionString("Db");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("Connection string is null or empty!");
}

builder.Services.AddDbContext<ProfilesDbContext>(options =>
{
    options.UseNpgsql(connectionString);
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
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

#endregion

#region Kafka

var kafkaConfig = new ConsumerConfig
{
    BootstrapServers = "localhost:9092",
    GroupId = "profile-service",
    AutoOffsetReset = AutoOffsetReset.Earliest
};

builder.Services.AddSingleton(kafkaConfig);

builder.Services.AddHostedService<CreateProfileService>();

#endregion

#region S3

var s3Config = new AmazonS3Config
{
    ServiceURL = "http://localhost:9000",
    ForcePathStyle = true
};

var s3Client = new AmazonS3Client("minioadmin", "minioadmin", s3Config);

builder.Services.AddSingleton(s3Client);

#endregion

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
}

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
