using School.Domain.Entities;
using System.Linq;

namespace School.Infrastructure.Data;

public static class SessionScheduleGenerator
{
    private static readonly DayOfWeek[] SchoolDays =
    [
        DayOfWeek.Sunday,
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday
    ];

    private static readonly (TimeSpan Start, TimeSpan End, string AttendanceType)[] DailySlots =
    [
        (new TimeSpan(8, 0, 0), new TimeSpan(8, 45, 0), "QR"),
        (new TimeSpan(9, 0, 0), new TimeSpan(9, 45, 0), "Manual"),
        (new TimeSpan(10, 0, 0), new TimeSpan(10, 45, 0), "Face")
    ];

    public static List<Session> BuildMissingSessions(
        IEnumerable<Subject> subjects,
        IEnumerable<Session> existingSessions,
        DateTime startDate,
        DateTime endDate)
    {
        var normalizedStart = startDate.Date;
        var normalizedEnd = endDate.Date;

        if (normalizedEnd < normalizedStart)
        {
            return [];
        }

        var scheduledSubjects = subjects
            .Where(subject => subject.IsActive && subject.TeacherId.HasValue && subject.ClassRoomId.HasValue)
            .OrderBy(subject => subject.ClassRoomId)
            .ThenBy(subject => subject.Term)
            .ThenBy(subject => subject.Name)
            .ToList();

        if (scheduledSubjects.Count == 0)
        {
            return [];
        }

        var existingKeys = new HashSet<string>(
            existingSessions.Select(session => BuildKey(session.ClassRoomId, session.SessionDate, session.StartTime, session.EndTime))
        );
        var existingTeacherSlotKeys = new HashSet<string>(
            existingSessions.Select(session => BuildTeacherSlotKey(session.TeacherId, session.SessionDate, session.StartTime, session.EndTime))
        );

        var generated = new List<Session>();

        foreach (var classGroup in scheduledSubjects.GroupBy(subject => subject.ClassRoomId!.Value))
        {
            var classSubjects = classGroup.ToList();
            if (classSubjects.Count == 0)
            {
                continue;
            }

            for (var date = normalizedStart; date <= normalizedEnd; date = date.AddDays(1))
            {
                if (!SchoolDays.Contains(date.DayOfWeek))
                {
                    continue;
                }

                var schoolDayIndex = Array.IndexOf(SchoolDays, date.DayOfWeek);
                var weekIndex = Math.Max((date.Date - normalizedStart).Days / 7, 0);

                for (var slotIndex = 0; slotIndex < DailySlots.Length; slotIndex++)
                {
                    var slot = DailySlots[slotIndex];
                    var subjectIndex = (schoolDayIndex + weekIndex + slotIndex) % classSubjects.Count;
                    var subject = classSubjects[subjectIndex];
                    var key = BuildKey(classGroup.Key, date, slot.Start, slot.End);
                    var teacherSlotKey = BuildTeacherSlotKey(subject.TeacherId!.Value, date, slot.Start, slot.End);

                    if (existingKeys.Contains(key) || existingTeacherSlotKeys.Contains(teacherSlotKey))
                    {
                        continue;
                    }

                    existingKeys.Add(key);
                    existingTeacherSlotKeys.Add(teacherSlotKey);

                    generated.Add(new Session
                    {
                        Title = $"حصة {subject.Name}",
                        SessionDate = date,
                        StartTime = slot.Start,
                        EndTime = slot.End,
                        AttendanceType = slot.AttendanceType,
                        TeacherId = subject.TeacherId!.Value,
                        ClassRoomId = subject.ClassRoomId!.Value,
                        SubjectId = subject.Id
                    });
                }
            }
        }

        return generated;
    }

    public static int CountPlannedSlots(IEnumerable<Subject> subjects, DateTime startDate, DateTime endDate)
    {
        var normalizedStart = startDate.Date;
        var normalizedEnd = endDate.Date;
        if (normalizedEnd < normalizedStart)
        {
            return 0;
        }

        var classesCount = subjects
            .Where(subject => subject.IsActive && subject.TeacherId.HasValue && subject.ClassRoomId.HasValue)
            .Select(subject => subject.ClassRoomId!.Value)
            .Distinct()
            .Count();

        if (classesCount == 0)
        {
            return 0;
        }

        var schoolDaysInRange = 0;
        for (var date = normalizedStart; date <= normalizedEnd; date = date.AddDays(1))
        {
            if (SchoolDays.Contains(date.DayOfWeek))
            {
                schoolDaysInRange++;
            }
        }

        return classesCount * schoolDaysInRange * DailySlots.Length;
    }

    private static string BuildKey(int classRoomId, DateTime sessionDate, TimeSpan startTime, TimeSpan endTime)
    {
        return $"{classRoomId}:{sessionDate:yyyyMMdd}:{startTime:c}:{endTime:c}";
    }

    private static string BuildTeacherSlotKey(int teacherId, DateTime sessionDate, TimeSpan startTime, TimeSpan endTime)
    {
        return $"{teacherId}:{sessionDate:yyyyMMdd}:{startTime:c}:{endTime:c}";
    }
}
