using System.ComponentModel.DataAnnotations;

namespace RureSubProfiles.Models.Dto;

public class ChangeBioDto
{
    [Required]
    public Guid UserId { get; set; }
    [Required]
    [RegularExpression(ProfilesValidator.BIO_REGEX)]
    public string? NewBio { get; set; }
}
