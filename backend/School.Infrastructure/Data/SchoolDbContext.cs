using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using School.Domain.Entities;
using School.Infrastructure.Identity;

namespace School.Infrastructure.Data;

public class SchoolDbContext : IdentityDbContext<ApplicationUser>
{
    public SchoolDbContext(DbContextOptions<SchoolDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students { get; set; }
    public DbSet<Parent> Parents { get; set; }
    public DbSet<Teacher> Teachers { get; set; }
    public DbSet<ClassRoom> ClassRooms { get; set; }
    public DbSet<GradeLevel> GradeLevels { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Exam> Exams { get; set; }
    public DbSet<ExamResult> ExamResults { get; set; }
    public DbSet<GradeRecord> GradeRecords { get; set; }
    public DbSet<GradeUploadConfirmation> GradeUploadConfirmations { get; set; }
    public DbSet<EmailOtp> EmailOtps { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Schedule> Schedules { get; set; }
    public DbSet<StudentSubject> StudentSubjects { get; set; }
    public DbSet<Video> Videos { get; set; }
    public DbSet<Assignment> Assignments { get; set; }
    public DbSet<AssignmentSubmission> AssignmentSubmissions { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionChoice> QuestionChoices { get; set; }
    public DbSet<Announcement> Announcements { get; set; }
    public DbSet<TuitionInvoice> TuitionInvoices { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(SchoolDbContext).Assembly);

        // Configure Student -> Parent relationship
        builder.Entity<Student>()
            .HasOne(s => s.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(s => s.ParentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure ClassRoom relationships
        builder.Entity<ClassRoom>()
            .HasOne(c => c.Teacher)
            .WithMany(t => t.ClassRooms)
            .HasForeignKey(c => c.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Student>()
            .HasOne(s => s.ClassRoom)
            .WithMany(c => c.Students)
            .HasForeignKey(s => s.ClassRoomId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Student>()
            .HasIndex(s => s.FullName);

        builder.Entity<Student>()
            .HasIndex(s => s.Email);

        builder.Entity<Student>()
            .HasIndex(s => s.QrCodeValue);

        // Configure Session relationships
        builder.Entity<Session>()
            .HasOne(s => s.Subject)
            .WithMany(sub => sub.Sessions)
            .HasForeignKey(s => s.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Session>()
            .HasOne(s => s.Schedule)
            .WithMany(schedule => schedule.Sessions)
            .HasForeignKey(s => s.ScheduleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure Attendance relationships
        builder.Entity<Attendance>()
            .HasOne(a => a.Student)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Attendance>()
            .HasOne(a => a.Session)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TuitionInvoice>()
            .HasOne(t => t.Student)
            .WithMany(s => s.TuitionInvoices)
            .HasForeignKey(t => t.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TuitionInvoice>()
            .Property(t => t.Amount)
            .HasColumnType("decimal(18,2)");

        builder.Entity<TuitionInvoice>()
            .Property(t => t.AmountPaid)
            .HasColumnType("decimal(18,2)");
            
        // Configure Exams 
        builder.Entity<Exam>()
            .HasOne(e => e.Teacher)
            .WithMany(t => t.Exams)
            .HasForeignKey(e => e.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ExamResult>()
            .HasOne(er => er.Exam)
            .WithMany(e => e.ExamResults)
            .HasForeignKey(er => er.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<GradeUploadConfirmation>(entity =>
        {
            entity.Property(item => item.GradeType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(item => item.Date)
                .HasColumnType("date");

            entity.HasIndex(item => new { item.TeacherId, item.SubjectId, item.GradeType, item.Date })
                .IsUnique();

            entity.HasOne(item => item.Teacher)
                .WithMany()
                .HasForeignKey(item => item.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.Subject)
                .WithMany()
                .HasForeignKey(item => item.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
