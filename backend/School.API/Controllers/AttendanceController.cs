using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.API.Infrastructure;
using School.Application.Features.Attendance.Commands;
using School.Application.Interfaces;
using School.Infrastructure.Data;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class AttendanceController : BaseApiController
{
    private const int AttendanceWindowOpensBeforeMinutes = 30;
    private const int AttendanceWindowClosesAfterHours = 3;

    private readonly IQrCodeService _qrCodeService;
    private readonly SchoolDbContext _context;
    private readonly IFaceRecognitionService _faceRecognition;
    private readonly IFileStorageService _storage;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IQrCodeService qrCodeService,
        SchoolDbContext context,
        IFaceRecognitionService faceRecognition,
        IFileStorageService storage,
        ILogger<AttendanceController> logger)
    {
        _qrCodeService = qrCodeService;
        _context = context;
        _faceRecognition = faceRecognition;
        _storage = storage;
        _logger = logger;
    }

    private async Task<School.Domain.Entities.Student?> GetCurrentStudentAsync()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return null;
        }

        return await _context.Students.FirstOrDefaultAsync(student => student.Email == userEmail);
    }

    private async Task<(School.Domain.Entities.Session? Session, ActionResult? Error)> GetManagedSessionAsync(int sessionId)
    {
        var session = await _context.Sessions
            .Include(currentSession => currentSession.Teacher)
            .Include(currentSession => currentSession.ClassRoom)
            .Include(currentSession => currentSession.Subject)
            .FirstOrDefaultAsync(currentSession => currentSession.Id == sessionId);

        if (session == null)
        {
            return (null, NotFound(new { success = false, message = "الحصة غير موجودة." }));
        }

        if (!User.IsInRole("Teacher"))
        {
            return (session, null);
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var teacher = await _context.Teachers
            .FirstOrDefaultAsync(currentTeacher => currentTeacher.Email == userEmail);

        if (teacher == null || teacher.Id != session.TeacherId)
        {
            return (null, StatusCode(403, new
            {
                success = false,
                message = "لا يمكنك إدارة رصد حصة غير مسندة إليك."
            }));
        }

        return (session, null);
    }

    private ActionResult? EnsureAttendanceWindowIsOpen(School.Domain.Entities.Session session)
    {
        if (User.IsInRole("Admin"))
        {
            return null;
        }

        var window = DescribeAttendanceWindow(session);
        if (window.CanRecord)
        {
            return null;
        }

        return Conflict(new
        {
            success = false,
            canRecordAttendance = false,
            attendanceWindowStatus = window.Status,
            message = window.Message
        });
    }

    private ActionResult? EnsureManualAttendanceEditIsOpen(School.Domain.Entities.Session session)
    {
        if (User.IsInRole("Admin"))
        {
            return null;
        }

        var now = SchoolClock.Now;
        var sessionStart = session.SessionDate.Date.Add(session.StartTime);
        if (now < sessionStart.AddMinutes(-AttendanceWindowOpensBeforeMinutes))
        {
            return Conflict(new
            {
                success = false,
                canRecordAttendance = false,
                attendanceWindowStatus = "upcoming",
                message = $"ÙŠØ¨Ø¯Ø£ Ø§Ù„Ø±ØµØ¯ Ù…Ù† {sessionStart.AddMinutes(-AttendanceWindowOpensBeforeMinutes):hh:mm tt}."
            });
        }

        var weekLock = GetAttendanceWeekLockTime(session.SessionDate);
        if (now >= weekLock)
        {
            return Conflict(new
            {
                success = false,
                canRecordAttendance = false,
                attendanceWindowStatus = "locked",
                message = $"ØªÙ… Ù‚ÙÙ„ ØªØ¹Ø¯ÙŠÙ„ Ø­Ø¶ÙˆØ± Ù‡Ø°Ø§ Ø§Ù„Ø£Ø³Ø¨ÙˆØ¹ ÙŠÙˆÙ… Ø§Ù„Ø¬Ù…Ø¹Ø© {weekLock:hh:mm tt}. Ø§Ù„ØªØ¹Ø¯ÙŠÙ„ Ù…ØªØ§Ø­ Ù„Ù„Ø£Ø¯Ù…Ù† ÙÙ‚Ø·."
            });
        }

        return null;
    }

    private ActionResult? EnsureQrBroadcastIsSupported(School.Domain.Entities.Session session)
    {
        if (string.Equals(session.AttendanceType ?? "QR", "QR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Conflict(new
        {
            success = false,
            message = "بث رمز QR متاح فقط للحصص المضبوطة على QR."
        });
    }

    private static AttendanceWindowState DescribeAttendanceWindow(School.Domain.Entities.Session session)
    {
        var sessionStart = session.SessionDate.Date.Add(session.StartTime);
        var sessionEnd = session.SessionDate.Date.Add(session.EndTime);
        var windowStart = sessionStart.AddMinutes(-AttendanceWindowOpensBeforeMinutes);
        var windowEnd = sessionEnd.AddHours(AttendanceWindowClosesAfterHours);
        var now = SchoolClock.Now;

        if (now < windowStart)
        {
            return new AttendanceWindowState(
                "upcoming",
                $"يبدأ الرصد من {windowStart:hh:mm tt}",
                false,
                windowStart,
                windowEnd);
        }

        if (now > windowEnd)
        {
            return new AttendanceWindowState(
                "closed",
                $"انتهت نافذة الرصد عند {windowEnd:hh:mm tt}",
                false,
                windowStart,
                windowEnd);
        }

        return new AttendanceWindowState(
            "open",
            $"الرصد متاح الآن حتى {windowEnd:hh:mm tt}",
            true,
            windowStart,
            windowEnd);
    }

    private static DateTime GetAttendanceWeekLockTime(DateTime sessionDate)
    {
        var daysUntilFriday = ((int)DayOfWeek.Friday - (int)sessionDate.DayOfWeek + 7) % 7;
        return sessionDate.Date.AddDays(daysUntilFriday).AddHours(12);
    }

    private static string NormalizeManualAttendanceStatus(string? status, bool isPresent)
    {
        var normalized = status?.Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            if (string.Equals(normalized, "Present", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "حاضر", StringComparison.OrdinalIgnoreCase))
            {
                return "Present";
            }

            if (string.Equals(normalized, "Absent", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "غائب", StringComparison.OrdinalIgnoreCase))
            {
                return "Absent";
            }

            if (string.Equals(normalized, "Late", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "متأخر", StringComparison.OrdinalIgnoreCase))
            {
                return "Late";
            }

            if (string.Equals(normalized, "Unrecorded", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "لم يرصد", StringComparison.OrdinalIgnoreCase))
            {
                return "Unrecorded";
            }
        }

        return isPresent ? "Present" : "Absent";
    }

    [HttpPost("scan-qr")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<bool>> ScanQr(ScanQrCommand command)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var student = await _context.Students.FirstOrDefaultAsync(currentStudent => currentStudent.Email == userEmail);
        if (student == null)
        {
            return Unauthorized(new { success = false, message = "تعذر تحديد الطالب الحالي." });
        }

        command.StudentId = student.Id;

        try
        {
            var decodedString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(command.QrToken));
            var parts = decodedString.Split('|');
            var payloadParts = parts[0].Split(':');
            command.SessionId = int.Parse(payloadParts[0]);
        }
        catch
        {
            return BadRequest(new { success = false, message = "صيغة رمز QR غير صحيحة." });
        }

        var result = await Mediator.Send(command);
        if (!result)
        {
            return BadRequest(new
            {
                success = false,
                message = "رمز QR غير صالح أو انتهت صلاحيته. اطلب من المعلم تحديث الرمز ثم أعد المحاولة."
            });
        }

        return Ok(result);
    }

    [HttpGet("generate-qr/{sessionId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<string>> GenerateQrToken(int sessionId)
    {
        var (session, error) = await GetManagedSessionAsync(sessionId);
        if (error != null)
        {
            return error;
        }

        var qrSupportError = EnsureQrBroadcastIsSupported(session!);
        if (qrSupportError != null)
        {
            return qrSupportError;
        }

        var windowError = EnsureAttendanceWindowIsOpen(session!);
        if (windowError != null)
        {
            return windowError;
        }

        var token = _qrCodeService.GenerateQrToken(sessionId);
        var window = DescribeAttendanceWindow(session!);

        return Ok(new
        {
            Token = token,
            canRecordAttendance = true,
            attendanceWindowStatus = window.Status,
            attendanceWindowMessage = window.Message
        });
    }

    [HttpGet("generate-flex-qr/{sessionId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult<string>> GenerateFlexibleQrToken(int sessionId)
    {
        var (session, error) = await GetManagedSessionAsync(sessionId);
        if (error != null)
        {
            return error;
        }

        var windowError = EnsureAttendanceWindowIsOpen(session!);
        if (windowError != null)
        {
            return windowError;
        }

        var token = _qrCodeService.GenerateQrToken(sessionId);
        var window = DescribeAttendanceWindow(session!);

        var attendanceType = session!.AttendanceType ?? "QR";
        var isFlex = attendanceType.Equals("Flex", StringComparison.OrdinalIgnoreCase);

        return Ok(new
        {
            Token = token,
            canRecordAttendance = true,
            attendanceWindowStatus = window.Status,
            attendanceWindowMessage = window.Message,
            attendanceType = attendanceType,
            supportsManual = true, // Teachers can always manually override
            supportsFace = isFlex || attendanceType.Equals("Face", StringComparison.OrdinalIgnoreCase),
            supportsQr = isFlex || attendanceType.Equals("QR", StringComparison.OrdinalIgnoreCase)
        });
    }

    [HttpGet("session/{sessionId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult> GetSessionAttendance(int sessionId)
    {
        var (_, error) = await GetManagedSessionAsync(sessionId);
        if (error != null)
        {
            return error;
        }

        var records = await _context.Attendances
            .Include(attendance => attendance.Student)
            .Where(attendance => attendance.SessionId == sessionId)
            .Select(attendance => new
            {
                studentId = attendance.StudentId,
                studentName = attendance.Student.FullName,
                status = attendance.Status,
                recordedAt = attendance.RecordedAt,
                method = attendance.Method
            })
            .ToListAsync();

        return Ok(records);
    }

    [HttpGet("session/{sessionId}/roster")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult> GetSessionRoster(int sessionId)
    {
        var (session, error) = await GetManagedSessionAsync(sessionId);
        if (error != null)
        {
            return error;
        }

        var rosterStudents = await _context.Students
            .Where(student => student.ClassRoomId == session!.ClassRoomId)
            .OrderBy(student => student.FullName)
            .Select(student => new
            {
                student.Id,
                student.FullName,
                student.Email
            })
            .ToListAsync();

        var attendanceMap = await _context.Attendances
            .Where(attendance => attendance.SessionId == sessionId)
            .ToDictionaryAsync(attendance => attendance.StudentId);

        var window = DescribeAttendanceWindow(session!);

        var students = rosterStudents.Select(student =>
        {
            attendanceMap.TryGetValue(student.Id, out var attendance);

            return new
            {
                id = student.Id,
                fullName = student.FullName,
                email = student.Email,
                status = attendance?.Status ?? "Unrecorded",
                isPresent = attendance?.IsPresent ?? false,
                notes = attendance?.Notes ?? string.Empty,
                method = attendance?.Method,
                recordedAt = attendance?.RecordedAt
            };
        });

        return Ok(new
        {
            sessionId = session!.Id,
            subjectName = session.Subject?.Name ?? session.Title ?? "حصة دراسية",
            classRoomName = session.ClassRoom?.Name ?? "غير محدد",
            attendanceWindowStatus = window.Status,
            attendanceWindowMessage = window.Message,
            canRecordAttendance = window.CanRecord || User.IsInRole("Admin"),
            totalStudents = rosterStudents.Count,
            students
        });
    }

    [HttpGet("student/{studentId}")]
    public async Task<ActionResult> GetStudentAttendance(int studentId)
    {
        var records = await _context.Attendances
            .Where(attendance => attendance.StudentId == studentId)
            .Include(attendance => attendance.Session)
            .Select(attendance => new
            {
                sessionId = attendance.SessionId,
                sessionName = attendance.Session != null ? attendance.Session.Title : string.Empty,
                status = attendance.Status,
                date = attendance.RecordedAt
            })
            .OrderByDescending(attendance => attendance.date)
            .ToListAsync();

        return Ok(records);
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult> GetCurrentStudentAttendance()
    {
        var student = await GetCurrentStudentAsync();
        if (student == null)
        {
            return Unauthorized("Student not found.");
        }

        var records = await _context.Attendances
            .Where(attendance => attendance.StudentId == student.Id)
            .Include(attendance => attendance.Session)
                .ThenInclude(session => session.Subject)
            .Include(attendance => attendance.Session)
                .ThenInclude(session => session.ClassRoom)
            .OrderByDescending(attendance => attendance.RecordedAt)
            .Select(attendance => new
            {
                sessionId = attendance.SessionId,
                sessionName = attendance.Session != null ? attendance.Session.Title : string.Empty,
                subjectName = attendance.Session != null && attendance.Session.Subject != null ? attendance.Session.Subject.Name : string.Empty,
                classRoomName = attendance.Session != null && attendance.Session.ClassRoom != null ? attendance.Session.ClassRoom.Name : string.Empty,
                status = attendance.Status,
                isPresent = attendance.IsPresent,
                method = attendance.Method,
                recordedAt = attendance.RecordedAt
            })
            .ToListAsync();

        return Ok(new
        {
            studentId = student.Id,
            records
        });
    }

    [HttpGet("student/{studentId}/stats")]
    public async Task<ActionResult> GetStudentStats(int studentId)
    {
        var total = await _context.Attendances.CountAsync(attendance => attendance.StudentId == studentId);
        var present = await _context.Attendances.CountAsync(attendance => attendance.StudentId == studentId && attendance.Status == "Present");
        var absent = await _context.Attendances.CountAsync(attendance => attendance.StudentId == studentId && attendance.Status == "Absent");
        var late = await _context.Attendances.CountAsync(attendance => attendance.StudentId == studentId && attendance.Status == "Late");

        return Ok(new { total, present, absent, late });
    }

    [HttpGet("me/stats")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult> GetCurrentStudentStats()
    {
        var student = await GetCurrentStudentAsync();
        if (student == null)
        {
            return Unauthorized("Student not found.");
        }

        var total = await _context.Attendances.CountAsync(attendance => attendance.StudentId == student.Id);
        var present = await _context.Attendances.CountAsync(attendance => attendance.StudentId == student.Id && attendance.Status == "Present");
        var absent = await _context.Attendances.CountAsync(attendance => attendance.StudentId == student.Id && attendance.Status == "Absent");
        var late = await _context.Attendances.CountAsync(attendance => attendance.StudentId == student.Id && attendance.Status == "Late");
        var attendanceRate = total > 0 ? Math.Round((double)present / total * 100, 1) : 0;

        return Ok(new
        {
            total,
            present,
            absent,
            late,
            attendanceRate
        });
    }

    [HttpPost("face/{sessionId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult> FaceAttendance(int sessionId, [FromForm] IFormFile? file, [FromForm] IFormFile? image)
    {
        var uploadedFile = file ?? image;
        if (uploadedFile == null || uploadedFile.Length == 0)
        {
            return BadRequest(new { success = false, message = "لم يتم إرسال صورة." });
        }

        var (session, error) = await GetManagedSessionAsync(sessionId);
        if (error != null)
        {
            return error;
        }

        var windowError = EnsureAttendanceWindowIsOpen(session!);
        if (windowError != null)
        {
            return windowError;
        }

        using var stream = new MemoryStream();
        await uploadedFile.CopyToAsync(stream);
        var bytes = stream.ToArray();

        var recognizedStudentId = 0;
        var recognized = false;
        var confidence = 0d;
        string? recognitionMessage = null;

        try
        {
            var faceResult = await _faceRecognition.RecognizeFaceAsync(bytes, uploadedFile.FileName);
            recognitionMessage = faceResult.Message;
            if (faceResult.Success)
            {
                recognizedStudentId = faceResult.StudentId;
                recognized = true;
                confidence = faceResult.Confidence;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Face recognition failed while recording attendance for session {SessionId}.", sessionId);
        }

        if (!recognized)
        {
            return Ok(new
            {
                success = false,
                message = recognitionMessage ?? "لم يتم التعرف على الوجه. يرجى المحاولة مرة أخرى أو التأكد من إضاءة المكان."
            });
        }

        var student = await _context.Students
            .Include(currentStudent => currentStudent.ClassRoom)
            .FirstOrDefaultAsync(currentStudent => currentStudent.Id == recognizedStudentId);

        if (false && student == null)
        {
            return BadRequest(new { success = false, message = "الطالب غير مسجل في النظام." });
        }

        if (student == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "البصمة التي تم التعرف عليها غير مرتبطة ببيانات الطلاب الحالية. أعد تدريب وجه الطالب الصحيح ثم حاول مرة أخرى."
            });
        }

        if (session!.ClassRoomId > 0 && student.ClassRoomId > 0 && session.ClassRoomId != student.ClassRoomId)
        {
            return Ok(new
            {
                success = false,
                message = "هذا الطالب ليس من طلاب الفصل المرتبط بهذه الحصة."
            });
        }

        var existingAttendance = await _context.Attendances
            .FirstOrDefaultAsync(attendance => attendance.SessionId == sessionId && attendance.StudentId == recognizedStudentId);

        if (existingAttendance != null &&
            existingAttendance.IsPresent &&
            string.Equals(existingAttendance.Status, "Present", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                success = true,
                alreadyPresent = true,
                recognized = true,
                studentName = student.FullName,
                studentId = student.Id,
                confidence,
                message = "تم تسجيل حضور الطالب مسبقًا."
            });
        }

        var capturedAt = DateTime.UtcNow;
        var updatedExistingRecord = false;

        if (existingAttendance == null)
        {
            existingAttendance = new School.Domain.Entities.Attendance
            {
                StudentId = student.Id,
                SessionId = sessionId
            };

            _context.Attendances.Add(existingAttendance);
        }
        else
        {
            updatedExistingRecord = true;
        }

        existingAttendance.Status = "Present";
        existingAttendance.IsPresent = true;
        existingAttendance.RecordedAt = capturedAt;
        existingAttendance.Time = capturedAt;
        existingAttendance.Method = "Face";
        existingAttendance.Notes = string.Empty;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            recognized = true,
            alreadyPresent = false,
            studentName = student.FullName,
            studentId = student.Id,
            confidence,
            updatedExistingRecord,
            message = "تم تسجيل الحضور بنجاح."
        });
    }

    [HttpPost("manual")]
    [HttpPost("mark-manual")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult> ManualAttendance([FromBody] ManualAttendanceDto dto)
    {
        if (dto.Records == null || dto.Records.Count == 0)
        {
            return Ok(new
            {
                success = true,
                savedCount = 0,
                resetCount = 0,
                message = "لا توجد تغييرات جديدة للحفظ."
            });
        }

        var rawSessionId = string.IsNullOrWhiteSpace(dto.SessionId) ? dto.ClassId : dto.SessionId;
        if (!int.TryParse(rawSessionId, out var sessionId) || sessionId <= 0)
        {
            return BadRequest(new { success = false, message = "تعذر تحديد الحصة المطلوبة." });
        }

        var (session, error) = await GetManagedSessionAsync(sessionId);
        if (error != null)
        {
            return error;
        }

        var windowError = EnsureManualAttendanceEditIsOpen(session!);
        if (windowError != null)
        {
            return windowError;
        }

        var allowedStudentIds = (await _context.Students
            .Where(student => student.ClassRoomId == session!.ClassRoomId)
            .Select(student => student.Id)
            .ToListAsync())
            .ToHashSet();

        var requestedStudentIds = dto.Records
            .Select(record => int.TryParse(record.Id, out var studentId) ? studentId : 0)
            .Where(studentId => studentId > 0)
            .Distinct()
            .ToList();

        if (requestedStudentIds.Count == 0)
        {
            return Ok(new
            {
                success = true,
                savedCount = 0,
                resetCount = 0,
                message = "لا توجد سجلات صالحة للحفظ."
            });
        }

        if (requestedStudentIds.Any(studentId => !allowedStudentIds.Contains(studentId)))
        {
            return BadRequest(new
            {
                success = false,
                message = "يوجد طالب واحد على الأقل لا ينتمي إلى هذه الحصة."
            });
        }

        var existingAttendances = await _context.Attendances
            .Where(attendance => attendance.SessionId == sessionId && requestedStudentIds.Contains(attendance.StudentId))
            .ToDictionaryAsync(attendance => attendance.StudentId);

        var capturedAt = DateTime.UtcNow;
        var savedCount = 0;
        var resetCount = 0;

        foreach (var record in dto.Records)
        {
            if (!int.TryParse(record.Id, out var studentId) || !allowedStudentIds.Contains(studentId))
            {
                continue;
            }

            var status = NormalizeManualAttendanceStatus(record.Status, record.IsPresent);
            if (string.Equals(status, "Unrecorded", StringComparison.OrdinalIgnoreCase))
            {
                if (existingAttendances.TryGetValue(studentId, out var existingRecord))
                {
                    _context.Attendances.Remove(existingRecord);
                    existingAttendances.Remove(studentId);
                    resetCount++;
                }

                continue;
            }

            var isPresent = string.Equals(status, "Present", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Late", StringComparison.OrdinalIgnoreCase);

            if (!existingAttendances.TryGetValue(studentId, out var existingAttendance))
            {
                existingAttendance = new School.Domain.Entities.Attendance
                {
                    StudentId = studentId,
                    SessionId = sessionId
                };

                _context.Attendances.Add(existingAttendance);
                existingAttendances[studentId] = existingAttendance;
            }

            existingAttendance.Status = status;
            existingAttendance.IsPresent = isPresent;
            existingAttendance.RecordedAt = capturedAt;
            existingAttendance.Time = capturedAt;
            existingAttendance.Method = "Manual";
            existingAttendance.Notes = record.Notes ?? string.Empty;
            savedCount++;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            savedCount,
            resetCount,
            message = "تم حفظ الرصد اليدوي بنجاح."
        });
    }

    [HttpPost("enroll-face")]
    [Authorize(Roles = "Teacher,Admin,Student")]
    public async Task<ActionResult> EnrollFace([FromForm] int studentId, [FromForm] IFormFile? file, [FromForm] IFormFile? image)
    {
        var uploadedFile = file ?? image;
        if (uploadedFile == null || uploadedFile.Length == 0)
        {
            return BadRequest(new { success = false, message = "لم يتم إرسال صورة." });
        }

        var studentDb = await _context.Students.FindAsync(studentId);
        if (studentDb == null)
        {
            return NotFound(new { success = false, message = "الطالب غير موجود." });
        }

        if (User.IsInRole("Student"))
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = await _context.Students.FirstOrDefaultAsync(currentStudent => currentStudent.Email == userEmail);
            if (student == null || student.Id != studentId)
            {
                return Unauthorized(new { success = false, message = "لا يمكنك تسجيل وجه لطالب آخر." });
            }
        }

        using var stream = new MemoryStream();
        await uploadedFile.CopyToAsync(stream);
        var bytes = stream.ToArray();

        var trainResult = await _faceRecognition.TrainFaceAsync(studentId, bytes, uploadedFile.FileName);
        if (!trainResult.Success)
        {
            return BadRequest(new
            {
                success = false,
                message = trainResult.Message ?? "تعذر تسجيل الوجه. تأكد من وضوح الصورة أو من توفر خدمة التعرف على الوجه."
            });
        }

        string? profilePicUrl = null;
        var profileUpdated = false;

        try
        {
            profilePicUrl = await _storage.UploadFileAsync(uploadedFile, "StudentProfiles");

            if (!string.IsNullOrWhiteSpace(profilePicUrl))
            {
                studentDb.ProfilePictureUrl = profilePicUrl;
                await _context.SaveChangesAsync();
                profileUpdated = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Face enrollment succeeded for student {StudentId}, but profile picture upload failed.", studentId);
        }

        return Ok(new
        {
            success = true,
            profilePicUrl,
            profileUpdated,
            message = profileUpdated
                ? "تم تسجيل بصمة الوجه بنجاح وتحديث الصورة الشخصية."
                : (trainResult.Message ?? "تم تسجيل بصمة الوجه بنجاح.")
        });
    }
}

public class ManualAttendanceDto
{
    public string ClassId { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public List<ManualAttendanceRecordDto> Records { get; set; } = [];
}

public class ManualAttendanceRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPresent { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

sealed record AttendanceWindowState(
    string Status,
    string Message,
    bool CanRecord,
    DateTime WindowStart,
    DateTime WindowEnd);
