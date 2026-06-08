namespace RureSubProfiles.Models;

public class Profile
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public long RedisId { get; set; }
    public Guid UserId { get; set; }

    public string UserName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Bio { get; set; }

    public string? AvatarPath { get; set; }
    public string? BannerPath { get; set; }

    public bool ShowFollowers { get; set; } = true;
    public bool ShowFollowings { get; set; } = true;
    public bool IsVerified { get; set; } = false;

    public int FollowersCount { get; set; }
    public int FollowingsCount { get; set; }
    public int PostsCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
