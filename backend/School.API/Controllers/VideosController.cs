using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Domain.Entities;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class VideosController : BaseApiController
{
    private readonly School.Infrastructure.Data.SchoolDbContext _context;

    public VideosController(School.Infrastructure.Data.SchoolDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetVideos([FromQuery] int? subjectId, [FromQuery] int? gradeLevelId)
    {
        var isTeacher = User.IsInRole("Teacher") || User.IsInRole("Admin");

        var query = _context.Videos
            .Include(v => v.Subject)
            .Include(v => v.GradeLevel)
            .AsQueryable();

        if (subjectId.HasValue)
        {
            query = query.Where(v => v.SubjectId == subjectId.Value);
        }

        if (gradeLevelId.HasValue)
        {
            query = query.Where(v => v.GradeLevelId == gradeLevelId.Value);
        }

        if (!isTeacher)
        {
            // Only show visible videos to students/parents
            query = query.Where(v => !v.IsHidden);
        }

        var videos = await query
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new
            {
                id = v.Id,
                thumbnailUrl = v.ThumbnailUrl,
                duration = v.Duration,
                subject = v.Subject != null ? v.Subject.Name : "عام",
                subjectId = v.SubjectId,
                gradeLevelId = v.GradeLevelId,
                gradeName = v.GradeLevel != null ? v.GradeLevel.Name : "جميع الصفوف",
                views = v.Views,
                title = v.Title,
                description = v.Description,
                url = v.Url,
                isHidden = v.IsHidden
            })
            .ToListAsync();

        // Fix broken thumbnails on-the-fly and map to frontend model
        var fixedVideos = videos.Select(v => {
            var thumb = v.thumbnailUrl;
            
            if (string.IsNullOrWhiteSpace(thumb) || thumb.Contains("example"))
            {
                var youtubeId = ExtractYouTubeId(v.url ?? "");
                if (!string.IsNullOrEmpty(youtubeId))
                {
                    thumb = $"https://img.youtube.com/vi/{youtubeId}/hqdefault.jpg";
                }
                else
                {
                    thumb = $"https://images.unsplash.com/photo-1503676260728-1c00da094a0b?auto=format&fit=crop&q=80&w=400&h=250";
                }
            }
            
            return new {
                v.id,
                thumbnailUrl = thumb,
                thumbnail = thumb, // For model compatibility
                v.duration,
                v.subject,
                v.subjectId,
                v.gradeLevelId,
                v.gradeName,
                v.views,
                v.title,
                v.description,
                v.url,
                v.isHidden
            };
        });

        return Ok(fixedVideos);
    }

    private string? ExtractYouTubeId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var patterns = new[] {
            @"(?:youtube\.com\/watch\?v=|youtu\.be\/)([a-zA-Z0-9_-]{11})",
            @"youtube\.com\/embed\/([a-zA-Z0-9_-]{11})"
        };
        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(url, pattern);
            if (match.Success) return match.Groups[1].Value;
        }
        return null;
    }

    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> AddVideo([FromBody] Video video)
    {
        // Try setting TeacherId from current user
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
        if (teacher != null)
        {
            video.TeacherId = teacher.Id;
        }

        video.CreatedAt = DateTime.UtcNow;
        video.Views = 0;
        
        _context.Videos.Add(video);
        await _context.SaveChangesAsync();
        return Ok(video);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> UpdateVideo(int id, [FromBody] Video video)
    {
        var existing = await _context.Videos.FindAsync(id);
        if (existing == null) return NotFound();

        existing.Title = video.Title;
        existing.Description = video.Description;
        existing.Url = video.Url;
        existing.ThumbnailUrl = video.ThumbnailUrl;
        existing.SubjectId = video.SubjectId;
        existing.GradeLevelId = video.GradeLevelId;
        existing.IsHidden = video.IsHidden;

        await _context.SaveChangesAsync();
        return Ok(existing);
    }

    [HttpPost("{id:int}/view")]
    public async Task<IActionResult> IncrementViews(int id)
    {
        var video = await _context.Videos.FirstOrDefaultAsync(currentVideo => currentVideo.Id == id);
        if (video == null)
        {
            return NotFound(new { message = "الفيديو غير موجود." });
        }

        if (!User.IsInRole("Teacher") && !User.IsInRole("Admin") && video.IsHidden)
        {
            return Forbid();
        }

        video.Views += 1;
        await _context.SaveChangesAsync();

        return Ok(new { views = video.Views });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteVideo(int id)
    {
        var existing = await _context.Videos.FindAsync(id);
        if (existing == null) return NotFound();

        _context.Videos.Remove(existing);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Video Deleted Successfully" });
    }

    [HttpGet("grades")]
    public async Task<IActionResult> GetGrades()
    {
        var grades = await _context.GradeLevels
            .Select(g => new { id = g.Id, name = g.Name })
            .ToListAsync();
        return Ok(grades);
    }
}
