namespace RureSubProfiles.Models;

public class InboxMessage
{
    public Guid Id { get; set; }
    public string Topic { get; set; } = null!;
    public string? Content { get; set; }
    public DateTime ProcessedAt { get; set; }
}
