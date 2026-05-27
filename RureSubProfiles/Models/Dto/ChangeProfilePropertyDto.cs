namespace RureSubProfiles.Models.Dto;

public class ChangeProfilePropertyDto
{
    public Guid ProfileId { get; set; }
    public Guid UserId { get; set; }

    public string? PropertyName { get; set; }
    public string? Value { get; set; }
}
