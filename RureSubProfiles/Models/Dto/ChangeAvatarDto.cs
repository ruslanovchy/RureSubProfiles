using System.ComponentModel.DataAnnotations;

namespace RureSubProfiles.Models.Dto;

public class ChangeAvatarDto
{
    [Required]
    public Guid UserId { get; set; }
    [Required]
    public IFormFile? NewAvatar { get; set; }
}
