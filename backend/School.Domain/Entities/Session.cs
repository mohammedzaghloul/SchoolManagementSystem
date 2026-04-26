namespace School.Domain.Entities;

public class Session : BaseEntity
{
    public string? Title { get; set; }
    public DateTime SessionDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string? AttendanceType { get; set; } // QR, Face, Manual

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    
    public int ClassRoomId { get; set; }
    public ClassRoom? ClassRoom { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int? ScheduleId { get; set; }
    public Schedule? Schedule { get; set; }
    
    public bool IsLive { get; set; }
    public string? AgoraChannelName { get; set; }

    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
}
