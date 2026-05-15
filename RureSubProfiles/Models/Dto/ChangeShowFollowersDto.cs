using System.ComponentModel.DataAnnotations;

namespace RureSubProfiles.Models.Dto;

public class ChangeShowFollowersDto
{
    [Required]
    public Guid UserId { get; set; }
    [Required]
    public bool Value { get; set; }
}
