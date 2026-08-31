namespace Sated.Data.Entities;

public class Consent
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public int DocumentId { get; set; }
    public ConsentDocument Document { get; set; } = null!;
    public DateTimeOffset GivenAt { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
}
