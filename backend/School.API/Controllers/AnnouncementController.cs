using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using School.API.Hubs;
using System.Security.Claims;

namespace School.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnnouncementController : ControllerBase
{
    private readonly School.Infrastructure.Data.SchoolDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;

    public AnnouncementController(School.Infrastructure.Data.SchoolDbContext context, IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAnnouncements()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var roleAudience = string.IsNullOrWhiteSpace(role)
            ? Array.Empty<string>()
            : new[] { role, $"{role}s" };
        
        // Admins see everything, others see 'All' or their specific role
        IQueryable<School.Domain.Entities.Announcement> query = _context.Announcements;
        
        if (role != "Admin")
        {
            query = query.Where(a => a.Audience == "All" || roleAudience.Contains(a.Audience));
        }

        var announcements = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(announcements);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAnnouncement(School.Domain.Entities.Announcement announcement)
    {
        announcement.CreatedAt = DateTime.UtcNow;
        announcement.CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Admin";

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();
        
        await _hubContext.Clients.All.SendAsync("ReceiveNotification", announcement.Title, announcement.Content, announcement.Audience);
        
        return Ok(announcement);
    }
}
