namespace RureSubProfiles.Models.Dto;

public class ProfileResponseDto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    public string UserName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }

    public bool ShowFollowers { get; set; } = true;
    public bool IsVerified { get; set; } = false;
    public bool IsFollowed { get; set; }

    public int FollowersCount { get; set; }
    public int FollowingsCount { get; set; }
    public int PostsCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
