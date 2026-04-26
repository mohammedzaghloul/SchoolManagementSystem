using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using School.Application.Features.ClassRooms.Commands;
using School.Application.Features.ClassRooms.Queries;
using School.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
[Route("api/ClassRooms")]
[Route("api/ClassRoom")]
public class ClassRoomsController : BaseApiController
{
    private readonly SchoolDbContext _context;

    public ClassRoomsController(SchoolDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ClassRoomDto>>> GetClassRooms()
    {
        var result = await Mediator.Send(new GetClassRoomsQuery()); 
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClassRoomDto>> GetClassRoom(int id)
    {
        // For the demo, we can use GetClassRoomsQuery and filter or assume there's a specific query
        var result = await Mediator.Send(new GetClassRoomsQuery());
        var classroom = result.FirstOrDefault(c => c.Id == id);
        if (classroom == null) return NotFound();
        return Ok(classroom);
    }

    [HttpGet("teacher/{teacherId?}")]
    public async Task<ActionResult<List<ClassRoomDto>>> GetTeacherClasses(int teacherId = 0)
    {
        var currentTeacher = await GetCurrentTeacherAsync();
        var isAdmin = User.IsInRole("Admin");
        var effectiveTeacherId = currentTeacher?.Id ?? (isAdmin ? teacherId : 0);

        if (effectiveTeacherId <= 0)
        {
            return Ok(new List<ClassRoomDto>());
        }

        var hasDirectClassLinks = await _context.ClassRooms
            .AsNoTracking()
            .AnyAsync(classRoom =>
                classRoom.TeacherId == effectiveTeacherId ||
                classRoom.Subjects.Any(subject => subject.TeacherId == effectiveTeacherId));

        var result = await _context.ClassRooms
            .AsNoTracking()
            .Where(classRoom => hasDirectClassLinks
                ? classRoom.TeacherId == effectiveTeacherId ||
                  classRoom.Subjects.Any(subject => subject.TeacherId == effectiveTeacherId)
                : classRoom.Sessions.Any(session => session.TeacherId == effectiveTeacherId))
            .OrderBy(classRoom => classRoom.GradeLevel != null ? classRoom.GradeLevel.Name : string.Empty)
            .ThenBy(classRoom => classRoom.Name)
            .Select(classRoom => new ClassRoomDto
            {
                Id = classRoom.Id,
                Name = classRoom.Name ?? $"Class {classRoom.Id}",
                GradeLevelId = classRoom.GradeLevelId.GetValueOrDefault(),
                Capacity = classRoom.Capacity
            })
            .ToListAsync();

        return Ok(result);
    }

    [HttpGet("{id}/students")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<ActionResult> GetClassRoomStudents(int id)
    {
        var classroomExists = await _context.ClassRooms.AnyAsync(classRoom => classRoom.Id == id);
        if (!classroomExists)
        {
            return NotFound(new { message = "الفصل الدراسي غير موجود." });
        }

        var students = await _context.Students
            .AsNoTracking()
            .Where(student => student.ClassRoomId == id)
            .OrderBy(student => student.FullName)
            .Select(student => new
            {
                student.Id,
                student.FullName,
                student.Email,
                student.Phone,
                student.IsActive
            })
            .ToListAsync();

        return Ok(students);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> CreateClassRoom([FromBody] CreateClassRoomCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name) || command.GradeLevelId <= 0)
        {
            return BadRequest(new { message = "يرجى إدخال اسم الفصل وتحديد المرحلة الدراسية." });
        }

        var id = await Mediator.Send(command);
        return Ok(new { id, message = "تم إنشاء الفصل الدراسي بنجاح." });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> UpdateClassRoom(int id, [FromBody] UpdateClassRoomCommand command)
    {
        command.Id = id;

        if (string.IsNullOrWhiteSpace(command.Name) || (command.GradeLevelId.HasValue && command.GradeLevelId <= 0))
        {
            return BadRequest(new { message = "بيانات الفصل الدراسي غير مكتملة." });
        }

        var updated = await Mediator.Send(command);
        if (!updated)
        {
            return NotFound(new { message = "الفصل الدراسي غير موجود." });
        }

        return Ok(new { message = "تم تحديث الفصل الدراسي بنجاح." });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteClassRoom(int id)
    {
        var deleted = await Mediator.Send(new DeleteClassRoomCommand { Id = id });
        if (!deleted)
        {
            return NotFound(new { message = "الفصل الدراسي غير موجود." });
        }

        return Ok(new { message = "تم حذف الفصل الدراسي بنجاح." });
    }

    private async Task<School.Domain.Entities.Teacher?> GetCurrentTeacherAsync()
    {
        if (!User.IsInRole("Teacher"))
        {
            return null;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return await _context.Teachers
            .AsNoTracking()
            .FirstOrDefaultAsync(teacher => teacher.UserId == userId || teacher.Email == email);
    }
}
