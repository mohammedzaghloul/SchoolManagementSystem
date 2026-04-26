using System.ComponentModel.DataAnnotations;

namespace School.Application.DTOs.Teacher;

public class RecordGradeRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int SubjectId { get; set; }

    [Required]
    [Range(0, 100)]
    public double Score { get; set; }

    [Required]
    [MaxLength(50)]
    public string GradeType { get; set; } = "Quiz";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime? RecordedOnUtc { get; set; }
}

public class RecordAttendanceRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int SessionId { get; set; }

    [Required]
    [MinLength(1)]
    public List<RecordAttendanceStudentDto> Students { get; set; } = [];
}

public class RecordAttendanceStudentDto
{
    [Required]
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    public bool IsPresent { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Present";

    [MaxLength(32)]
    public string Method { get; set; } = "Manual";

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class OperationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
}
