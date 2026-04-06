using System.Collections.Generic;

namespace School.Domain.Entities;

public class Question : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public int Score { get; set; } = 1;
    public string? ImageUrl { get; set; }

    public int ExamId { get; set; }
    public Exam? Exam { get; set; }

    public ICollection<QuestionChoice> Choices { get; set; } = new List<QuestionChoice>();
}

public class QuestionChoice : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }
}
