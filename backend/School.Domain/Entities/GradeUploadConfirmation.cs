namespace School.Domain.Entities;

public class GradeUploadConfirmation : BaseEntity
{
    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public string GradeType { get; set; } = null!;
    public DateTime Date { get; set; }
    public bool IsConfirmed { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
