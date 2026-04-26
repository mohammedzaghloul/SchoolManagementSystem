using School.Application.DTOs.Teacher;

namespace School.Application.Interfaces;

public interface ITeacherWorkflowService
{
    Task<OperationResultDto> RecordGradeAsync(string teacherIdentityUserId, RecordGradeRequest request, bool isAdmin, CancellationToken cancellationToken = default);
    Task<OperationResultDto> RecordAttendanceAsync(string teacherIdentityUserId, RecordAttendanceRequest request, bool isAdmin, CancellationToken cancellationToken = default);
}
