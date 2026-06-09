using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RureSubProfiles.Models;
using RureSubProfiles.Models.Dto;
using RureSubProfiles.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Security.Claims;
using System.Text.Json;

namespace RureSubProfiles.Controllers;

public class ProfilesController : Controller
{
    private readonly ILogger<ProfilesController> logger;
    private readonly IImageEncoder encoder;

    public ProfilesController(ILogger<ProfilesController> logger)
    {
        this.logger = logger;
        encoder = new JpegEncoder()
        {
            Quality = 90
        };
    }

    public ProfileResponseDto GetProfileResponse(Profile profile, string? storagePath = null) => new ProfileResponseDto
    {
        Id = profile.Id,
        UserId = profile.UserId,
        UserName = profile.UserName,
        DisplayName = profile.DisplayName,
        Bio = profile.Bio,
        AvatarUrl = storagePath == null ? null : profile.AvatarPath != null ? Path.Combine(storagePath, profile.AvatarPath) : null,
        BannerUrl = storagePath == null ? null : profile.BannerPath != null ? Path.Combine(storagePath, profile.BannerPath) : null,
        ShowFollowers = profile.ShowFollowers,
        ShowFollowings = profile.ShowFollowings,
        IsVerified = profile.IsVerified,
        FollowersCount = profile.FollowersCount,
        FollowingsCount = profile.FollowingsCount,
        PostsCount = profile.PostsCount,
        CreatedAt = profile.CreatedAt
    };

    [HttpGet("/")]
    public async Task<IActionResult> GetProfile(
        [FromServices] ProfilesDbContext db, 
        [FromServices] IConfiguration config,
        [FromServices] IFollowersService followersService,
        [FromQuery] Guid? id, 
        [FromQuery] string? userName,
        [FromQuery] bool? isSettings)
    {
        Profile? profile = null;
        if (id != null)
        {
            profile = db.Profiles.FirstOrDefault(p => p.UserId == id.Value);
        }
        else if (!string.IsNullOrEmpty(userName))
        {
            profile = db.Profiles.FirstOrDefault(p => p.UserName == userName);
        }

        if (profile == null)
        {
            return NotFound();
        }

        string storagePath = config["S3:StoragePath"] ?? "/";

        var result = GetProfileResponse(profile, storagePath);

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !Guid.TryParse(userIdRaw.Value, out var userId))
        {
            result.FollowersCount = result.ShowFollowers ? result.FollowersCount : 0;
            result.FollowingsCount = result.ShowFollowings ? result.FollowingsCount : 0;
            return Ok(result);
        }

        var isFollowed = await followersService.IsFollowed(userId, profile.UserId);

        result.IsFollowed = isFollowed ?? false;

        // Check should we sent true followers and followings count to client
        isSettings ??= false;
        if (userId != profile.UserId)
        {
            result.FollowersCount = result.ShowFollowers ? result.FollowersCount : 0;
            result.FollowingsCount = result.ShowFollowings ? result.FollowingsCount : 0;
        }
        else
        {
            if (!isSettings.Value)
            {
                result.ShowFollowers = true;
                result.ShowFollowings = true;
            }
            result.FollowersCount = profile.FollowersCount;
            result.FollowingsCount = profile.FollowingsCount;
        }

