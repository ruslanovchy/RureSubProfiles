namespace RureSubProfiles.Models;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Topic { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime OccuredAt{ get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
}
