namespace School.Domain.Entities;

public class Exam : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int MaxScore { get; set; }
    public string ExamType { get; set; } = null!; // Quiz, Midterm, Final

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int ClassRoomId { get; set; }
    public ClassRoom? ClassRoom { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    
    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
}
