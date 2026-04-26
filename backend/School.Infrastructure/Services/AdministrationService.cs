using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using School.Application.DTOs.Admin;
using School.Application.DTOs.Teacher;
using School.Application.Interfaces;
using School.Domain.Entities;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;

namespace School.Infrastructure.Services;

public class AdministrationService : IAdministrationService
{
    private readonly SchoolDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IScheduleService _scheduleService;

    public AdministrationService(
        SchoolDbContext context,
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IScheduleService scheduleService)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _roleManager = roleManager;
        _scheduleService = scheduleService;
    }

    public async Task<UserSummaryDto> CreateAdminAsync(CreateAdminRequest request, CancellationToken cancellationToken = default)
    {
        var user = await CreateIdentityUserAsync(request.FullName, request.Email, request.Password, null, "Admin");
        return MapUserSummary(user, "Admin", null);
    }

    public async Task<UserSummaryDto> CreateTeacherAsync(CreateTeacherRequest request, CancellationToken cancellationToken = default)
    {
        var user = await CreateIdentityUserAsync(request.FullName, request.Email, request.Password, request.Phone, "Teacher");

        var teacher = new Teacher
        {
            UserId = user.Id,
            FullName = Normalize(request.FullName),
            Email = NormalizeEmail(request.Email),
            Phone = Normalize(request.Phone),
            IsActive = true
        };

        await _unitOfWork.Repository<Teacher>().AddAsync(teacher);
        await _unitOfWork.CompleteAsync();

        user.TeacherId = teacher.Id;
        await UpdateIdentityUserAsync(user);

        return MapUserSummary(user, "Teacher", teacher.Id);
    }

    public async Task<UserSummaryDto> CreateStudentAsync(CreateStudentRequest request, CancellationToken cancellationToken = default)
    {
        var classRoom = await _context.ClassRooms.FirstOrDefaultAsync(item => item.Id == request.ClassRoomId, cancellationToken)
            ?? throw new KeyNotFoundException("Classroom was not found.");

        if (request.ParentId.HasValue)
        {
            var parentExists = await _context.Parents.AnyAsync(item => item.Id == request.ParentId.Value, cancellationToken);
            if (!parentExists)
            {
                throw new KeyNotFoundException("Parent was not found.");
            }
        }

        var user = await CreateIdentityUserAsync(request.FullName, request.Email, request.Password, request.Phone, "Student");
        user.DeviceId ??= Guid.NewGuid().ToString("N");
        await UpdateIdentityUserAsync(user);

        var student = new Student
        {
            UserId = user.Id,
            FullName = Normalize(request.FullName),
            Email = NormalizeEmail(request.Email),
            Phone = Normalize(request.Phone),
            BirthDate = request.BirthDate ?? DateTime.UtcNow.AddYears(-10),
            ClassRoomId = classRoom.Id,
            ParentId = request.ParentId,
            QrCodeValue = Guid.NewGuid().ToString("N"),
            IsActive = true
        };

        await _unitOfWork.Repository<Student>().AddAsync(student);
        await _unitOfWork.CompleteAsync();

        user.StudentId = student.Id;
        await UpdateIdentityUserAsync(user);

        return MapUserSummary(user, "Student", student.Id);
    }

    public async Task<UserSummaryDto> AssignRoleAsync(string userId, AssignRoleRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("User was not found.");

        var requestedRole = Normalize(request.Role);
        if (string.IsNullOrWhiteSpace(requestedRole))
        {
            throw new InvalidOperationException("Role is required.");
        }

        await EnsureRoleExistsAsync(requestedRole);

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", removeResult.Errors.Select(error => error.Description)));
            }
        }

        var addResult = await _userManager.AddToRoleAsync(user, requestedRole);
        if (!addResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", addResult.Errors.Select(error => error.Description)));
        }

        return MapUserSummary(user, requestedRole, user.TeacherId ?? user.StudentId);
    }

    public async Task<OperationResultDto> AssignStudentSubjectsAsync(
        int studentId,
        AssignStudentSubjectsRequest request,
        CancellationToken cancellationToken = default)
    {
        var student = await _context.Students.FirstOrDefaultAsync(item => item.Id == studentId, cancellationToken)
            ?? throw new KeyNotFoundException("Student was not found.");

        var subjectIds = request.SubjectIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (subjectIds.Count == 0)
        {
            throw new InvalidOperationException("At least one subject is required.");
        }

        var subjects = await _context.Subjects
            .Where(subject => subjectIds.Contains(subject.Id))
            .ToListAsync(cancellationToken);

        if (subjects.Count != subjectIds.Count)
        {
            throw new KeyNotFoundException("One or more subjects were not found.");
        }

        var invalidSubject = subjects.FirstOrDefault(subject => subject.ClassRoomId.HasValue && subject.ClassRoomId.Value != student.ClassRoomId);
        if (invalidSubject != null)
        {
            throw new InvalidOperationException("All selected subjects must belong to the student's classroom.");
        }

        var existingAssignments = await _context.StudentSubjects
            .Where(item => item.StudentId == studentId)
            .ToListAsync(cancellationToken);

        var existingSubjectIds = existingAssignments.Select(item => item.SubjectId).ToHashSet();
        var requestedSubjectIds = subjectIds.ToHashSet();

        var toRemove = existingAssignments.Where(item => !requestedSubjectIds.Contains(item.SubjectId)).ToList();
        var toAdd = requestedSubjectIds
            .Where(subjectId => !existingSubjectIds.Contains(subjectId))
            .Select(subjectId => new StudentSubject
            {
                StudentId = studentId,
                SubjectId = subjectId
            })
            .ToList();

        if (toRemove.Count > 0)
        {
            _context.StudentSubjects.RemoveRange(toRemove);
        }

        if (toAdd.Count > 0)
        {
            await _context.StudentSubjects.AddRangeAsync(toAdd, cancellationToken);
        }

        await _unitOfWork.CompleteAsync();

        return new OperationResultDto
        {
            Success = true,
            Message = "Student subjects updated successfully.",
            ProcessedCount = toAdd.Count + toRemove.Count
        };
    }

    public async Task<ScheduleDto> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var (schedule, generatedSessionsCount) = await _scheduleService.CreateAndGenerateAsync(request, cancellationToken);

        return new ScheduleDto
        {
            Id = schedule.Id,
            Title = schedule.Title,
            SubjectName = schedule.Subject?.Name ?? string.Empty,
            TeacherName = schedule.Teacher?.FullName ?? string.Empty,
            ClassRoomName = schedule.ClassRoom?.Name ?? string.Empty,
            DayOfWeek = schedule.DayOfWeek,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            AttendanceType = schedule.AttendanceType,
            TermStartDate = schedule.TermStartDate,
            TermEndDate = schedule.TermEndDate,
            GeneratedSessionsCount = generatedSessionsCount
        };
    }

    private async Task<ApplicationUser> CreateIdentityUserAsync(
        string fullName,
        string email,
        string password,
        string? phone,
        string role)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedName = Normalize(fullName);

        if (string.IsNullOrWhiteSpace(normalizedName) || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new InvalidOperationException("Full name and email are required.");
        }

        if (await _userManager.FindByEmailAsync(normalizedEmail) != null)
        {
            throw new InvalidOperationException("A user with the same email already exists.");
        }

        await EnsureRoleExistsAsync(role);

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = normalizedName,
            PhoneNumber = Normalize(phone),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", createResult.Errors.Select(error => error.Description)));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            throw new InvalidOperationException(string.Join(" ", roleResult.Errors.Select(error => error.Description)));
        }

        return user;
    }

    private async Task UpdateIdentityUserAsync(ApplicationUser user)
    {
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }

    private async Task EnsureRoleExistsAsync(string role)
    {
        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static UserSummaryDto MapUserSummary(ApplicationUser user, string role, int? domainEntityId)
    {
        return new UserSummaryDto
        {
            IdentityUserId = user.Id,
            DomainEntityId = domainEntityId,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = role
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeEmail(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
