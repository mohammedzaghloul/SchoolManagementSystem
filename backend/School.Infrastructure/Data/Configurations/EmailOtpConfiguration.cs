using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using School.Domain.Entities;

namespace School.Infrastructure.Data.Configurations;

public class EmailOtpConfiguration : IEntityTypeConfiguration<EmailOtp>
{
    public void Configure(EntityTypeBuilder<EmailOtp> builder)
    {
        builder.ToTable("EmailOtps");

        builder.HasKey(otp => otp.Id);

        builder.Property(otp => otp.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(otp => otp.UserId)
            .HasMaxLength(450);

        builder.Property(otp => otp.Purpose)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(otp => otp.CodeHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(otp => otp.Salt)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(otp => new { otp.Email, otp.Purpose, otp.IsUsed, otp.ExpiresAtUtc });
    }
}
