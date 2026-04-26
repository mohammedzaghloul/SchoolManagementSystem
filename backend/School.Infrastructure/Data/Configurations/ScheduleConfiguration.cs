using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using School.Domain.Entities;

namespace School.Infrastructure.Data.Configurations;

public class ScheduleConfiguration : IEntityTypeConfiguration<Schedule>
{
    public void Configure(EntityTypeBuilder<Schedule> builder)
    {
        builder.ToTable("Schedules");

        builder.HasKey(schedule => schedule.Id);

        builder.Property(schedule => schedule.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(schedule => schedule.AttendanceType)
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne(schedule => schedule.Teacher)
            .WithMany(teacher => teacher.Schedules)
            .HasForeignKey(schedule => schedule.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(schedule => schedule.Subject)
            .WithMany(subject => subject.Schedules)
            .HasForeignKey(schedule => schedule.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(schedule => schedule.ClassRoom)
            .WithMany(classRoom => classRoom.Schedules)
            .HasForeignKey(schedule => schedule.ClassRoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(schedule => new
        {
            schedule.TeacherId,
            schedule.SubjectId,
            schedule.ClassRoomId,
            schedule.DayOfWeek,
            schedule.StartTime,
            schedule.EndTime,
            schedule.TermStartDate
        }).IsUnique();
    }
}
