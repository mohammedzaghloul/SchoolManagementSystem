namespace School.Domain.Entities;

public class GradeSession : BaseEntity
{
    public int ClassId { get; set; }
    public ClassRoom? ClassRoom { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public string Type { get; set; } = null!;
    public DateTime Date { get; set; }
    public DateTime? Deadline { get; set; }

    public ICollection<Grade> Grades { get; set; } = new List<Grade>();
    public ICollection<GradeUpload> Uploads { get; set; } = new List<GradeUpload>();
}
