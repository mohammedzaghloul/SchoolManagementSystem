namespace School.Domain.Entities;

public class Announcement : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string Audience { get; set; } = null!; // e.g., All, Teachers, Students, Parents
    public string CreatedBy { get; set; } = null!; // ApplicationUserId
}
