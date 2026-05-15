namespace RureSubProfiles.Models;

public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string UserName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Bio { get; set; }

    public string? AvatarPath { get; set; }
    public string? BannerPath { get; set; }

    public bool ShowFollowers { get; set; } = true;
    public bool IsVerified { get; set; } = false;

    public DateTime CreatedAt { get; set; }
}
