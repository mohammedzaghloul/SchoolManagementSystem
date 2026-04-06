using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using School.Infrastructure.Data;

namespace School.API.Filters;

public class RequireActiveAccountFilter : IAsyncActionFilter
{
    private readonly SchoolDbContext _context;

    public RequireActiveAccountFilter(SchoolDbContext context)
    {
        _context = context;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() != null ||
            endpoint?.Metadata.GetMetadata<AllowInactiveAccountAttribute>() != null)
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            await next();
            return;
        }

        var userEmail = user.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            await next();
            return;
        }

        if (user.IsInRole("Student"))
        {
            var isActive = await _context.Students
                .AsNoTracking()
                .Where(student => student.Email == userEmail)
                .Select(student => (bool?)student.IsActive)
                .FirstOrDefaultAsync();

            if (isActive != true)
            {
                context.Result = BuildForbiddenResult("تم إيقاف حساب الطالب. يرجى مراجعة إدارة المدرسة.");
                return;
            }
        }

        if (user.IsInRole("Teacher"))
        {
            var isActive = await _context.Teachers
                .AsNoTracking()
                .Where(teacher => teacher.Email == userEmail)
                .Select(teacher => (bool?)teacher.IsActive)
                .FirstOrDefaultAsync();

            if (isActive != true)
            {
                context.Result = BuildForbiddenResult("تم إيقاف حساب المعلم. يرجى مراجعة الإدارة.");
                return;
            }
        }

        await next();
    }

    private static ObjectResult BuildForbiddenResult(string message)
    {
        return new ObjectResult(new { message })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