        return Ok(result);
    }

    [HttpPatch("/name")]
    [Authorize]
    public async Task<IActionResult> ChangeDisplayName(
        [FromServices]ProfilesDbContext db, 
        [FromForm]ChangeDisplayNameDto dto)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(dto.NewName))
        {
            return BadRequest();
        }

        var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userId == null || userId.Value == null || userId.Value != dto.UserId.ToString())
        {
            return Unauthorized();
        }

        Profile? profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == dto.UserId);

        if (profile == null)
        {
            return NotFound();
        }

        profile.DisplayName = dto.NewName;

        db.OutboxMessages.Add(new OutboxMessage
        {
            OccuredAt = DateTime.UtcNow,
            Topic = "profile-display-name-changed",
            Content = JsonSerializer.Serialize(new ChangeProfilePropertyDto
            {
                ProfileId = profile.Id,
                UserId = profile.UserId,
                PropertyName = "DisplayName",
                Value = profile.DisplayName
            })
        });

        await db.SaveChangesAsync();

        return Ok();
    }

    [HttpPatch("/bio")]
    [Authorize]
    public async Task<IActionResult> ChangeBio(
        [FromServices] ProfilesDbContext db, 
        [FromForm] ChangeBioDto dto)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(dto.NewBio))
        {
            return BadRequest();
        }

        var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userId == null || userId.Value == null || userId.Value != dto.UserId.ToString())
        {
            return Unauthorized();
        }

        Profile? profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == dto.UserId);

        if (profile == null)
        {
            return NotFound();
        }

        profile.Bio = dto.NewBio;

        await db.SaveChangesAsync();

        return Ok();
    }


    [HttpPatch("/avatar")]
    [Authorize]
    public async Task<IActionResult> ChangeAvatar(
        [FromServices] ProfilesDbContext db,
        [FromServices] IConfiguration config,
        [FromServices] IImageResizer resizer,
        [FromServices] AmazonS3Client s3Client,
        [FromForm] ChangeAvatarDto dto)
    {
        if (!ModelState.IsValid || dto.NewAvatar == null)
        {
            return BadRequest();
        }

        var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userId == null || userId.Value == null || userId.Value != dto.UserId.ToString())
        {
            return Unauthorized();
        }

        Profile? profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == dto.UserId);

        if (profile == null)
        {
            return NotFound();
        }

        string? storagePath = config["S3:StoragePath"];
        string? bucket = config["S3:AvatarsBucket"];

        if (string.IsNullOrEmpty(storagePath) || string.IsNullOrEmpty(bucket))
        {
            return Problem();
        }

        using MemoryStream imageStream = new();
        await dto.NewAvatar.CopyToAsync(imageStream);
        imageStream.Position = 0;
        using Image image = await Image.LoadAsync(imageStream);

        if (image.Width > 512)
        {
            resizer.ResizeImage(image, 512, (int)Math.Round(512f / image.Width * image.Height));
        }
        if (image.Height > 512)
        {
            resizer.ResizeImage(image, (int)Math.Round(512f / image.Height * image.Width), 512);
        }

        MemoryStream resultImageStream = new();

        await image.SaveAsync(resultImageStream, encoder);

        resultImageStream.Position = 0;

        if (!string.IsNullOrEmpty(profile.AvatarPath))
        {
            var pathElements = profile.AvatarPath.Split('/');
            if (pathElements.Length > 1)
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = pathElements[0],
                    Key = pathElements[1]
                };

                try
                {
                    var response = await s3Client.DeleteObjectAsync(deleteRequest);

                    if (response.HttpStatusCode != System.Net.HttpStatusCode.NoContent)
                    {
                        return BadRequest();
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e.Message);
                }
            }
        }

        Guid imageId = Guid.NewGuid();

        var putRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = $"{imageId}.jpg",
            InputStream = resultImageStream,
            ContentType = "image/jpeg",
            AutoCloseStream = true,
        };

        try
        {
            var response = await s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                return BadRequest();
            }

            profile.AvatarPath = $"{bucket}/{imageId}.jpg";

            db.OutboxMessages.Add(new OutboxMessage
            {
                OccuredAt = DateTime.UtcNow,
                Topic = "profile-avatar-changed",
                Content = JsonSerializer.Serialize(new ChangeProfilePropertyDto
                {
                    ProfileId = profile.Id,
                    UserId = profile.UserId,
                    PropertyName = "AvatarUrl",
                    Value = Path.Combine(storagePath, profile.AvatarPath)
                })
            });

            await db.SaveChangesAsync();

            return Ok(Path.Combine(storagePath, profile.AvatarPath));
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
        }

        return Problem();
    }
    

    [HttpPatch("/banner")]
    [RequestSizeLimit(100_000_000)]
    [Authorize]
    public async Task<IActionResult> ChangeBanner(
        [FromServices] ProfilesDbContext db,
        [FromServices] IConfiguration config,
        [FromServices] IImageResizer resizer,
        [FromServices] AmazonS3Client s3Client,
        [FromForm] ChangeBannerDto dto)
    {
        if (!ModelState.IsValid || dto.NewBanner == null)
        {
            return BadRequest();
        }

        var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userId == null || userId.Value == null || userId.Value != dto.UserId.ToString())
        {
            return Unauthorized();
        }

        Profile? profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == dto.UserId);

        if (profile == null)
        {
            return NotFound();
        }

        string? storagePath = config["S3:StoragePath"];
        string? bucket = config["S3:BannersBucket"];

        if (string.IsNullOrEmpty(storagePath) || string.IsNullOrEmpty(bucket))
        {
            return Problem();
        }

        using MemoryStream imageStream = new();
        await dto.NewBanner.CopyToAsync(imageStream);
        imageStream.Position = 0;
        using Image image = await Image.LoadAsync(imageStream);

        if (image.Width > 2048)
        {
            resizer.ResizeImage(image, 2048, (int)Math.Round(2048f / image.Width * image.Height));
        }
        if (image.Height > 2048)
        {
            resizer.ResizeImage(image, (int)Math.Round(2048f / image.Height * image.Width), 2048);
        }

        MemoryStream resultImageStream = new();

        await image.SaveAsync(resultImageStream, encoder);

        resultImageStream.Position = 0;

        if (!string.IsNullOrEmpty(profile.BannerPath))
        {
            var pathElements = profile.BannerPath.Split('/');
            if (pathElements.Length > 1)
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = pathElements[0],
                    Key = pathElements[1]
                };

                try
                {
                    var response = await s3Client.DeleteObjectAsync(deleteRequest);

                    if (response.HttpStatusCode != System.Net.HttpStatusCode.NoContent)
                    {
                        return BadRequest();
                    }
                }
                catch (Exception e)
                {
                    logger.LogError(e.Message);
                }
            }
        }

        Guid imageId = Guid.NewGuid();

        var putRequest = new PutObjectRequest
        {
            BucketName = bucket,
            Key = $"{imageId}.jpg",
            InputStream = resultImageStream,
            ContentType = "image/jpeg",
            AutoCloseStream = true,
        };

        try
        {
            var response = await s3Client.PutObjectAsync(putRequest);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
            {
                return BadRequest();
            }

            profile.BannerPath = $"{bucket}/{imageId}.jpg";
            await db.SaveChangesAsync();

            return Ok(Path.Combine(storagePath, profile.BannerPath));
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
        }

        return Problem();
    }

    [HttpPatch("/showfollowers")]
    [Authorize]
    public async Task<IActionResult> ChangeShowFollowers(
        [FromServices] ProfilesDbContext db,
        [FromForm] ChangeShowFollowersDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userId == null || userId.Value == null || userId.Value != dto.UserId.ToString())
        {
            return Unauthorized();
        }

        Profile? profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == dto.UserId);

        if (profile == null)
        {
            return NotFound();
        }

        profile.ShowFollowers = dto.Value;

        await db.SaveChangesAsync();

        return Ok();
    }

    [HttpPatch("/showfollowings")]
    [Authorize]
    public async Task<IActionResult> ChangeShowFollowings(
        [FromServices] ProfilesDbContext db,
        [FromForm] ChangeShowFollowingsDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var userId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userId == null || userId.Value == null || userId.Value != dto.UserId.ToString())
        {
            return Unauthorized();
        }

        Profile? profile = await db.Profiles.FirstOrDefaultAsync(p => p.UserId == dto.UserId);

        if (profile == null)
        {
            return NotFound();
        }

        profile.ShowFollowings = dto.Value;

        await db.SaveChangesAsync();

        return Ok();
    }

    [HttpGet("/followers")]
    public async Task<IActionResult> GetFollowers(
        [FromServices] ProfilesDbContext db,
        [FromServices] IFollowersService followersService,
        [FromServices] IConfiguration config,
        [FromQuery] Guid? profileId,
        [FromQuery] int page,
        [FromQuery] int pageSize = 10)
    {
        if (profileId == null)
        {
            return BadRequest();
        }

        page = page < 1 ? 1 : page;
        pageSize = (pageSize < 2) ? 2 : (pageSize > 100) ? 100 : pageSize;

        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId.Value);

        if (profile == null)
        {
            return NotFound();
        }

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        var canParseUserId = Guid.TryParse(userIdRaw?.Value, out var userId);

        if (!profile.ShowFollowers)
        {
            if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !canParseUserId)
            {
                return Forbid();
            }

            if (userId != profile.UserId)
            {
                return Forbid();
            }
        }

        var followersRedisIds = await followersService.GetUserFollowers(profile.UserId, pageSize, page);

        string storagePath = config["S3:StoragePath"] ?? "/";

        var followers = await db.Profiles
            .Where(p => followersRedisIds.Contains(p.RedisId))
            .ToListAsync();

        var orderMap = followersRedisIds
            .Select((element, index) => new { element, index })
            .ToDictionary(e => e.element, e => e.index);

        followers = [.. followers
            .OrderBy(f => orderMap[f.RedisId])
            .Select(f => f)];

        var result = followers.Select(f => GetProfileResponse(f, storagePath)).ToArray();

        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !canParseUserId)
        {
            return Ok(result);
        }

        var myFollowings = await followersService.IsFollowed(userId, [.. followers.Select(r => r.RedisId.ToString())]);

        if (myFollowings.Length == result.Length)
        {
            for (int i = 0; i < myFollowings.Length; i++)
            {
                result[i].IsFollowed = myFollowings[i];
            }
        }

        return Ok(result);
    }

    [HttpGet("/followings")]
    public async Task<IActionResult> GetFollowings(
        [FromServices] ProfilesDbContext db,
        [FromServices] IFollowersService followersService,
        [FromServices] IConfiguration config,
        [FromQuery] Guid? profileId,
        [FromQuery] int page,
        [FromQuery] int pageSize = 10)
    {
        if (profileId == null)
        {
            return BadRequest();
        }

        page = page < 1 ? 1 : page;
        pageSize = (pageSize < 2) ? 2 : (pageSize > 100) ? 100 : pageSize;

        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.Id == profileId.Value);

        if (profile == null)
        {
            return NotFound();
        }

        var userIdRaw = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        var canParseUserId = Guid.TryParse(userIdRaw?.Value, out var userId);

        if (!profile.ShowFollowers)
        {
            if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !canParseUserId)
            {
                return Forbid();
            }

            if (userId != profile.UserId)
            {
                return Forbid();
            }
        }

        var followersRedisIds = await followersService.GetUserFollowings(profile.UserId, pageSize, page);

        string storagePath = config["S3:StoragePath"] ?? "/";

        var followers = await db.Profiles
            .Where(p => followersRedisIds.Contains(p.RedisId))
            .ToListAsync();

        var orderMap = followersRedisIds
            .Select((element, index) => new { element, index })
            .ToDictionary(e => e.element, e => e.index);

        var result = followers
            .OrderBy(f => orderMap[f.RedisId])
            .Select(f => GetProfileResponse(f, storagePath)).ToArray();

        if (userIdRaw == null || string.IsNullOrEmpty(userIdRaw.Value) || !canParseUserId)
        {
            return Ok(result);
        }

        for (int i = 0; i < result.Length; i++)
        {
            result[i].IsFollowed = true;
        }

        return Ok(result);
    }
}