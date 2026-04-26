namespace School.Domain.Entities;

public static class GradeUploadStatuses
{
    public const string NotStarted = "NotStarted";
    public const string InProgress = "InProgress";
    public const string Approved = "Approved";
}

public class GradeUpload : BaseEntity
{
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int SessionId { get; set; }
    public GradeSession? Session { get; set; }

    public string Status { get; set; } = GradeUploadStatuses.NotStarted;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
}
