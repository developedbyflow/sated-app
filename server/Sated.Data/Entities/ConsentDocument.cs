namespace Sated.Data.Entities;

public class ConsentDocument
{
    public int Id { get; set; }
    public ConsentPurpose Purpose { get; set; }
    public required string Version { get; set; }
    public required string Text { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}
