namespace School.Domain.Entities;

public class Grade : BaseEntity
{
    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int SessionId { get; set; }
    public GradeSession? Session { get; set; }

    public double Score { get; set; }
    public double MaxScore { get; set; }
}
