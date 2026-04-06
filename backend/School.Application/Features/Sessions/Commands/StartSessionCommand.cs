using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Sessions.Commands;

public class StartSessionCommand : IRequest<int>
{
    public string Title { get; set; }
    public int ClassRoomId { get; set; }
    public int SubjectId { get; set; }
    public int TeacherId { get; set; }
    public string AttendanceType { get; set; } = "QR"; // QR, Face, Manual
}

public class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public StartSessionCommandHandler(IUnitOfWork unitOfWork, ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<int> Handle(StartSessionCommand request, CancellationToken cancellationToken)
    {
        var session = new Session
        {
            Title = request.Title,
            SessionDate = DateTime.UtcNow.Date,
            StartTime = DateTime.UtcNow.TimeOfDay,
            EndTime = DateTime.UtcNow.AddMinutes(90).TimeOfDay,
            AttendanceType = request.AttendanceType,
            ClassRoomId = request.ClassRoomId,
            SubjectId = request.SubjectId,
            TeacherId = request.TeacherId
        };

        await _unitOfWork.Repository<Session>().AddAsync(session);
        await _unitOfWork.CompleteAsync();

        // Get students in the classroom
        var spec = new Specifications.BaseSpecification<Student>(x => x.ClassRoomId == request.ClassRoomId);
        var students = await _unitOfWork.Repository<Student>().ListAsync(spec);

        // Pre-load students into Redis as Absent
        var attendanceKey = $"session:{session.Id}:attendance";
        
        var attendanceRecords = students.Select(s => new SessionAttendanceDto 
        { 
            StudentId = s.Id, 
            IsPresent = false, 
            Time = null 
        }).ToList();
        
        await _cacheService.SetAsync(attendanceKey, attendanceRecords, TimeSpan.FromHours(4));

        return session.Id;
    }
}

public class SessionAttendanceDto
{
    public int StudentId { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? Time { get; set; }
}
