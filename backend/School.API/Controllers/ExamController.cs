using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
[Route("api/[controller]")]
public class ExamsController : BaseApiController
{
    private readonly School.Infrastructure.Data.SchoolDbContext _context;

    public ExamsController(School.Infrastructure.Data.SchoolDbContext context)
    {
        _context = context;
    }

    [HttpGet("ping")]
    [AllowAnonymous]
    public IActionResult Ping() => Ok(new { message = "Exams API is live!" });

    [HttpGet("results-by-exam/{id:int}")]
    public async Task<IActionResult> GetExamResults(int id)
    {
        var results = await _context.ExamResults
            .Include(r => r.Student)
            .Where(r => r.ExamId == id)
            .Select(r => new {
                r.Id,
                StudentName = r.Student != null ? r.Student.FullName : "Unknown",
                StudentCode = r.Student != null ? (r.Student.QrCodeValue ?? (r.Student.UserId != null && r.Student.UserId.Length > 8 ? r.Student.UserId.Substring(0, 8) : r.Student.UserId ?? "N/A")) : "N/A",
                r.Score,
                r.CreatedAt,
                r.Notes
            })
            .ToListAsync();

        return Ok(results);
    }

    [HttpGet("results-by-student/{studentId:int}")]
    public async Task<IActionResult> GetStudentResults(int studentId)
    {
        var results = await _context.ExamResults
            .Include(r => r.Exam)
                .ThenInclude(e => e.Subject)
            .Where(r => r.StudentId == studentId)
            .Select(r => new {
                r.Id,
                ExamTitle = r.Exam.Title,
                SubjectName = r.Exam.Subject.Name,
                r.Score,
                MaxScore = r.Exam.MaxScore,
                r.CreatedAt,
                r.Notes
            })
            .ToListAsync();

        return Ok(results);
    }

