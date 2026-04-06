namespace School.Domain.Entities;

public class Parent : BaseEntity
{
    public string UserId { get; set; } = null!; // Link to AspNetUsers
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Address { get; set; }

    public ICollection<Student> Children { get; set; } = new List<Student>();
}
