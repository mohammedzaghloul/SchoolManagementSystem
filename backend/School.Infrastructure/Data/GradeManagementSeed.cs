using Microsoft.EntityFrameworkCore;
using School.Domain.Entities;

namespace School.Infrastructure.Data;

public static class GradeManagementSeed
{
    private const double DefaultMaxScore = 100;

    private static readonly TeacherSeed[] TeacherSeeds =
    [
        new("Ahmed", "ahmed.grade-seed@school.com", "01010001001", "Math"),
        new("Sara", "sara.grade-seed@school.com", "01010001002", "Arabic"),
        new("Mahmoud", "mahmoud.grade-seed@school.com", "01010001003", "Science")
    ];

    private static readonly GradeLevelSeed[] GradeLevelSeeds =
    [
        new("First Grade", ["1/A", "1/B"]),
        new("Second Grade", ["2/A", "2/B"])
    ];

    private static readonly SubjectSeed[] SubjectSeeds =
    [
        new("Math", "MATH", "Ahmed"),
        new("Arabic", "ARB", "Sara"),
        new("English", "ENG", "Sara"),
        new("Science", "SCI", "Mahmoud")
    ];

    public static async Task SeedAsync(SchoolDbContext context, CancellationToken cancellationToken = default)
    {
        var hasGradeManagementData = await context.GradeSessions.AnyAsync(cancellationToken)
            || await context.Grades.AnyAsync(cancellationToken)
            || await context.GradeUploads.AnyAsync(cancellationToken);

        if (hasGradeManagementData)
        {
            return;
        }

        var teachers = await EnsureTeachersAsync(context, cancellationToken);
        var gradeLevels = await EnsureGradeLevelsAsync(context, cancellationToken);
        var classRooms = await EnsureClassRoomsAsync(context, gradeLevels, teachers, cancellationToken);
        var subjects = await EnsureSubjectsAsync(context, classRooms, teachers, cancellationToken);
        var studentsByClassId = await EnsureStudentsAsync(context, classRooms, cancellationToken);
        var sessions = await EnsureGradeSessionsAsync(context, classRooms, subjects, cancellationToken);

        await EnsureGradesAndUploadsAsync(context, sessions, studentsByClassId, cancellationToken);
    }

    private static async Task<Dictionary<string, Teacher>> EnsureTeachersAsync(
        SchoolDbContext context,
        CancellationToken cancellationToken)
    {
        var teachers = await context.Teachers.ToListAsync(cancellationToken);
        var teachersByName = teachers
            .Where(teacher => !string.IsNullOrWhiteSpace(teacher.FullName))
            .GroupBy(teacher => Normalize(teacher.FullName!))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var seed in TeacherSeeds)
        {
            if (teachersByName.ContainsKey(Normalize(seed.Name)))
            {
                continue;
            }

            var teacher = new Teacher
            {
                FullName = seed.Name,
                Email = seed.Email,
                Phone = seed.Phone,
                IsActive = true
            };

            context.Teachers.Add(teacher);
            teachersByName[Normalize(seed.Name)] = teacher;
        }

        await context.SaveChangesAsync(cancellationToken);

