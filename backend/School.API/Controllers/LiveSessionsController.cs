using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Application.Interfaces;
using School.Infrastructure.Data;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class LiveSessionsController : BaseApiController
{
    private readonly SchoolDbContext _context;
    private readonly ILiveSessionService _liveSessionService;
    private readonly IConfiguration _configuration;

    public LiveSessionsController(SchoolDbContext context, ILiveSessionService liveSessionService, IConfiguration configuration)
    {
        _context = context;
        _liveSessionService = liveSessionService;
        _configuration = configuration;
    }

    [HttpPost("start/{sessionId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult> StartSession(int sessionId)
    {
        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null) return NotFound("Session not found");

        session.IsLive = true;
        session.AgoraChannelName = $"session_{sessionId}";
        
        await _context.SaveChangesAsync();

        var appId = _configuration["Agora:AppId"];
        var token = _liveSessionService.GenerateToken(session.AgoraChannelName, 0, 3600);

        return Ok(new
        {
            appId,
            channelName = session.AgoraChannelName,
            token,
            uid = 0
        });
    }

    [HttpGet("join/{sessionId}")]
    public async Task<ActionResult> JoinSession(int sessionId)
    {
        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null || !session.IsLive) return BadRequest("Session is not live");

        var appId = _configuration["Agora:AppId"];
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
        var token = _liveSessionService.GenerateToken(session.AgoraChannelName!, (uint)userId, 3600);

        return Ok(new
        {
            appId,
            channelName = session.AgoraChannelName,
            token,
            uid = userId
        });
    }

    [HttpPost("end/{sessionId}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<ActionResult> EndSession(int sessionId)
    {
        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null) return NotFound();

        session.IsLive = false;
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}
