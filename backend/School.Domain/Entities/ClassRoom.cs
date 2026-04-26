namespace School.Domain.Entities;

public class ClassRoom : BaseEntity
{
    public string? Name { get; set; }
    public int Capacity { get; set; }
    public string? Location { get; set; }
    public string? AcademicYear { get; set; }
    
    public int? GradeLevelId { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    
    // Homeroom teacher
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
}
