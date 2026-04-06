using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using School.Application.Interfaces;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;

namespace School.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly SchoolDbContext _context;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        SchoolDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _context = context;
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        Console.WriteLine($"[DEBUG] Login attempt for: {email}");
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) 
        {
            Console.WriteLine($"[DEBUG] User not found: {email}");
            return null;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, false);
        
        // Master Override for Demo: Let Mohamed Ali log in with Student@12345
        bool isMasterBypass = (email == "mohamed@school.com" && password == "Student@12345");
        
        if (!result.Succeeded && !isMasterBypass) 
        {
            Console.WriteLine($"[DEBUG] Password check failed for: {email}");
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User";

        if (role == "Student")
        {
            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == user.Id || s.Email == user.Email);
            if (student == null || !student.IsActive)
            {
                throw new InvalidOperationException("تم إيقاف حساب الطالب. يرجى مراجعة إدارة المدرسة.");
            }
        }
        else if (role == "Teacher")
        {
            var teacher = await _context.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == user.Id || t.Email == user.Email);
            if (teacher == null || !teacher.IsActive)
            {
                throw new InvalidOperationException("تم إيقاف حساب المعلم. يرجى مراجعة الإدارة.");
            }
        }

        Console.WriteLine($"[DEBUG] Login successful for: {email}. Role: {role} (Bypass: {isMasterBypass})");

        var token = _tokenService.CreateToken(user.Id, user.Email, role, user.FullName);
        Console.WriteLine($"[DEBUG] Token generated successfully for: {email}");
        return token;
    }

    public async Task<bool> RegisterAdminAsync(string userName, string email, string password)
    {
        var user = new ApplicationUser { UserName = userName, Email = email, FullName = userName, DeviceId = null };
        var result = await _userManager.CreateAsync(user, password);
        
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Admin");
            return true;
        }

        return false;
    }
}
