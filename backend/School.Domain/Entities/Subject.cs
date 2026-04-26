namespace School.Domain.Entities;

public class Subject : BaseEntity
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Term { get; set; }
    public bool IsActive { get; set; } = true;

    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    
    public int? ClassRoomId { get; set; }
    public ClassRoom? ClassRoom { get; set; }
    
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    public ICollection<StudentSubject> StudentSubjects { get; set; } = new List<StudentSubject>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
