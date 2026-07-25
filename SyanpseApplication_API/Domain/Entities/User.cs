namespace Domain.Entities;

public class User
{
    /// <summary>Matches the NameIdentifier claim issued by the identity provider.</summary>
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public ICollection<Analysis> Analyses { get; set; } = new List<Analysis>();
}
