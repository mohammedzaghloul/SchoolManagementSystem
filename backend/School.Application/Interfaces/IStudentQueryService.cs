using School.Application.DTOs.Students;

namespace School.Application.Interfaces;

public interface IStudentQueryService
{
    Task<IReadOnlyList<StudentSearchResultDto>> SearchStudentsAsync(
        string? query,
        int limit,
        int? parentId = null,
        CancellationToken cancellationToken = default);

    Task<StudentDashboardDto?> GetStudentDashboardAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentGradeDto>?> GetStudentGradesAsync(
        int studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentAttendanceDto>?> GetStudentAttendanceAsync(
        int studentId,
        CancellationToken cancellationToken = default);
}
