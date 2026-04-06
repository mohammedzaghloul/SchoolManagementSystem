using MediatR;
using School.Application.Interfaces;
using School.Application.Specifications;
using School.Application.Features.Sessions.Commands;
using School.Domain.Entities;

namespace School.Application.Features.Attendance.Commands;

public class ScanQrCommand : IRequest<bool>
{
    public string QrToken { get; set; }
    public int SessionId { get; set; }
    public int StudentId { get; set; }
}

public class ScanQrCommandHandler : IRequestHandler<ScanQrCommand, bool>
{
    private readonly IQrCodeService _qrCodeService;
    private readonly ICacheService _cacheService;
    private readonly IRepository<Session> _sessionRepo;
    private readonly IRepository<Student> _studentRepo;

    public ScanQrCommandHandler(IQrCodeService qrCodeService, ICacheService cacheService, IRepository<Session> sessionRepo, IRepository<Student> studentRepo)
    {
        _qrCodeService = qrCodeService;
        _cacheService = cacheService;
        _sessionRepo = sessionRepo;
        _studentRepo = studentRepo;
    }

    public async Task<bool> Handle(ScanQrCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate the QR token (checks signature and 30-second expiry)
        var isValid = _qrCodeService.ValidateQrToken(request.QrToken, request.SessionId);
        if (!isValid) return false;

        // 2. Get attendance from Cache
        var attendanceKey = $"session:{request.SessionId}:attendance";
        var attendanceRecords = await _cacheService.GetAsync<List<SessionAttendanceDto>>(attendanceKey);

        // 3. Fallback: If cache is empty, try to rebuild it from DB
        if (attendanceRecords == null)
        {
            var session = await _sessionRepo.GetByIdAsync(request.SessionId);
            if (session == null) return false;

            var spec = new BaseSpecification<Student>(s => s.ClassRoomId == session.ClassRoomId);
            var students = await _studentRepo.ListAsync(spec);
            attendanceRecords = students.Select(s => new SessionAttendanceDto 
            { 
                StudentId = s.Id, 
                IsPresent = false 
            }).ToList();
        }

        var studentRecord = attendanceRecords.FirstOrDefault(x => x.StudentId == request.StudentId);
        if (studentRecord == null) return false; // Student not in this class

        studentRecord.IsPresent = true;
        studentRecord.Time = DateTime.UtcNow;

        // 4. Save back to Cache
        await _cacheService.SetAsync(attendanceKey, attendanceRecords, TimeSpan.FromHours(4));

        return true;
    }
}
