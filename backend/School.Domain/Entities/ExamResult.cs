namespace School.Domain.Entities;

public class ExamResult : BaseEntity
{
    public double Score { get; set; }
    public string? Notes { get; set; }

    public int ExamId { get; set; }
    public Exam? Exam { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
