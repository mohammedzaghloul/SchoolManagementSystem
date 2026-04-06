namespace School.Domain.Entities;

public class Teacher : BaseEntity
{
    public string? UserId { get; set; } // Link to AspNetUsers
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ClassRoom> ClassRooms { get; set; } = new List<ClassRoom>();
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
