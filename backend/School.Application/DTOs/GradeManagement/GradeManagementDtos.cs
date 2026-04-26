namespace School.Application.DTOs.GradeManagement;

public sealed class PublishGradeSessionsRequest
{
    public string Scope { get; set; } = "Class";
    public int? ClassId { get; set; }
    public int? GradeLevelId { get; set; }
    public int SubjectId { get; set; }
    public string Type { get; set; } = "Homework";
    public DateTime Date { get; set; }
    public DateTime? Deadline { get; set; }
}

public sealed class PublishGradeSessionsResultDto
{
    public int CreatedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int SkippedCount { get; set; }
    public List<PublishedGradeSessionDto> CreatedSessions { get; set; } = [];
    public List<string> SkippedClasses { get; set; } = [];
}

public sealed class PublishedGradeSessionDto
{
    public int SessionId { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime? Deadline { get; set; }
}

public sealed class AdminGradeSessionsDashboardDto
{
    public string GlobalStatus { get; set; } = "In Progress";
    public int TotalSessions { get; set; }
    public int ApprovedSessions { get; set; }
    public int InProgressSessions { get; set; }
    public List<GradeSessionMonitorDto> Sessions { get; set; } = [];
}

public sealed class GradeSessionMonitorDto
{
    public int SessionId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string? GradeLevelName { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime? Deadline { get; set; }
    public int TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int GradedStudents { get; set; }
    public int MissingGradesCount { get; set; }
    public int ProgressPercent { get; set; }
    public string Status { get; set; } = "NotStarted";
}

public sealed class TeacherGradeSessionOptionDto
{
    public int SessionId { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string? GradeLevelName { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime? Deadline { get; set; }
    public string Status { get; set; } = "NotStarted";
    public int ProgressPercent { get; set; }
}

public sealed class TeacherSessionGradebookDto
{
    public int SessionId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime? Deadline { get; set; }
    public bool IsApproved { get; set; }
    public bool IsDeadlinePassed { get; set; }
    public bool IsLocked { get; set; }
    public string Status { get; set; } = "NotStarted";
    public int TotalStudents { get; set; }
    public int GradedStudents { get; set; }
    public int MissingGradesCount { get; set; }
    public int ProgressPercent { get; set; }
    public List<TeacherSessionGradeStudentDto> Students { get; set; } = [];
}

public sealed class TeacherSessionGradeStudentDto
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int? GradeId { get; set; }
    public double? Score { get; set; }
    public double MaxScore { get; set; } = 100;
    public double? Percentage { get; set; }
    public bool IsGraded { get; set; }
}

public sealed class SaveTeacherSessionGradesRequest
{
    public int SessionId { get; set; }
    public List<SaveTeacherSessionGradeItem> Grades { get; set; } = [];
}

public sealed class SaveTeacherSessionGradeItem
{
    public int StudentId { get; set; }
    public double? Score { get; set; }
    public double MaxScore { get; set; } = 100;
}

public sealed class GradeOperationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalStudents { get; set; }
    public int GradedStudents { get; set; }
    public int MissingGradesCount { get; set; }
    public string Status { get; set; } = "NotStarted";
}
