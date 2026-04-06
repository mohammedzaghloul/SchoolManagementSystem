namespace School.Domain.Entities;

public class Attendance : BaseEntity
{
    public bool IsPresent { get; set; }
    public string? Status { get; set; } // e.g., Present, Absent, Late
    public string? Notes { get; set; }
    public DateTime Time { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public string? Method { get; set; } // QR, Face, Manual

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int SessionId { get; set; }
    public Session? Session { get; set; }
}
