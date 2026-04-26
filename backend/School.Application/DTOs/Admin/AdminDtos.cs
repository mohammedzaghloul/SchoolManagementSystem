using System.ComponentModel.DataAnnotations;

namespace School.Application.DTOs.Admin;

public class CreateAdminRequest
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public class CreateTeacherRequest
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = "Teacher@123";
}

public class CreateStudentRequest
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [MaxLength(20)]
    public string? Phone { get; set; }

    public DateTime? BirthDate { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int ClassRoomId { get; set; }

    public int? ParentId { get; set; }

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = "Student@123";
}

public class AssignRoleRequest
{
    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = string.Empty;
}

public class AssignStudentSubjectsRequest
{
    [Required]
    [MinLength(1)]
    public List<int> SubjectIds { get; set; } = [];
}

public class CreateScheduleRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int TeacherId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int SubjectId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int ClassRoomId { get; set; }

    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    [MaxLength(32)]
    public string AttendanceType { get; set; } = "Manual";

    [Required]
    public DateTime TermStartDate { get; set; }

    [Required]
    public DateTime TermEndDate { get; set; }
}

public class UserSummaryDto
{
    public int? DomainEntityId { get; set; }
    public string IdentityUserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class ScheduleDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string ClassRoomName { get; set; } = string.Empty;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string AttendanceType { get; set; } = string.Empty;
    public DateTime TermStartDate { get; set; }
    public DateTime TermEndDate { get; set; }
    public int GeneratedSessionsCount { get; set; }
}
