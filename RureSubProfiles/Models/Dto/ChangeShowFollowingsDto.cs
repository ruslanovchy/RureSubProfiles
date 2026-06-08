using System.ComponentModel.DataAnnotations;

namespace RureSubProfiles.Models.Dto;

public class ChangeShowFollowingsDto
{
    [Required]
    public Guid UserId { get; set; }
    [Required]
    public bool Value { get; set; }
}
