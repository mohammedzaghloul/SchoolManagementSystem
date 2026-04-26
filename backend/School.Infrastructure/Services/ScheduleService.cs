using Microsoft.EntityFrameworkCore;
using School.Application.DTOs.Admin;
using School.Application.Interfaces;
using School.Domain.Entities;
using School.Infrastructure.Data;

namespace School.Infrastructure.Services;

public class ScheduleService : IScheduleService
{
    private readonly SchoolDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleService(SchoolDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<(Schedule Schedule, int GeneratedSessionsCount)> CreateAndGenerateAsync(
        CreateScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateScheduleRequestAsync(request, cancellationToken);

        var schedule = new Schedule
        {
            Title = request.Title.Trim(),
            TeacherId = request.TeacherId,
            SubjectId = request.SubjectId,
            ClassRoomId = request.ClassRoomId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            AttendanceType = request.AttendanceType.Trim(),
            TermStartDate = request.TermStartDate.Date,
            TermEndDate = request.TermEndDate.Date,
            IsActive = true
        };

        await _unitOfWork.Repository<Schedule>().AddAsync(schedule);
        await _unitOfWork.CompleteAsync();

        var generatedSessionsCount = await GenerateSessionsAsync(schedule, cancellationToken);

        await _context.Entry(schedule).Reference(item => item.Teacher).LoadAsync(cancellationToken);
        await _context.Entry(schedule).Reference(item => item.Subject).LoadAsync(cancellationToken);
        await _context.Entry(schedule).Reference(item => item.ClassRoom).LoadAsync(cancellationToken);

        return (schedule, generatedSessionsCount);
    }

    public async Task<int> GenerateSessionsAsync(Schedule schedule, CancellationToken cancellationToken = default)
    {
        var startDate = schedule.TermStartDate.Date;
        var endDate = schedule.TermEndDate.Date;

        if (endDate < startDate)
        {
            return 0;
        }

        var existingKeys = await _context.Sessions
            .Where(session => session.ClassRoomId == schedule.ClassRoomId)
            .Where(session => session.SubjectId == schedule.SubjectId)
            .Where(session => session.SessionDate >= startDate && session.SessionDate <= endDate)
            .Select(session => new { session.SessionDate, session.StartTime, session.EndTime })
            .ToListAsync(cancellationToken);

        var existingLookup = existingKeys.ToHashSetBy(
            item => $"{item.SessionDate:yyyyMMdd}:{item.StartTime:c}:{item.EndTime:c}");

        var sessionsToCreate = new List<Session>();

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            if (date.DayOfWeek != schedule.DayOfWeek)
            {
                continue;
            }

            var key = $"{date:yyyyMMdd}:{schedule.StartTime:c}:{schedule.EndTime:c}";
            if (existingLookup.Contains(key))
            {
                continue;
            }

            sessionsToCreate.Add(new Session
            {
                Title = schedule.Title,
                SessionDate = date,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime,
                AttendanceType = schedule.AttendanceType,
                TeacherId = schedule.TeacherId,
                SubjectId = schedule.SubjectId,
                ClassRoomId = schedule.ClassRoomId,
                ScheduleId = schedule.Id
            });

            existingLookup.Add(key);
        }

        if (sessionsToCreate.Count > 0)
        {
            await _context.Sessions.AddRangeAsync(sessionsToCreate, cancellationToken);
        }

        schedule.SessionsGeneratedUntil = endDate;
        await _unitOfWork.CompleteAsync();

        return sessionsToCreate.Count;
    }

    private async Task ValidateScheduleRequestAsync(CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        if (request.EndTime <= request.StartTime)
        {
            throw new InvalidOperationException("End time must be after start time.");
        }

        if (request.TermEndDate.Date < request.TermStartDate.Date)
        {
            throw new InvalidOperationException("Term end date must be after or equal to term start date.");
        }

        var teacherExists = await _context.Teachers.AnyAsync(teacher => teacher.Id == request.TeacherId, cancellationToken);
        if (!teacherExists)
        {
            throw new KeyNotFoundException("Teacher was not found.");
        }

        var classRoomExists = await _context.ClassRooms.AnyAsync(classRoom => classRoom.Id == request.ClassRoomId, cancellationToken);
        if (!classRoomExists)
        {
            throw new KeyNotFoundException("Classroom was not found.");
        }

        var subject = await _context.Subjects.FirstOrDefaultAsync(subject => subject.Id == request.SubjectId, cancellationToken)
            ?? throw new KeyNotFoundException("Subject was not found.");

        if (subject.TeacherId.HasValue && subject.TeacherId.Value != request.TeacherId)
        {
            throw new InvalidOperationException("The selected subject is already assigned to another teacher.");
        }

        if (subject.ClassRoomId.HasValue && subject.ClassRoomId.Value != request.ClassRoomId)
        {
            throw new InvalidOperationException("The selected subject is already assigned to another classroom.");
        }

        var hasDuplicateSchedule = await _context.Schedules.AnyAsync(schedule =>
            schedule.IsActive &&
            schedule.TeacherId == request.TeacherId &&
            schedule.SubjectId == request.SubjectId &&
            schedule.ClassRoomId == request.ClassRoomId &&
            schedule.DayOfWeek == request.DayOfWeek &&
            schedule.StartTime == request.StartTime &&
            schedule.EndTime == request.EndTime &&
            schedule.TermStartDate <= request.TermEndDate.Date &&
            request.TermStartDate.Date <= schedule.TermEndDate,
            cancellationToken);

        if (hasDuplicateSchedule)
        {
            throw new InvalidOperationException("يوجد جدول مطابق لنفس المدرس والمادة والفصل داخل نفس الفترة الدراسية.");
        }

        var hasTeacherOrClassOverlap = await _context.Schedules.AnyAsync(schedule =>
            schedule.IsActive &&
            schedule.DayOfWeek == request.DayOfWeek &&
            schedule.TermStartDate <= request.TermEndDate.Date &&
            request.TermStartDate.Date <= schedule.TermEndDate &&
            (schedule.TeacherId == request.TeacherId || schedule.ClassRoomId == request.ClassRoomId) &&
            schedule.StartTime < request.EndTime &&
            request.StartTime < schedule.EndTime,
            cancellationToken);

        if (hasTeacherOrClassOverlap)
        {
            throw new InvalidOperationException("يوجد تعارض في نفس اليوم والوقت مع جدول آخر للمدرس أو للفصل.");
        }
    }
}

internal static class HashSetExtensions
{
    public static HashSet<TValue> ToHashSetBy<TSource, TValue>(this IEnumerable<TSource> source, Func<TSource, TValue> selector)
        where TValue : notnull
    {
        return source.Select(selector).ToHashSet();
    }
}
