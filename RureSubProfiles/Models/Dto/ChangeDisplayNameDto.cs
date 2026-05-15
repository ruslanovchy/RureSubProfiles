using System.ComponentModel.DataAnnotations;

namespace RureSubProfiles.Models.Dto;

public class ChangeDisplayNameDto
{
    [Required]
    public Guid UserId { get; set; }
    [Required]
    [RegularExpression(ProfilesValidator.NAME_REGEX)]
    public string? NewName { get; set; }
}
