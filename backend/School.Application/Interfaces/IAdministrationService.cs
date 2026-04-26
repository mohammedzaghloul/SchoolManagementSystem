using School.Application.DTOs.Admin;
using School.Application.DTOs.Teacher;

namespace School.Application.Interfaces;

public interface IAdministrationService
{
    Task<UserSummaryDto> CreateAdminAsync(CreateAdminRequest request, CancellationToken cancellationToken = default);
    Task<UserSummaryDto> CreateTeacherAsync(CreateTeacherRequest request, CancellationToken cancellationToken = default);
    Task<UserSummaryDto> CreateStudentAsync(CreateStudentRequest request, CancellationToken cancellationToken = default);
    Task<UserSummaryDto> AssignRoleAsync(string userId, AssignRoleRequest request, CancellationToken cancellationToken = default);
    Task<OperationResultDto> AssignStudentSubjectsAsync(int studentId, AssignStudentSubjectsRequest request, CancellationToken cancellationToken = default);
    Task<ScheduleDto> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default);
}
