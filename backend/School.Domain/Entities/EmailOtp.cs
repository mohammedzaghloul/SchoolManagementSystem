namespace School.Domain.Entities;

public class EmailOtp : BaseEntity
{
    public string Email { get; set; } = null!;
    public string? UserId { get; set; }
    public string Purpose { get; set; } = null!;
    public string CodeHash { get; set; } = null!;
    public string Salt { get; set; } = null!;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public bool IsUsed { get; set; }
    public int FailedAttempts { get; set; }
}