    [HttpGet("student")]
    public async Task<IActionResult> GetStudentExams()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail);
        if (student == null) return NotFound("Student not found");

        var exams = await _context.Exams
            .Include(e => e.Subject)
            .Include(e => e.Teacher)
            .Where(e => e.ClassRoomId == student.ClassRoomId)
            .Select(e => new
            {
                e.Id,
                e.Title,
                SubjectName = e.Subject.Name,
                TeacherName = e.Teacher.FullName,
                e.StartTime,
                e.EndTime,
                e.MaxScore,
                QuestionCount = _context.Questions.Count(q => q.ExamId == e.Id),
                IsCompleted = _context.ExamResults.Any(r => r.ExamId == e.Id && r.StudentId == student.Id),
                Score = _context.ExamResults.Where(r => r.ExamId == e.Id && r.StudentId == student.Id).Select(r => r.Score).FirstOrDefault(),
                Notes = _context.ExamResults.Where(r => r.ExamId == e.Id && r.StudentId == student.Id).Select(r => r.Notes).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(exams);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetExamDetails(int id)
    {
        var exam = await _context.Exams
            .Include(e => e.Subject)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exam == null) return NotFound();

        // Security: Block access if the exam hasn't started yet (Only for Students)
        var userRole = User.FindFirstValue(ClaimTypes.Role);
        if (userRole == "Student" && exam.StartTime > DateTime.Now)
        {
            return BadRequest(new { message = "هذا الاختبار لم يبدأ بعد. يرجى العودة في الموعد المحدد." });
        }

        // Security: Prevent accessing questions if already submitted
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail);
        if (student != null)
        {
            var alreadyFinished = await _context.ExamResults.AnyAsync(r => r.ExamId == id && r.StudentId == student.Id);
            if (alreadyFinished)
            {
                return BadRequest(new { message = "لقد قمت بتأدية هذا الاختبار مسبقاً، لا يمكن الدخول مرة أخرى." });
            }
        }

        var questions = await _context.Questions
            .Include(q => q.Choices)
            .Where(q => q.ExamId == id)
            .ToListAsync();

        return Ok(new {
            exam.Id,
            exam.Title,
            exam.Description,
            exam.ClassRoomId,
            exam.SubjectId,
            exam.StartTime,
            exam.EndTime,
            exam.MaxScore,
            Duration = (int)(exam.EndTime - exam.StartTime).TotalMinutes,
            Type = exam.ExamType,
            questions = questions.Select(q => new {
                q.Id,
                q.Text,
                Marks = q.Score,
                Type = q.Choices.Count == 0 ? "essay" : 
                       (q.Choices.Count == 2 && (q.Choices.Any(c => c.Text == "صح") || q.Choices.Any(c => c.Text == "True"))) ? "true-false" : "multiple-choice",
                Options = q.Choices.Select(c => c.Text).ToList(),
                CorrectAnswer = q.Choices.ToList().FindIndex(c => c.IsCorrect)
            })
        });
    }


    [HttpPost]
    [HttpPost("create")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> CreateExam([FromBody] ExamCreateDto dto)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Email == userEmail);
        if (teacher == null) return NotFound("Teacher not found");

        var exam = new School.Domain.Entities.Exam
        {
            Title = dto.Title,
            Description = dto.Description,
            StartTime = dto.StartTime ?? dto.Date ?? DateTime.Today,
            EndTime = dto.EndTime ?? (dto.StartTime ?? dto.Date ?? DateTime.Today).AddMinutes(dto.Duration),
            MaxScore = dto.TotalMarks,
            ExamType = dto.Type ?? "Quiz",
            TeacherId = teacher.Id,
            ClassRoomId = dto.ClassRoomId,
            SubjectId = dto.SubjectId
        };

        if (dto.Questions != null)
        {
            foreach (var qDto in dto.Questions)
            {
                var question = new School.Domain.Entities.Question
                {
                    Text = qDto.Text,
                    Score = qDto.Marks,
                    Choices = qDto.Options?.Select((opt, index) => new School.Domain.Entities.QuestionChoice
                    {
                        Text = opt,
                        IsCorrect = index == qDto.CorrectAnswer
                    }).ToList() ?? new List<School.Domain.Entities.QuestionChoice>()
                };
                exam.Questions.Add(question);
            }
        }

        _context.Exams.Add(exam);
        await _context.SaveChangesAsync();
        return Ok(new { id = exam.Id, message = "Exam created successfully" });
    }

    public class ExamCreateDto
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int ClassRoomId { get; set; }
        public int SubjectId { get; set; }
        public string? Type { get; set; }
        public DateTime? Date { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Duration { get; set; }
        public int TotalMarks { get; set; }
        public List<QuestionDto>? Questions { get; set; }
    }

    public class QuestionDto
    {
        public string Text { get; set; } = null!;
        public int Marks { get; set; }
        public List<string>? Options { get; set; }
        public int CorrectAnswer { get; set; }
    }

    [HttpGet("teacher")]
    public async Task<IActionResult> GetTeacherExams()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.Email == userEmail);
        if (teacher == null) return NotFound();

        var exams = await _context.Exams
            .Include(e => e.Subject)
            .Include(e => e.ClassRoom)
            .Where(e => e.TeacherId == teacher.Id)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Description,
                SubjectId = e.SubjectId,
                SubjectName = e.Subject != null ? e.Subject.Name : "N/A",
                ClassRoomId = e.ClassRoomId,
                ClassRoomName = e.ClassRoom != null ? e.ClassRoom.Name : "N/A",
                e.StartTime,
                e.EndTime,
                e.MaxScore,
                e.ExamType,
                QuestionCount = _context.Questions.Count(q => q.ExamId == e.Id)
            })
            .ToListAsync();

        return Ok(exams);
    }

    [HttpPost("{id:int}/submit")]
    public async Task<IActionResult> SubmitExam(int id, [FromBody] ExamSubmission submission)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail);
        if (student == null) return NotFound("Student not found");

        var questions = await _context.Questions
            .Include(q => q.Choices)
            .Where(q => q.ExamId == id)
            .ToListAsync();

        var exam = await _context.Exams.FindAsync(id);
        if (exam == null) return NotFound("الامتحان غير موجود");

        // Security: Ensure it's not submitted before start or after end (with grace period)
        if (DateTime.Now < exam.StartTime)
            return BadRequest(new { message = "لا يمكن تسليم الاختبار قبل موعد بدئه." });

        if (DateTime.Now > exam.EndTime.AddMinutes(5)) // 5 mins grace period
            return BadRequest(new { message = "عذراً، انتهى الوقت المسموح به لتسليم الاختبار." });

        // Prevent duplicate submissions
        var existingResult = await _context.ExamResults
            .AnyAsync(r => r.ExamId == id && r.StudentId == student.Id);
            
        if (existingResult)
        {
            return BadRequest(new { message = "لقد قمت بأداء هذا الاختبار مسبقاً، لا يمكن إعادة الحل." });
        }

        int totalScore = 0;
        int earnedScore = 0;

        if (submission.Answers != null)
        {
            foreach (var q in questions)
            {
                totalScore += q.Score;
                var answer = submission.Answers.FirstOrDefault(a => a.QuestionId == q.Id);
                if (answer != null)
                {
                    var correctChoice = q.Choices.FirstOrDefault(c => c.IsCorrect);
                    if (correctChoice != null && correctChoice.Id == answer.ChoiceId)
                    {
                        earnedScore += q.Score;
                    }
                }
            }
        }

        var result = new School.Domain.Entities.ExamResult
        {
            ExamId = id,
            StudentId = student.Id,
            Score = earnedScore,
            Notes = $"تم الحل بنجاح. النتيجة: {earnedScore} من {totalScore}"
        };

        _context.ExamResults.Add(result);
        await _context.SaveChangesAsync();

        return Ok(new { earnedScore, totalScore, resultId = result.Id });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    public async Task<IActionResult> DeleteExam(int id)
    {
        var exam = await _context.Exams.FindAsync(id);
        if (exam == null) return NotFound();

        // Check if there are results for this exam, you might want to handle it
        var results = await _context.ExamResults.Where(r => r.ExamId == id).ToListAsync();
        _context.ExamResults.RemoveRange(results);

        var questions = await _context.Questions.Where(q => q.ExamId == id).ToListAsync();
        foreach(var q in questions)
        {
            var choices = await _context.QuestionChoices.Where(c => c.QuestionId == q.Id).ToListAsync();
            _context.QuestionChoices.RemoveRange(choices);
        }
        _context.Questions.RemoveRange(questions);

        _context.Exams.Remove(exam);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Exam deleted successfully" });
    }
}

public class ExamSubmission
{
    public List<AnswerDto>? Answers { get; set; }
}

public class AnswerDto
{
    public int QuestionId { get; set; }
    public int ChoiceId { get; set; }
}
