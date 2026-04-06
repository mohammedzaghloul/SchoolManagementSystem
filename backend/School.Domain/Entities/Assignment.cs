using System;
using System.Collections.Generic;

namespace School.Domain.Entities;

public class Assignment : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string? AttachmentUrl { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public int ClassRoomId { get; set; }
    public ClassRoom? ClassRoom { get; set; }

    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public ICollection<AssignmentSubmission> Submissions { get; set; } = new List<AssignmentSubmission>();
}

public class AssignmentSubmission : BaseEntity
{
    public DateTime SubmissionDate { get; set; } = DateTime.UtcNow;
    public string? FileUrl { get; set; }
    public string? StudentNotes { get; set; }
    public double? Grade { get; set; }
    public string? TeacherFeedback { get; set; }

    public int AssignmentId { get; set; }
    public Assignment? Assignment { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }
}
