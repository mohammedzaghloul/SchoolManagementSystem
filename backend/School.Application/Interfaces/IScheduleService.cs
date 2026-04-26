using School.Application.DTOs.Admin;
using School.Domain.Entities;

namespace School.Application.Interfaces;

public interface IScheduleService
{
    Task<(Schedule Schedule, int GeneratedSessionsCount)> CreateAndGenerateAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default);
    Task<int> GenerateSessionsAsync(Schedule schedule, CancellationToken cancellationToken = default);
}
