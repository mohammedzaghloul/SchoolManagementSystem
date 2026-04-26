using Microsoft.EntityFrameworkCore;
using School.Application.DTOs.GradeManagement;
using School.Application.Interfaces;
using School.Domain.Entities;
using School.Infrastructure.Data;

namespace School.Infrastructure.Services;

public sealed class GradeManagementService : IGradeManagementService
{
    private const string ScopeAll = "All";
    private const string ScopeClass = "Class";
    private const string ScopeGradeLevel = "GradeLevel";

    private readonly SchoolDbContext _context;

    public GradeManagementService(SchoolDbContext context)
    {
        _context = context;
    }

    public async Task<PublishGradeSessionsResultDto> PublishSessionsAsync(
        PublishGradeSessionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var type = NormalizeType(request.Type);
        var date = NormalizeDate(request.Date);
        var deadline = request.Deadline?.ToUniversalTime();

        if (request.SubjectId <= 0)
        {
            throw new InvalidOperationException("Subject is required.");
        }

        var sourceSubject = await _context.Subjects
            .AsNoTracking()
            .Include(subject => subject.Teacher)
            .FirstOrDefaultAsync(subject => subject.Id == request.SubjectId && subject.IsActive, cancellationToken);

        if (sourceSubject == null)
        {
            throw new KeyNotFoundException("Subject not found.");
        }

        var targetClasses = await GetTargetClassesAsync(request, cancellationToken);
        if (targetClasses.Count == 0)
        {
            throw new InvalidOperationException("No classes matched the selected publish scope.");
        }

        var classIds = targetClasses.Select(item => item.Id).ToList();
        var classSubjects = await _context.Subjects
            .AsNoTracking()
            .Include(subject => subject.Teacher)
            .Where(subject => subject.IsActive)
            .Where(subject => subject.ClassRoomId.HasValue && classIds.Contains(subject.ClassRoomId.Value))
            .ToListAsync(cancellationToken);
        var normalizedSourceName = NormalizeName(sourceSubject.Name);

        var existingSessions = await _context.GradeSessions
            .AsNoTracking()
            .Where(session => classIds.Contains(session.ClassId))
            .Where(session => session.Type == type && session.Date == date)
            .Select(session => new { session.ClassId, session.SubjectId })
            .ToListAsync(cancellationToken);
        var existingKeys = existingSessions
            .Select(item => BuildSessionKey(item.ClassId, item.SubjectId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new PublishGradeSessionsResultDto();

        foreach (var classRoom in targetClasses)
        {
            var subject = ResolveSubjectForClass(sourceSubject, classRoom.Id, normalizedSourceName, classSubjects);
            if (subject == null)
            {
                result.SkippedClasses.Add($"{classRoom.Name}: subject is not assigned to this class.");
                result.SkippedCount++;
                continue;
            }

            if (!subject.TeacherId.HasValue)
            {
                result.SkippedClasses.Add($"{classRoom.Name}: subject has no teacher.");
                result.SkippedCount++;
                continue;
            }

            if (existingKeys.Contains(BuildSessionKey(classRoom.Id, subject.Id)))
            {
                result.DuplicateCount++;
                continue;
            }

            var session = new GradeSession
            {
                ClassId = classRoom.Id,
                SubjectId = subject.Id,
                Type = type,
                Date = date,
                Deadline = deadline
            };

            var upload = new GradeUpload
            {
                TeacherId = subject.TeacherId.Value,
                Session = session,
                Status = GradeUploadStatuses.NotStarted,
                UpdatedAt = DateTime.UtcNow
            };

            _context.GradeSessions.Add(session);
            _context.GradeUploads.Add(upload);

            result.CreatedSessions.Add(new PublishedGradeSessionDto
            {
                ClassId = classRoom.Id,
                ClassName = classRoom.Name,
                SubjectId = subject.Id,
                SubjectName = subject.Name ?? "Subject",
                TeacherId = subject.TeacherId.Value,
                TeacherName = subject.Teacher?.FullName ?? "Teacher",
                Type = type,
                Date = date,
                Deadline = deadline
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        result.CreatedCount = result.CreatedSessions.Count;
        var createdSessionsByKey = await _context.GradeSessions
            .AsNoTracking()
            .Where(session => classIds.Contains(session.ClassId))
            .Where(session => session.Type == type && session.Date == date)
            .Select(session => new { session.Id, session.ClassId, session.SubjectId })
            .ToListAsync(cancellationToken);
        var createdIdByKey = createdSessionsByKey.ToDictionary(
            item => BuildSessionKey(item.ClassId, item.SubjectId),
            item => item.Id,
            StringComparer.OrdinalIgnoreCase);

        foreach (var created in result.CreatedSessions)
        {
            createdIdByKey.TryGetValue(BuildSessionKey(created.ClassId, created.SubjectId), out var sessionId);
            created.SessionId = sessionId;
        }

        return result;
    }

    public async Task<AdminGradeSessionsDashboardDto> GetAdminDashboardAsync(
        string? type,
        DateTime? date,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GradeSessions
            .AsNoTracking()
            .Include(session => session.ClassRoom)
            .ThenInclude(classRoom => classRoom!.GradeLevel)
            .Include(session => session.Subject)
            .ThenInclude(subject => subject!.Teacher)
            .Include(session => session.Uploads)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = NormalizeType(type);
            query = query.Where(session => session.Type == normalizedType);
        }

        if (date.HasValue)
        {
            var normalizedDate = NormalizeDate(date.Value);
            query = query.Where(session => session.Date == normalizedDate);
        }

        var sessions = await query
            .OrderByDescending(session => session.Date)
            .ThenBy(session => session.ClassRoom!.Name)
            .ThenBy(session => session.Subject!.Name)
            .ToListAsync(cancellationToken);

        var sessionIds = sessions.Select(session => session.Id).ToList();
        var classIds = sessions.Select(session => session.ClassId).Distinct().ToList();

        var studentCounts = await _context.Students
            .AsNoTracking()
            .Where(student => student.IsActive)
            .Where(student => student.ClassRoomId.HasValue && classIds.Contains(student.ClassRoomId.Value))
            .GroupBy(student => student.ClassRoomId!.Value)
            .Select(group => new { ClassId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ClassId, item => item.Count, cancellationToken);

        var gradeCounts = await _context.Grades
            .AsNoTracking()
            .Where(grade => sessionIds.Contains(grade.SessionId))
            .GroupBy(grade => grade.SessionId)
            .Select(group => new { SessionId = group.Key, Count = group.Select(item => item.StudentId).Distinct().Count() })
            .ToDictionaryAsync(item => item.SessionId, item => item.Count, cancellationToken);

        var items = sessions.Select(session =>
        {
            studentCounts.TryGetValue(session.ClassId, out var totalStudents);
            gradeCounts.TryGetValue(session.Id, out var gradedStudents);
            var upload = ResolveUpload(session);
            var status = upload?.Status ?? GradeUploadStatuses.NotStarted;

            return new GradeSessionMonitorDto
            {
                SessionId = session.Id,
                ClassName = session.ClassRoom?.Name ?? $"Class {session.ClassId}",
                GradeLevelName = session.ClassRoom?.GradeLevel?.Name,
                SubjectName = session.Subject?.Name ?? "Subject",
                Type = session.Type,
                Date = session.Date,
                Deadline = session.Deadline,
                TeacherId = upload?.TeacherId ?? session.Subject?.TeacherId ?? 0,
                TeacherName = session.Subject?.Teacher?.FullName ?? "Teacher",
                TotalStudents = totalStudents,
                GradedStudents = gradedStudents,
                MissingGradesCount = Math.Max(totalStudents - gradedStudents, 0),
                ProgressPercent = totalStudents == 0 ? 0 : (int)Math.Round(gradedStudents * 100.0 / totalStudents),
                Status = status
            };
        }).ToList();

        var approvedSessions = items.Count(item => item.Status == GradeUploadStatuses.Approved);

        return new AdminGradeSessionsDashboardDto
        {
            TotalSessions = items.Count,
            ApprovedSessions = approvedSessions,
            InProgressSessions = Math.Max(items.Count - approvedSessions, 0),
            GlobalStatus = items.Count > 0 && approvedSessions == items.Count ? "Completed" : "In Progress",
            Sessions = items
        };
    }

    public async Task<IReadOnlyList<TeacherGradeSessionOptionDto>> GetTeacherSessionsAsync(
        int teacherId,
        CancellationToken cancellationToken = default)
    {
        if (teacherId <= 0)
        {
            return [];
        }

        var uploads = await _context.GradeUploads
            .AsNoTracking()
            .Include(upload => upload.Session)
            .ThenInclude(session => session!.ClassRoom)
            .ThenInclude(classRoom => classRoom!.GradeLevel)
            .Include(upload => upload.Session)
            .ThenInclude(session => session!.Subject)
            .Where(upload => upload.TeacherId == teacherId)
            .OrderByDescending(upload => upload.Session!.Date)
            .ThenBy(upload => upload.Session!.ClassRoom!.Name)
            .ToListAsync(cancellationToken);

        var sessionIds = uploads.Select(upload => upload.SessionId).ToList();
        var classIds = uploads
            .Where(upload => upload.Session != null)
            .Select(upload => upload.Session!.ClassId)
            .Distinct()
            .ToList();

        var studentCounts = await _context.Students
            .AsNoTracking()
            .Where(student => student.IsActive)
            .Where(student => student.ClassRoomId.HasValue && classIds.Contains(student.ClassRoomId.Value))
            .GroupBy(student => student.ClassRoomId!.Value)
            .Select(group => new { ClassId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ClassId, item => item.Count, cancellationToken);

        var gradeCounts = await _context.Grades
            .AsNoTracking()
            .Where(grade => sessionIds.Contains(grade.SessionId))
            .GroupBy(grade => grade.SessionId)
            .Select(group => new { SessionId = group.Key, Count = group.Select(item => item.StudentId).Distinct().Count() })
            .ToDictionaryAsync(item => item.SessionId, item => item.Count, cancellationToken);

        return uploads
            .Where(upload => upload.Session != null)
            .Select(upload =>
            {
                var session = upload.Session!;
                studentCounts.TryGetValue(session.ClassId, out var totalStudents);
                gradeCounts.TryGetValue(session.Id, out var gradedStudents);

                return new TeacherGradeSessionOptionDto
                {
                    SessionId = session.Id,
                    ClassId = session.ClassId,
                    ClassName = session.ClassRoom?.Name ?? $"Class {session.ClassId}",
                    GradeLevelName = session.ClassRoom?.GradeLevel?.Name,
                    SubjectId = session.SubjectId,
                    SubjectName = session.Subject?.Name ?? "Subject",
                    Type = session.Type,
                    Date = session.Date,
                    Deadline = session.Deadline,
                    Status = upload.Status,
                    ProgressPercent = totalStudents == 0 ? 0 : (int)Math.Round(gradedStudents * 100.0 / totalStudents)
                };
            })
            .ToList();
    }

    public async Task<TeacherSessionGradebookDto?> GetTeacherGradebookAsync(
        int sessionId,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveSessionAccessAsync(sessionId, teacherId, isAdmin, track: false, cancellationToken);
        if (access == null)
        {
            return null;
        }

        return await BuildGradebookAsync(access.Session, access.Upload, cancellationToken);
    }

    public async Task<GradeOperationResultDto> SaveTeacherGradesAsync(
        SaveTeacherSessionGradesRequest request,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        if (request.SessionId <= 0)
        {
            throw new InvalidOperationException("Session is required.");
        }

        var access = await ResolveSessionAccessAsync(request.SessionId, teacherId, isAdmin, track: true, cancellationToken)
            ?? throw new KeyNotFoundException("Grade session not found.");

        if (access.Upload.Status == GradeUploadStatuses.Approved)
        {
            throw new InvalidOperationException("This session is approved and locked.");
        }

        if (IsDeadlinePassed(access.Session))
        {
            throw new InvalidOperationException("The session deadline has passed.");
        }

        var activeStudentIds = await _context.Students
            .AsNoTracking()
            .Where(student => student.IsActive)
            .Where(student => student.ClassRoomId == access.Session.ClassId)
            .Select(student => student.Id)
            .ToHashSetAsync(cancellationToken);

        var items = request.Grades
            .Where(item => item.Score.HasValue)
            .ToList();

        foreach (var item in items)
        {
            if (!activeStudentIds.Contains(item.StudentId))
            {
                throw new InvalidOperationException("One or more students do not belong to this class.");
            }

            if (item.MaxScore <= 0)
            {
                throw new InvalidOperationException("Max score must be greater than zero.");
            }

            if (item.Score < 0 || item.Score > item.MaxScore)
            {
                throw new InvalidOperationException("Score must be between zero and max score.");
            }
        }

        var studentIds = items.Select(item => item.StudentId).Distinct().ToList();
        var existingGrades = await _context.Grades
            .Where(grade => grade.SessionId == request.SessionId && studentIds.Contains(grade.StudentId))
            .ToListAsync(cancellationToken);
        var existingByStudentId = existingGrades.ToDictionary(grade => grade.StudentId);

        foreach (var item in items)
        {
            if (!existingByStudentId.TryGetValue(item.StudentId, out var grade))
            {
                grade = new Grade
                {
                    StudentId = item.StudentId,
                    SessionId = request.SessionId
                };
                _context.Grades.Add(grade);
            }

            grade.Score = item.Score!.Value;
            grade.MaxScore = item.MaxScore;
        }

        access.Upload.Status = items.Count > 0 ? GradeUploadStatuses.InProgress : GradeUploadStatuses.NotStarted;
        access.Upload.UpdatedAt = DateTime.UtcNow;
        access.Upload.ApprovedAt = null;

        await _context.SaveChangesAsync(cancellationToken);

        var coverage = await GetCoverageAsync(access.Session.Id, access.Session.ClassId, cancellationToken);
        return new GradeOperationResultDto
        {
            Success = true,
            Message = $"Saved {items.Count} grade(s).",
            TotalStudents = coverage.TotalStudents,
            GradedStudents = coverage.GradedStudents,
            MissingGradesCount = coverage.MissingGradesCount,
            Status = access.Upload.Status
        };
    }

    public async Task<GradeOperationResultDto> ApproveTeacherUploadAsync(
        int sessionId,
        int? teacherId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveSessionAccessAsync(sessionId, teacherId, isAdmin, track: true, cancellationToken)
            ?? throw new KeyNotFoundException("Grade session not found.");

        if (IsDeadlinePassed(access.Session))
        {
            throw new InvalidOperationException("The session deadline has passed.");
        }

        var coverage = await GetCoverageAsync(access.Session.Id, access.Session.ClassId, cancellationToken);
        if (coverage.TotalStudents == 0)
        {
            throw new InvalidOperationException("No active students found in this class.");
        }

        if (coverage.MissingGradesCount > 0)
        {
            throw new InvalidOperationException($"Cannot approve before all students are graded. Missing: {coverage.MissingGradesCount}.");
        }

        access.Upload.Status = GradeUploadStatuses.Approved;
        access.Upload.UpdatedAt = DateTime.UtcNow;
        access.Upload.ApprovedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new GradeOperationResultDto
        {
            Success = true,
            Message = "Grade upload approved.",
            TotalStudents = coverage.TotalStudents,
            GradedStudents = coverage.GradedStudents,
            MissingGradesCount = coverage.MissingGradesCount,
            Status = GradeUploadStatuses.Approved
        };
    }

    private async Task<List<ClassLookup>> GetTargetClassesAsync(
        PublishGradeSessionsRequest request,
        CancellationToken cancellationToken)
    {
        var scope = NormalizeScope(request.Scope);
        var query = _context.ClassRooms
            .AsNoTracking()
            .Include(classRoom => classRoom.GradeLevel)
            .AsQueryable();

        if (scope == ScopeClass)
        {
            if (!request.ClassId.HasValue || request.ClassId.Value <= 0)
            {
                throw new InvalidOperationException("Class is required.");
            }

            query = query.Where(classRoom => classRoom.Id == request.ClassId.Value);
        }
        else if (scope == ScopeGradeLevel)
        {
            if (!request.GradeLevelId.HasValue || request.GradeLevelId.Value <= 0)
            {
                throw new InvalidOperationException("Grade level is required.");
            }

            query = query.Where(classRoom => classRoom.GradeLevelId == request.GradeLevelId.Value);
        }

        return await query
            .OrderBy(classRoom => classRoom.GradeLevel != null ? classRoom.GradeLevel.Name : string.Empty)
            .ThenBy(classRoom => classRoom.Name)
            .Select(classRoom => new ClassLookup(classRoom.Id, classRoom.Name ?? $"Class {classRoom.Id}"))
            .ToListAsync(cancellationToken);
    }

    private static Subject? ResolveSubjectForClass(
        Subject sourceSubject,
        int classId,
        string normalizedSourceName,
        IReadOnlyList<Subject> classSubjects)
    {
        if (sourceSubject.ClassRoomId == classId)
        {
            return sourceSubject;
        }

        return classSubjects.FirstOrDefault(subject =>
            subject.ClassRoomId == classId &&
            NormalizeName(subject.Name) == normalizedSourceName);
    }

    private async Task<SessionAccess?> ResolveSessionAccessAsync(
        int sessionId,
        int? teacherId,
        bool isAdmin,
        bool track,
        CancellationToken cancellationToken)
    {
        IQueryable<GradeSession> query = _context.GradeSessions
            .Include(session => session.Subject)
            .Include(session => session.ClassRoom)
            .Include(session => session.Uploads);

        if (!track)
        {
            query = query.AsNoTracking();
        }

        var session = await query.FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session == null)
        {
            return null;
        }

        var upload = ResolveUpload(session);
        if (upload == null)
        {
            var subjectTeacherId = session.Subject?.TeacherId;
            if (!subjectTeacherId.HasValue)
            {
                throw new InvalidOperationException("Session subject has no teacher.");
            }

            upload = new GradeUpload
            {
                SessionId = session.Id,
                TeacherId = subjectTeacherId.Value,
                Status = GradeUploadStatuses.NotStarted,
                UpdatedAt = DateTime.UtcNow
            };

            if (track)
            {
                _context.GradeUploads.Add(upload);
            }
        }

        if (!isAdmin && (!teacherId.HasValue || upload.TeacherId != teacherId.Value))
        {
            throw new UnauthorizedAccessException("Not allowed to access this grade session.");
        }

        return new SessionAccess(session, upload);
    }

    private async Task<TeacherSessionGradebookDto> BuildGradebookAsync(
        GradeSession session,
        GradeUpload upload,
        CancellationToken cancellationToken)
    {
        var students = await _context.Students
            .AsNoTracking()
            .Where(student => student.IsActive)
            .Where(student => student.ClassRoomId == session.ClassId)
            .OrderBy(student => student.FullName)
            .Select(student => new { student.Id, student.FullName })
            .ToListAsync(cancellationToken);

        var grades = await _context.Grades
            .AsNoTracking()
            .Where(grade => grade.SessionId == session.Id)
            .ToListAsync(cancellationToken);
        var gradesByStudentId = grades.ToDictionary(grade => grade.StudentId);

        var rows = students.Select(student =>
        {
            gradesByStudentId.TryGetValue(student.Id, out var grade);
            var percentage = grade == null || grade.MaxScore <= 0
                ? (double?)null
                : Math.Round(grade.Score * 100.0 / grade.MaxScore, 1);

            return new TeacherSessionGradeStudentDto
            {
                StudentId = student.Id,
                StudentName = student.FullName ?? "Student",
                GradeId = grade?.Id,
                Score = grade?.Score,
                MaxScore = grade?.MaxScore ?? 100,
                Percentage = percentage,
                IsGraded = grade != null
            };
        }).ToList();

        var gradedStudents = rows.Count(row => row.IsGraded);
        var deadlinePassed = IsDeadlinePassed(session);
        var approved = upload.Status == GradeUploadStatuses.Approved;

        return new TeacherSessionGradebookDto
        {
            SessionId = session.Id,
            ClassName = session.ClassRoom?.Name ?? $"Class {session.ClassId}",
            SubjectName = session.Subject?.Name ?? "Subject",
            Type = session.Type,
            Date = session.Date,
            Deadline = session.Deadline,
            IsApproved = approved,
            IsDeadlinePassed = deadlinePassed,
            IsLocked = approved || deadlinePassed,
            Status = upload.Status,
            TotalStudents = rows.Count,
            GradedStudents = gradedStudents,
            MissingGradesCount = Math.Max(rows.Count - gradedStudents, 0),
            ProgressPercent = rows.Count == 0 ? 0 : (int)Math.Round(gradedStudents * 100.0 / rows.Count),
            Students = rows
        };
    }

    private async Task<Coverage> GetCoverageAsync(int sessionId, int classId, CancellationToken cancellationToken)
    {
        var activeStudents = _context.Students
            .AsNoTracking()
            .Where(student => student.IsActive)
            .Where(student => student.ClassRoomId == classId);

        var totalStudents = await activeStudents.CountAsync(cancellationToken);
        var gradedStudents = await _context.Grades
            .AsNoTracking()
            .Where(grade => grade.SessionId == sessionId)
            .Join(activeStudents, grade => grade.StudentId, student => student.Id, (grade, _) => grade.StudentId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new Coverage(totalStudents, gradedStudents, Math.Max(totalStudents - gradedStudents, 0));
    }

    private static GradeUpload? ResolveUpload(GradeSession session)
    {
        return session.Uploads
            .OrderByDescending(upload => upload.UpdatedAt)
            .ThenByDescending(upload => upload.Id)
            .FirstOrDefault();
    }

    private static bool IsDeadlinePassed(GradeSession session)
    {
        return session.Deadline.HasValue && DateTime.UtcNow > session.Deadline.Value;
    }

    private static string NormalizeScope(string? scope)
    {
        var normalized = string.IsNullOrWhiteSpace(scope) ? ScopeClass : scope.Trim();
        return normalized.Equals(ScopeAll, StringComparison.OrdinalIgnoreCase)
            ? ScopeAll
            : normalized.Equals(ScopeGradeLevel, StringComparison.OrdinalIgnoreCase)
                ? ScopeGradeLevel
                : ScopeClass;
    }

    private static string NormalizeType(string? type)
    {
        return string.IsNullOrWhiteSpace(type) ? "Homework" : type.Trim();
    }

    private static string NormalizeName(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static DateTime NormalizeDate(DateTime date)
    {
        return date.Date;
    }

    private static string BuildSessionKey(int classId, int subjectId)
    {
        return $"{classId}:{subjectId}";
    }

    private sealed record ClassLookup(int Id, string Name);
    private sealed record SessionAccess(GradeSession Session, GradeUpload Upload);
    private sealed record Coverage(int TotalStudents, int GradedStudents, int MissingGradesCount);
}
