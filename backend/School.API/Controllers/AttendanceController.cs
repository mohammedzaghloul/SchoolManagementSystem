using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Application.Features.Attendance.Commands;
using School.Application.Interfaces;
using School.Infrastructure.Data;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class AttendanceController : BaseApiController
{
    private readonly IQrCodeService _qrCodeService;
    private readonly SchoolDbContext _context;
    private readonly IFaceRecognitionService _faceRecognition;
    private readonly IFileStorageService _storage;

    public AttendanceController(IQrCodeService qrCodeService, SchoolDbContext context, IFaceRecognitionService faceRecognition, IFileStorageService storage)
    {
        _qrCodeService = qrCodeService;
        _context = context;
        _faceRecognition = faceRecognition;
        _storage = storage;
    }

    private async Task<School.Domain.Entities.Student?> GetCurrentStudentAsync()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return null;
        }

        return await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail);
    }

    [HttpPost("scan-qr")]
    public async Task<ActionResult<bool>> ScanQr(ScanQrCommand command)
    {
        // 1. If StudentId is not provided, get it from current logged in user
        if (command.StudentId == 0)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail);
            if (student == null) return Unauthorized("Student not found.");
            command.StudentId = student.Id;
        }

        // 2. Decode the token to get the SessionId if missing
        try 
        {
            var decodedString = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(command.QrToken));
            var parts = decodedString.Split('|');
            var payloadParts = parts[0].Split(':');
            command.SessionId = int.Parse(payloadParts[0]);
        }
        catch 
        {
            return BadRequest("Invalid QR Code format.");
        }

        var result = await Mediator.Send(command);
        if (!result) return BadRequest("Invalid or expired QR token.");
        
        return Ok(result);
    }

    [HttpGet("generate-qr/{sessionId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public ActionResult<string> GenerateQrToken(int sessionId)
    {
        var token = _qrCodeService.GenerateQrToken(sessionId);
        return Ok(new { Token = token });
    }

    [HttpGet("session/{sessionId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult> GetSessionAttendance(int sessionId)
    {
        var records = await _context.Attendances
            .Include(a => a.Student)
            .Where(a => a.SessionId == sessionId)
            .Select(a => new
            {
                studentId = a.StudentId,
                studentName = a.Student.FullName,
                status = a.Status,
                recordedAt = a.RecordedAt
            })
            .ToListAsync();

        return Ok(records);
    }

    [HttpGet("student/{studentId}")]
    public async Task<ActionResult> GetStudentAttendance(int studentId)
    {
        var records = await _context.Attendances
            .Where(a => a.StudentId == studentId)
            .Include(a => a.Session)
            .Select(a => new
            {
                sessionId = a.SessionId,
                sessionName = a.Session != null ? a.Session.Title : "",
                status = a.Status,
                date = a.RecordedAt
            })
            .OrderByDescending(a => a.date)
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
            .Where(a => a.StudentId == student.Id)
            .Include(a => a.Session)
                .ThenInclude(s => s.Subject)
            .Include(a => a.Session)
                .ThenInclude(s => s.ClassRoom)
            .OrderByDescending(a => a.RecordedAt)
            .Select(a => new
            {
                sessionId = a.SessionId,
                sessionName = a.Session != null ? a.Session.Title : "",
                subjectName = a.Session != null && a.Session.Subject != null ? a.Session.Subject.Name : "",
                classRoomName = a.Session != null && a.Session.ClassRoom != null ? a.Session.ClassRoom.Name : "",
                status = a.Status,
                isPresent = a.IsPresent,
                method = a.Method,
                recordedAt = a.RecordedAt
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
        var total = await _context.Attendances.CountAsync(a => a.StudentId == studentId);
        var present = await _context.Attendances.CountAsync(a => a.StudentId == studentId && a.Status == "Present");
        var absent = await _context.Attendances.CountAsync(a => a.StudentId == studentId && a.Status == "Absent");
        var late = await _context.Attendances.CountAsync(a => a.StudentId == studentId && a.Status == "Late");

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

        var total = await _context.Attendances.CountAsync(a => a.StudentId == student.Id);
        var present = await _context.Attendances.CountAsync(a => a.StudentId == student.Id && a.Status == "Present");
        var absent = await _context.Attendances.CountAsync(a => a.StudentId == student.Id && a.Status == "Absent");
        var late = await _context.Attendances.CountAsync(a => a.StudentId == student.Id && a.Status == "Late");
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
    public async Task<ActionResult> FaceAttendance(int sessionId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { success = false, message = "لم يتم إرسال صورة" });

        // 1. Get the session info
        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null)
            return NotFound(new { success = false, message = "السكشن غير موجود" });

        // 2. Perform Face Recognition
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        int recognizedStudentId = 0;
        bool recognized = false;

        try 
        {
            var faceResult = await _faceRecognition.RecognizeFaceAsync(bytes, file.FileName);
            if (faceResult.Success)
            {
                recognizedStudentId = faceResult.StudentId;
                recognized = true;
            }
        }
        catch (Exception ex)
        {
            // Log error but maybe provide a fallback or sensible error message
            // If the AI service is down, we might want to inform the user
        }

        // 3. Fallback/Mock logic for testing if recognition fails or service is down
        if (!recognized)
        {
             // For DEMO purposes: If not recognized, we can try to find the student in the session who isn't present
             // but it's better to tell the user that "Recognition Failed"
             return Ok(new { success = false, message = "لم يتم التعرف على الوجه. يرجى المحاولة مرة أخرى أو التأكد من إضاءة المكان." });
        }

        // 4. Record Attendance
        var student = await _context.Students.FindAsync(recognizedStudentId);
        if (student == null)
             return BadRequest(new { success = false, message = "الطالب غير مسجل في النظام" });

        // Check if already recorded
        var existing = await _context.Attendances
            .AnyAsync(a => a.SessionId == sessionId && a.StudentId == recognizedStudentId);

        if (existing)
             return Ok(new { success = true, alreadyPresent = true, studentName = student.FullName, message = "تم تسجيل حضورك مسبقاً" });

        var attendance = new School.Domain.Entities.Attendance
        {
            StudentId = student.Id,
            SessionId = sessionId,
            Status = "Present",
            IsPresent = true,
            RecordedAt = DateTime.UtcNow,
            Method = "Face"
        };

        _context.Attendances.Add(attendance);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            recognized = true,
            studentName = student.FullName,
            studentId = student.Id,
            message = "تم تسجيل الحضور بنجاح"
        });
    }

    [HttpPost("manual")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult> ManualAttendance([FromBody] ManualAttendanceDto dto)
    {
        if (dto.Records == null || dto.Records.Count == 0)
            return BadRequest("لا توجد سجلات");

        // ClassId is actually the SessionId sent from frontend
        int sessionId = 0;
        int.TryParse(dto.ClassId, out sessionId);

        foreach (var record in dto.Records)
        {
            var studentId = int.Parse(record.Id);
            var existing = await _context.Attendances
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.SessionId == sessionId);

            if (existing == null)
            {
                _context.Attendances.Add(new School.Domain.Entities.Attendance
                {
                    StudentId = studentId,
                    SessionId = sessionId,
                    Status = record.IsPresent ? "Present" : "Absent",
                    IsPresent = record.IsPresent,
                    RecordedAt = DateTime.UtcNow,
                    Method = "Manual"
                });
            }
            else
            {
                existing.Status = record.IsPresent ? "Present" : "Absent";
                existing.IsPresent = record.IsPresent;
                existing.RecordedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "تم حفظ الحضور بنجاح" });
    }

    [HttpPost("enroll-face")]
    [Authorize(Roles = "Teacher,Admin,Student")]
    public async Task<ActionResult> EnrollFace([FromForm] int studentId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("لم يتم إرسال صورة");

        // If Student role, ensure they are only enrolling themselves
        if (User.IsInRole("Student"))
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail);
            if (student == null || student.Id != studentId)
                return Unauthorized("لا يمكنك تسجيل وجه لطالب آخر");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var success = await _faceRecognition.TrainFaceAsync(studentId, bytes, file.FileName);
        
        if (!success)
            return BadRequest("فشل تسجيل الوجه. تأكد من وضوح الصورة ووجود وجه واحد فقط.");

        // Upload to Cloudinary for profile picture
        string profilePicUrl = await _storage.UploadFileAsync(file, "StudentProfiles");
        
        var studentDb = await _context.Students.FindAsync(studentId);
        if (studentDb != null)
        {
            studentDb.ProfilePictureUrl = profilePicUrl;
            await _context.SaveChangesAsync();
        }

        return Ok(new { success = true, profilePicUrl, message = "تم تسجيل بصمة الوجه بنجاح وتحديث الصورة الشخصية" });
    }
}

public class ManualAttendanceDto
{
    public string ClassId { get; set; }
    public string SubjectId { get; set; }
    public List<ManualAttendanceRecordDto> Records { get; set; }
}

public class ManualAttendanceRecordDto
{
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsPresent { get; set; }
    public string Notes { get; set; }
}