        return TeacherSeeds.ToDictionary(
            seed => seed.Name,
            seed => teachersByName[Normalize(seed.Name)],
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, GradeLevel>> EnsureGradeLevelsAsync(
        SchoolDbContext context,
        CancellationToken cancellationToken)
    {
        var gradeLevels = await context.GradeLevels.ToListAsync(cancellationToken);
        var gradeLevelsByName = gradeLevels
            .GroupBy(gradeLevel => Normalize(gradeLevel.Name))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var seed in GradeLevelSeeds)
        {
            if (gradeLevelsByName.ContainsKey(Normalize(seed.Name)))
            {
                continue;
            }

            var gradeLevel = new GradeLevel
            {
                Name = seed.Name,
                Description = $"{seed.Name} grade management seed"
            };

            context.GradeLevels.Add(gradeLevel);
            gradeLevelsByName[Normalize(seed.Name)] = gradeLevel;
        }

        await context.SaveChangesAsync(cancellationToken);

        return GradeLevelSeeds.ToDictionary(
            seed => seed.Name,
            seed => gradeLevelsByName[Normalize(seed.Name)],
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<List<ClassRoom>> EnsureClassRoomsAsync(
        SchoolDbContext context,
        IReadOnlyDictionary<string, GradeLevel> gradeLevels,
        IReadOnlyDictionary<string, Teacher> teachers,
        CancellationToken cancellationToken)
    {
        var classRooms = await context.ClassRooms.ToListAsync(cancellationToken);
        var classRoomsByName = classRooms
            .Where(classRoom => !string.IsNullOrWhiteSpace(classRoom.Name))
            .GroupBy(classRoom => Normalize(classRoom.Name!))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var homeroomTeacher = teachers["Ahmed"];
        var academicYear = GetAcademicYear(DateTime.Today);

        foreach (var gradeLevelSeed in GradeLevelSeeds)
        {
            foreach (var className in gradeLevelSeed.ClassNames)
            {
                if (classRoomsByName.ContainsKey(Normalize(className)))
                {
                    continue;
                }

                var classRoom = new ClassRoom
                {
                    Name = className,
                    Capacity = 30,
                    Location = $"Room {className.Replace("/", string.Empty, StringComparison.Ordinal)}",
                    AcademicYear = academicYear,
                    GradeLevelId = gradeLevels[gradeLevelSeed.Name].Id,
                    TeacherId = homeroomTeacher.Id
                };

                context.ClassRooms.Add(classRoom);
                classRoomsByName[Normalize(className)] = classRoom;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return GradeLevelSeeds
            .SelectMany(seed => seed.ClassNames)
            .Select(className => classRoomsByName[Normalize(className)])
            .OrderBy(classRoom => classRoom.Name)
            .ToList();
    }

    private static async Task<List<Subject>> EnsureSubjectsAsync(
        SchoolDbContext context,
        IReadOnlyCollection<ClassRoom> classRooms,
        IReadOnlyDictionary<string, Teacher> teachers,
        CancellationToken cancellationToken)
    {
        var subjects = await context.Subjects.ToListAsync(cancellationToken);
        var subjectKeys = subjects
            .Where(subject => subject.ClassRoomId.HasValue && !string.IsNullOrWhiteSpace(subject.Name))
            .Select(subject => GetSubjectKey(subject.ClassRoomId!.Value, subject.Name!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var classRoom in classRooms)
        {
            foreach (var seed in SubjectSeeds)
            {
                var key = GetSubjectKey(classRoom.Id, seed.Name);
                if (subjectKeys.Contains(key))
                {
                    continue;
                }

                var subject = new Subject
                {
                    Name = seed.Name,
                    Code = $"{seed.Code}-{classRoom.Name?.Replace("/", string.Empty, StringComparison.Ordinal)}",
                    Description = $"{seed.Name} seed subject for {classRoom.Name}",
                    Term = "Term 1",
                    IsActive = true,
                    TeacherId = teachers[seed.TeacherName].Id,
                    ClassRoomId = classRoom.Id
                };

                context.Subjects.Add(subject);
                subjects.Add(subject);
                subjectKeys.Add(key);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var targetClassIds = classRooms.Select(classRoom => classRoom.Id).ToHashSet();
        var targetSubjectNames = SubjectSeeds.Select(seed => Normalize(seed.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return subjects
            .Where(subject => subject.ClassRoomId.HasValue)
            .Where(subject => targetClassIds.Contains(subject.ClassRoomId!.Value))
            .Where(subject => !string.IsNullOrWhiteSpace(subject.Name))
            .Where(subject => targetSubjectNames.Contains(Normalize(subject.Name!)))
            .ToList();
    }

    private static async Task<Dictionary<int, List<Student>>> EnsureStudentsAsync(
        SchoolDbContext context,
        IReadOnlyCollection<ClassRoom> classRooms,
        CancellationToken cancellationToken)
    {
        var students = await context.Students.ToListAsync(cancellationToken);
        var studentsByEmail = students
            .Where(student => !string.IsNullOrWhiteSpace(student.Email))
            .GroupBy(student => student.Email!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var classRoom in classRooms)
        {
            var existingClassStudents = students
                .Where(student => student.ClassRoomId == classRoom.Id)
                .OrderBy(student => student.Id)
                .ToList();

            for (var index = existingClassStudents.Count; index < 5; index++)
            {
                var classToken = BuildClassToken(classRoom.Name);
                var email = $"grade-seed-{classToken}-{index + 1}@school.com";

                if (studentsByEmail.ContainsKey(email))
                {
                    continue;
                }

                var student = new Student
                {
                    FullName = $"{classRoom.Name} Student {index + 1}",
                    Email = email,
                    Phone = $"0102000{classRoom.Id:000}{index + 1}",
                    BirthDate = DateTime.Today.AddYears(-7 - (classRoom.Id % 3)).AddDays(index * 17),
                    ClassRoomId = classRoom.Id,
                    QrCodeValue = $"GM-{classToken}-{index + 1}",
                    IsActive = true
                };

                context.Students.Add(student);
                students.Add(student);
                studentsByEmail[email] = student;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return classRooms.ToDictionary(
            classRoom => classRoom.Id,
            classRoom => students
                .Where(student => student.ClassRoomId == classRoom.Id)
                .OrderBy(student => student.Id)
                .Take(5)
                .ToList());
    }

    private static async Task<List<GradeSession>> EnsureGradeSessionsAsync(
        SchoolDbContext context,
        IReadOnlyCollection<ClassRoom> classRooms,
        IReadOnlyCollection<Subject> subjects,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var sessions = new List<GradeSession>();
        var existingSessionKeys = await context.GradeSessions
            .Select(session => new { session.ClassId, session.SubjectId, session.Type, session.Date })
            .ToListAsync(cancellationToken);
        var sessionKeys = existingSessionKeys
            .Select(session => GetSessionKey(session.ClassId, session.SubjectId, session.Type, session.Date))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var classRoom in classRooms)
        {
            var math = subjects.FirstOrDefault(subject =>
                subject.ClassRoomId == classRoom.Id &&
                string.Equals(subject.Name, "Math", StringComparison.OrdinalIgnoreCase));
            var arabic = subjects.FirstOrDefault(subject =>
                subject.ClassRoomId == classRoom.Id &&
                string.Equals(subject.Name, "Arabic", StringComparison.OrdinalIgnoreCase));

            AddSessionIfMissing(math, "Homework");
            AddSessionIfMissing(arabic, "Exam");

            void AddSessionIfMissing(Subject? subject, string type)
            {
                if (subject == null)
                {
                    return;
                }

                var key = GetSessionKey(classRoom.Id, subject.Id, type, today);
                if (sessionKeys.Contains(key))
                {
                    return;
                }

                var session = new GradeSession
                {
                    ClassId = classRoom.Id,
                    SubjectId = subject.Id,
                    Type = type,
                    Date = today,
                    Deadline = today.AddDays(7).AddHours(23).AddMinutes(59)
                };

                context.GradeSessions.Add(session);
                sessions.Add(session);
                sessionKeys.Add(key);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return sessions;
    }

    private static async Task EnsureGradesAndUploadsAsync(
        SchoolDbContext context,
        IReadOnlyList<GradeSession> sessions,
        IReadOnlyDictionary<int, List<Student>> studentsByClassId,
        CancellationToken cancellationToken)
    {
        if (sessions.Count == 0)
        {
            return;
        }

        var sessionIds = sessions.Select(session => session.Id).ToList();
        var sessionsWithSubjects = await context.GradeSessions
            .Include(session => session.Subject)
            .Where(session => sessionIds.Contains(session.Id))
            .OrderBy(session => session.ClassId)
            .ThenBy(session => session.Subject!.Name)
            .ToListAsync(cancellationToken);

        var random = new Random(26042026);

        for (var index = 0; index < sessionsWithSubjects.Count; index++)
        {
            var session = sessionsWithSubjects[index];
            var teacherId = session.Subject?.TeacherId;

            if (!teacherId.HasValue)
            {
                continue;
            }

            var status = index % 2 == 0
                ? GradeUploadStatuses.Approved
                : GradeUploadStatuses.InProgress;

            context.GradeUploads.Add(new GradeUpload
            {
                SessionId = session.Id,
                TeacherId = teacherId.Value,
                Status = status,
                UpdatedAt = DateTime.UtcNow,
                ApprovedAt = status == GradeUploadStatuses.Approved ? DateTime.UtcNow : null
            });

            if (!studentsByClassId.TryGetValue(session.ClassId, out var students) || students.Count == 0)
            {
                continue;
            }

            var studentsToGrade = status == GradeUploadStatuses.Approved
                ? students
                : students.Take(Math.Max(1, students.Count - 2)).ToList();

            foreach (var student in studentsToGrade)
            {
                context.Grades.Add(new Grade
                {
                    StudentId = student.Id,
                    SessionId = session.Id,
                    Score = random.Next(55, 101),
                    MaxScore = DefaultMaxScore
                });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string GetSubjectKey(int classRoomId, string subjectName)
    {
        return $"{classRoomId}:{Normalize(subjectName)}";
    }

    private static string GetSessionKey(int classRoomId, int subjectId, string type, DateTime date)
    {
        return $"{classRoomId}:{subjectId}:{Normalize(type)}:{date:yyyyMMdd}";
    }

    private static string Normalize(string value)
    {
        return value.Trim().Replace("  ", " ", StringComparison.Ordinal).ToLowerInvariant();
    }

    private static string BuildClassToken(string? className)
    {
        return Normalize(className ?? "class").Replace("/", "-", StringComparison.Ordinal).Replace(" ", "-", StringComparison.Ordinal);
    }

    private static string GetAcademicYear(DateTime date)
    {
        return date.Month >= 8
            ? $"{date.Year}/{date.Year + 1}"
            : $"{date.Year - 1}/{date.Year}";
    }

    private sealed record TeacherSeed(string Name, string Email, string Phone, string SubjectName);

    private sealed record GradeLevelSeed(string Name, string[] ClassNames);

    private sealed record SubjectSeed(string Name, string Code, string TeacherName);
}
