using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Application.Features.Chat.Queries;
using School.Application.Features.Chat.Commands;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class ChatController : BaseApiController
{
    private readonly SchoolDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public ChatController(SchoolDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env)
    {
        _context = context;
        _userManager = userManager;
        _env = env;
    }

    [HttpGet("history/{otherUserId}")]
    public async Task<ActionResult<List<MessageDto>>> GetChatHistory(string otherUserId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        var query = new GetChatHistoryQuery
        {
            UserId1 = currentUserId,
            UserId2 = otherUserId
        };

        var messages = await Mediator.Send(query);
        return Ok(messages);
    }

    [HttpPost("send")]
    public async Task<ActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var senderId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(senderId)) return Unauthorized();

        var command = new SendMessageCommand
        {
            SenderId = senderId,
            ReceiverId = dto.ReceiverId,
            Content = dto.Content,
            FileUrl = dto.FileUrl,
            FileName = dto.FileName,
            FileType = dto.FileType,
            FileSize = dto.FileSize,
            MessageType = dto.MessageType ?? "text"
        };

        var messageId = await Mediator.Send(command);
        if (messageId > 0)
            return Ok(new { success = true, messageId });
        return BadRequest(new { success = false });
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50MB max
    public async Task<ActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        var uploadsDir = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "chat");
        Directory.CreateDirectory(uploadsDir);

        var fileExt = Path.GetExtension(file.FileName);
        var uniqueName = $"{Guid.NewGuid()}{fileExt}";
        var filePath = Path.Combine(uploadsDir, uniqueName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var fileUrl = $"/uploads/chat/{uniqueName}";
        var fileType = GetFileType(fileExt);

        return Ok(new
        {
            fileUrl,
            fileName = file.FileName,
            fileType,
            fileSize = file.Length
        });
    }

    [HttpGet("contacts")]
    public async Task<ActionResult<IEnumerable<object>>> GetContacts()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "";

        var contacts = new List<object>();

        if (role == "Student")
        {
            // Student -> chat with teachers of their classes (Homeroom + Subject teachers)
            var student = await _context.Students
                .Include(s => s.ClassRoom)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student?.ClassRoomId != null)
            {
                var addedIds = new HashSet<string>();

                if (student.ClassRoom?.TeacherId != null)
                {
                    var teacher = await _context.Teachers.FindAsync(student.ClassRoom.TeacherId.Value);
                    if (teacher != null && !string.IsNullOrEmpty(teacher.UserId))
                    {
                        var userTeacher = await _userManager.FindByIdAsync(teacher.UserId);
                        if (userTeacher != null && addedIds.Add(userTeacher.Id))
                        {
                            contacts.Add(new
                            {
                                id = userTeacher.Id,
                                name = teacher.FullName ?? userTeacher.FullName ?? "معلم الفصل",
                                role = "Teacher",
                                lastMessage = await GetLastMessage(userId, userTeacher.Id),
                                lastMessageTime = await GetLastMessageTime(userId, userTeacher.Id),
                                unreadCount = await GetUnreadCount(userId, userTeacher.Id),
                                isOnline = false
                            });
                        }
                    }
                }

                // 2. All Subject Teachers for this student's class
                var subjectTeachers = await _context.Subjects
                    .Where(s => s.ClassRoomId == student.ClassRoomId)
                    .Include(s => s.Teacher)
                    .Select(s => s.Teacher)
                    .Distinct()
                    .ToListAsync();

                foreach (var teacher in subjectTeachers)
                {
                    if (teacher == null || string.IsNullOrEmpty(teacher.UserId)) continue;
                    var userTeacher = await _userManager.FindByIdAsync(teacher.UserId);
                    if (userTeacher != null && addedIds.Add(userTeacher.Id))
                    {
                        contacts.Add(new
                        {
                            id = userTeacher.Id,
                            name = teacher.FullName ?? userTeacher.FullName ?? "مدرس مادة",
                            role = "Teacher",
                            lastMessage = await GetLastMessage(userId, userTeacher.Id),
                            lastMessageTime = await GetLastMessageTime(userId, userTeacher.Id),
                            unreadCount = await GetUnreadCount(userId, userTeacher.Id),
                            isOnline = false
                        });
                    }
                }
            }
        }
        else if (role == "Teacher")
        {
            // Teacher -> chat with students and parents of their classes
            var teacher = await _context.Teachers
                .Include(t => t.ClassRooms)
                    .ThenInclude(c => c.Students)
                        .ThenInclude(s => s.Parent)
                .FirstOrDefaultAsync(t => t.UserId == userId);

            if (teacher != null && teacher.ClassRooms != null)
            {
                var addedIds = new HashSet<string>();
                foreach (var cl in teacher.ClassRooms)
                {
                    foreach (var st in cl.Students)
                    {
                        // Add Student
                        var stUser = await _userManager.FindByIdAsync(st.UserId);
                        if (stUser != null && addedIds.Add(stUser.Id))
                        {
                            contacts.Add(new
                            {
                                id = stUser.Id,
                                name = st.FullName ?? stUser.FullName,
                                studentName = st.FullName ?? stUser.FullName,
                                role = "Student",
                                className = cl.Name,
                                lastMessage = await GetLastMessage(userId, stUser.Id),
                                lastMessageTime = await GetLastMessageTime(userId, stUser.Id),
                                unreadCount = await GetUnreadCount(userId, stUser.Id),
                                isOnline = false
                            });
                        }

                        // Add Parent of student
                        if (st.Parent != null)
                        {
                            var parentUser = await _userManager.FindByIdAsync(st.Parent.UserId);
                            if (parentUser != null && addedIds.Add(parentUser.Id))
                            {
                                contacts.Add(new
                                {
                                    id = parentUser.Id,
                                    name = st.Parent.FullName ?? parentUser.FullName ?? "ولي الأمر",
                                    studentName = st.FullName ?? stUser?.FullName ?? "",
                                    role = "Parent",
                                    className = cl.Name,
                                    lastMessage = await GetLastMessage(userId, parentUser.Id),
                                    lastMessageTime = await GetLastMessageTime(userId, parentUser.Id),
                                    unreadCount = await GetUnreadCount(userId, parentUser.Id),
                                    isOnline = false
                                });
                            }
                        }
                    }
                }
            }
        }
        else if (role == "Parent")
        {
            // Parent -> chat with teachers of their children (Homeroom + Subject teachers)
            var parent = await _context.Parents
                .Include(p => p.Children)
                    .ThenInclude(s => s.ClassRoom)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (parent != null && parent.Children != null)
            {
                var addedIds = new HashSet<string>();
                foreach (var child in parent.Children)
                {
                    // 1. Homeroom Teacher
                    if (child.ClassRoom?.TeacherId != null)
                    {
                        var teacher = await _context.Teachers.FindAsync(child.ClassRoom.TeacherId.Value);
                        if (teacher != null)
                        {
                            var userTeacher = await _userManager.FindByIdAsync(teacher.UserId);
                            if (userTeacher != null && addedIds.Add(userTeacher.Id))
                            {
                                contacts.Add(new
                                {
                                    id = userTeacher.Id,
                                    name = teacher.FullName ?? userTeacher.FullName ?? "معلم الفصل",
                                    studentName = child.FullName,
                                    role = "Teacher",
                                    lastMessage = await GetLastMessage(userId, userTeacher.Id),
                                    lastMessageTime = await GetLastMessageTime(userId, userTeacher.Id),
                                    unreadCount = await GetUnreadCount(userId, userTeacher.Id),
                                    isOnline = false
                                });
                            }
                        }
                    }

                    // 2. Subject Teachers for this student's class
                    if (child.ClassRoomId != null)
                    {
                        var subjectTeachers = await _context.Subjects
                            .Where(s => s.ClassRoomId == child.ClassRoomId)
                            .Include(s => s.Teacher)
                            .Select(s => s.Teacher)
                            .Distinct()
                            .ToListAsync();

                        foreach (var teacher in subjectTeachers)
                        {
                            if (teacher == null) continue;
                            var userTeacher = await _userManager.FindByIdAsync(teacher.UserId);
                            if (userTeacher != null && addedIds.Add(userTeacher.Id))
                            {
                                contacts.Add(new
                                {
                                    id = userTeacher.Id,
                                    name = teacher.FullName ?? userTeacher.FullName ?? "مدرس مادة",
                                    studentName = child.FullName,
                                    role = "Teacher",
                                    lastMessage = await GetLastMessage(userId, userTeacher.Id),
                                    lastMessageTime = await GetLastMessageTime(userId, userTeacher.Id),
                                    unreadCount = await GetUnreadCount(userId, userTeacher.Id),
                                    isOnline = false
                                });
                            }
                        }
                    }
                }
            }
        }
        else if (role == "Admin")
        {
            // Admin can chat with all teachers
            var allTeachers = await _context.Teachers.ToListAsync();
            foreach (var teacher in allTeachers)
            {
                var userTeacher = await _userManager.FindByIdAsync(teacher.UserId);
                if (userTeacher != null)
                {
                    contacts.Add(new
                    {
                        id = userTeacher.Id,
                        name = teacher.FullName ?? userTeacher.FullName ?? "معلم",
                        studentName = "",
                        role = "Teacher",
                        lastMessage = await GetLastMessage(userId, userTeacher.Id),
                        lastMessageTime = await GetLastMessageTime(userId, userTeacher.Id),
                        unreadCount = await GetUnreadCount(userId, userTeacher.Id),
                        isOnline = false
                    });
                }
            }
        }

        return Ok(contacts);
    }

    [HttpPost("mark-read/{otherUserId}")]
    public async Task<ActionResult> MarkAsRead(string otherUserId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var unreadMessages = await _context.Messages
            .Where(m => m.SenderId == otherUserId && m.ReceiverId == currentUserId && !m.IsRead)
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
        }
        await _context.SaveChangesAsync();
        return Ok(new { markedCount = unreadMessages.Count });
    }

    [HttpDelete("message/{messageId}")]
    public async Task<ActionResult> DeleteMessage(int messageId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var msg = await _context.Messages.FindAsync(messageId);
        if (msg == null) return NotFound();
        if (msg.SenderId != userId) return Forbid();

        msg.IsDeleted = true;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("search")]
    public async Task<ActionResult> SearchMessages([FromQuery] string query, [FromQuery] string? contactId = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(query)) return Ok(new List<object>());

        var q = _context.Messages
            .Where(m => !m.IsDeleted &&
                        ((m.SenderId == userId) || (m.ReceiverId == userId)) &&
                        m.Content.Contains(query));

        if (!string.IsNullOrEmpty(contactId))
        {
            q = q.Where(m => (m.SenderId == contactId || m.ReceiverId == contactId));
        }

        var results = await q
            .OrderByDescending(m => m.SentAt)
            .Take(50)
            .Select(m => new
            {
                m.Id,
                m.SenderId,
                m.ReceiverId,
                m.Content,
                m.SentAt,
                m.MessageType
            })
            .ToListAsync();

        return Ok(results);
    }

    // Helper methods
    private async Task<string> GetLastMessage(string userId1, string userId2)
    {
        var msg = await _context.Messages
            .Where(m => !m.IsDeleted &&
                        ((m.SenderId == userId1 && m.ReceiverId == userId2) ||
                        (m.SenderId == userId2 && m.ReceiverId == userId1)))
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();
        
        if (msg == null) return "";
        if (msg.MessageType == "audio") return "🎤 رسالة صوتية";
        if (msg.MessageType == "image") return "📷 صورة";
        if (msg.MessageType == "file") return "📎 ملف";
        return msg.Content ?? "";
    }

    private async Task<DateTime?> GetLastMessageTime(string userId1, string userId2)
    {
        var msg = await _context.Messages
            .Where(m => !m.IsDeleted &&
                        ((m.SenderId == userId1 && m.ReceiverId == userId2) ||
                        (m.SenderId == userId2 && m.ReceiverId == userId1)))
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();
        return msg?.SentAt;
    }

    private async Task<int> GetUnreadCount(string currentUserId, string otherUserId)
    {
        return await _context.Messages
            .CountAsync(m => m.SenderId == otherUserId && m.ReceiverId == currentUserId && !m.IsRead && !m.IsDeleted);
    }

    private static string GetFileType(string extension)
    {
        extension = extension.ToLower();
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => "image",
            ".mp3" or ".wav" or ".ogg" or ".m4a" or ".webm" => "audio",
            ".pdf" => "pdf",
            ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" => "document",
            _ => "file"
        };
    }
}

public class SendMessageDto
{
    public string ReceiverId { get; set; }
    public string Content { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string? MessageType { get; set; }
}
