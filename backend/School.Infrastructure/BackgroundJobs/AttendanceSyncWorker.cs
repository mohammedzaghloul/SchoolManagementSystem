using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using School.Application.Interfaces;
using School.Domain.Entities;
using School.Application.Features.Sessions.Commands;

namespace School.Infrastructure.BackgroundJobs;

public class AttendanceSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AttendanceSyncWorker> _logger;

    public AttendanceSyncWorker(IServiceProvider serviceProvider, ILogger<AttendanceSyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Running Attendance Sync at: {time}", DateTimeOffset.Now);

            try
            {
                if (stoppingToken.IsCancellationRequested) return;

                using var scope = _serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                // Sync sessions from today
                var today = DateTime.UtcNow.Date;
                var sessionSpec = new Application.Specifications.BaseSpecification<Session>(s => s.SessionDate == today);
                var activeSessions = await unitOfWork.Repository<Session>().ListAsync(sessionSpec);

                foreach (var session in activeSessions)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    var attendanceKey = $"session:{session.Id}:attendance";
                    var records = await cacheService.GetAsync<List<SessionAttendanceDto>>(attendanceKey);

                    if (records != null && records.Any())
                    {
                        var attendanceRepo = unitOfWork.Repository<Attendance>();
                        
                        foreach (var record in records)
                        {
                            if (stoppingToken.IsCancellationRequested) break;

                            // Check if attendance already logged in DB
                            var existingSpec = new Application.Specifications.BaseSpecification<Attendance>(
                                a => a.SessionId == session.Id && a.StudentId == record.StudentId);
                            var existing = await attendanceRepo.GetEntityWithSpec(existingSpec);

                            if (existing == null)
                            {
                                await attendanceRepo.AddAsync(new Attendance
                                {
                                    SessionId = session.Id,
                                    StudentId = record.StudentId,
                                    IsPresent = record.IsPresent,
                                    Time = record.Time ?? DateTime.UtcNow,
                                    Status = record.IsPresent ? "Present" : "Absent",
                                    Notes = "Synced from cache"
                                });
                            }
                            else
                            {
                                existing.IsPresent = record.IsPresent;
                                existing.Time = record.Time ?? DateTime.UtcNow;
                                existing.Status = record.IsPresent ? "Present" : "Absent";
                                attendanceRepo.Update(existing);
                            }
                        }
                    }
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await unitOfWork.CompleteAsync();
                }
            }
            catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred executing Attendance Sync.");
            }

            // Run every 15 minutes unless cancelled
            if (!stoppingToken.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
                catch (OperationCanceledException) { /* Normal shutdown */ }
            }
        }
    }
}
