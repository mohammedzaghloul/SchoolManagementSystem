namespace School.Application.DTOs.Students;

public sealed class StudentSearchResultDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ClassRoomName { get; set; }
    public string? GradeLevelName { get; set; }
    public string? Code { get; set; }
    public bool IsActive { get; set; }
}

public sealed class StudentDashboardDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? ParentName { get; set; }
    public string? ClassRoomName { get; set; }
    public string? GradeLevelName { get; set; }
    public string? AcademicYear { get; set; }
    public string? Code { get; set; }
    public double Avg { get; set; }
    public double Attendance { get; set; }
    public double Activity { get; set; }
    public bool GradesCompleted { get; set; }
    public int TotalSubjects { get; set; }
    public int ApprovedSubjects { get; set; }
    public string GradesStatus { get; set; } = "IN_PROGRESS";
    public string GradesStatusLabel { get; set; } = "جاري رفع الدرجات";
    public StudentAttendanceDto? LastAttendance { get; set; }
    public List<StudentGradeDto> Grades { get; set; } = [];
    public List<StudentAttendanceDto> AttendanceRecords { get; set; } = [];
    public List<StudentAssignmentDto> Assignments { get; set; } = [];
    public List<StudentAlertDto> Alerts { get; set; } = [];
}

public sealed class StudentGradeDto
{
    public int Id { get; set; }
    public int? SessionId { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string? TeacherName { get; set; }
    public string GradeType { get; set; } = string.Empty;
    public double Score { get; set; }
    public double? RawScore { get; set; }
    public double? MaxScore { get; set; }
    public double Percentage { get; set; }
    public bool IsGraded { get; set; } = true;
    public DateTime Date { get; set; }
    public DateTime? Deadline { get; set; }
    public string? Notes { get; set; }
    public bool IsApproved { get; set; }
    public string ApprovalStatus { get; set; } = "IN_PROGRESS";
}

public sealed class StudentAttendanceDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsPresent { get; set; }
    public string? Method { get; set; }
    public string? SessionTitle { get; set; }
    public string? SubjectName { get; set; }
    public DateTime RecordedAt { get; set; }
}

public sealed class StudentAssignmentDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? SubjectName { get; set; }
    public string? TeacherName { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsLate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public double? Grade { get; set; }
    public string? TeacherFeedback { get; set; }
}

public sealed class StudentAlertDto
{
    public string Type { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
