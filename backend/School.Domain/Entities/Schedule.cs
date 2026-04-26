namespace School.Domain.Entities;

public class Schedule : BaseEntity
{
    public string Title { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string AttendanceType { get; set; } = "Manual";
    public DateTime TermStartDate { get; set; }
    public DateTime TermEndDate { get; set; }
    public DateTime? SessionsGeneratedUntil { get; set; }
    public bool IsActive { get; set; } = true;

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int ClassRoomId { get; set; }
    public ClassRoom? ClassRoom { get; set; }

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
