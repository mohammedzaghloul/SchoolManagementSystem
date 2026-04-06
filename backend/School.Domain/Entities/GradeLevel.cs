namespace School.Domain.Entities;

public class GradeLevel : BaseEntity
{
    public string Name { get; set; } = null!; // e.g., Grade 1, Grade 2
    public string? Description { get; set; }
    public ICollection<ClassRoom> ClassRooms { get; set; } = new List<ClassRoom>();
}
