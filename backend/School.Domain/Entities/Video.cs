using System;
using System.Collections.Generic;

namespace School.Domain.Entities;

public class Video : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public int Views { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsHidden { get; set; } = false;

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public int? GradeLevelId { get; set; }
    public GradeLevel? GradeLevel { get; set; }

    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
}
