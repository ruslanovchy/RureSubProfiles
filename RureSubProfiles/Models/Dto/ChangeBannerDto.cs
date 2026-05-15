using System.ComponentModel.DataAnnotations;

namespace RureSubProfiles.Models.Dto;

public class ChangeBannerDto
{
    [Required]
    public Guid UserId { get; set; }
    [Required]
    public IFormFile? NewBanner { get; set; }
}
