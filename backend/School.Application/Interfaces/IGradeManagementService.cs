using School.Application.DTOs.GradeManagement;

namespace School.Application.Interfaces;

public interface IGradeManagementService
{
    Task<PublishGradeSessionsResultDto> PublishSessionsAsync(
        PublishGradeSessionsRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminGradeSessionsDashboardDto> GetAdminDashboardAsync(
        string? type,
        DateTime? date,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeacherGradeSessionOptionDto>> GetTeacherSessionsAsync(
        int teacherId,
        CancellationToken cancellationToken = default);

    Task<TeacherSessionGradebookDto?> GetTeacherGradebookAsync(
        int sessionId,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<GradeOperationResultDto> SaveTeacherGradesAsync(
        SaveTeacherSessionGradesRequest request,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<GradeOperationResultDto> ApproveTeacherUploadAsync(
        int sessionId,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default);
}
