namespace School.Domain.Entities;

public class Student : BaseEntity
{
    public string? UserId { get; set; } // Link to AspNetUsers
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? QrCodeValue { get; set; }
    public string? ProfilePictureUrl { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Foreign Keys
    public int? ParentId { get; set; }
    public Parent? Parent { get; set; }
    
    public int? ClassRoomId { get; set; }
    public ClassRoom? ClassRoom { get; set; }
    
    // Navigation
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<ExamResult> ExamResults { get; set; } = new List<ExamResult>();
    public ICollection<GradeRecord> GradeRecords { get; set; } = new List<GradeRecord>();
    public ICollection<TuitionInvoice> TuitionInvoices { get; set; } = new List<TuitionInvoice>();
}
