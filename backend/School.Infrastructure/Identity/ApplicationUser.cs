using Microsoft.AspNetCore.Identity;

namespace School.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; }
    public string? ProfilePictureUrl { get; set; }
    // Reference to integer IDs in the Domain layer depending on role
    public int? StudentId { get; set; }
    public int? TeacherId { get; set; }
    public int? ParentId { get; set; }
    
    // DeviceId is used for QR Attendance security (Students only). Nullable for other roles.
    public string? DeviceId { get; set; }
}
