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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Session> _sessionRepo;
    private readonly IRepository<Student> _studentRepo;
    private readonly IRepository<School.Domain.Entities.Attendance> _attendanceRepo;

    public ScanQrCommandHandler(
        IQrCodeService qrCodeService,
        ICacheService cacheService,
        IUnitOfWork unitOfWork,
        IRepository<Session> sessionRepo,
        IRepository<Student> studentRepo,
        IRepository<School.Domain.Entities.Attendance> attendanceRepo)
    {
        _qrCodeService = qrCodeService;
        _cacheService = cacheService;
        _unitOfWork = unitOfWork;
        _sessionRepo = sessionRepo;
        _studentRepo = studentRepo;
        _attendanceRepo = attendanceRepo;
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

        var attendanceSpec = new BaseSpecification<School.Domain.Entities.Attendance>(
            attendance => attendance.SessionId == request.SessionId && attendance.StudentId == request.StudentId);
        var existingAttendance = await _attendanceRepo.GetEntityWithSpec(attendanceSpec);

        if (existingAttendance == null)
        {
            existingAttendance = new School.Domain.Entities.Attendance
            {
                SessionId = request.SessionId,
                StudentId = request.StudentId
            };

            await _attendanceRepo.AddAsync(existingAttendance);
        }

        existingAttendance.IsPresent = true;
        existingAttendance.Status = "Present";
        existingAttendance.Method = "QR";
        existingAttendance.RecordedAt = DateTime.UtcNow;
        existingAttendance.Time = DateTime.UtcNow;
        existingAttendance.Notes = string.Empty;

        // 4. Save back to Cache
        await _cacheService.SetAsync(attendanceKey, attendanceRecords, TimeSpan.FromHours(4));
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
