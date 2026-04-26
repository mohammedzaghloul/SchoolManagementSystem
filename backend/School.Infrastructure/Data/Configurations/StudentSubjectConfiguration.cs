using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using School.Domain.Entities;

namespace School.Infrastructure.Data.Configurations;

public class StudentSubjectConfiguration : IEntityTypeConfiguration<StudentSubject>
{
    public void Configure(EntityTypeBuilder<StudentSubject> builder)
    {
        builder.ToTable("StudentSubjects");

        builder.HasKey(item => new { item.StudentId, item.SubjectId });

        builder.HasOne(item => item.Student)
            .WithMany(student => student.StudentSubjects)
            .HasForeignKey(item => item.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Subject)
            .WithMany(subject => subject.StudentSubjects)
            .HasForeignKey(item => item.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
