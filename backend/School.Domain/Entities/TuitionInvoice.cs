namespace School.Domain.Entities;

public class TuitionInvoice : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string AcademicYear { get; set; } = null!;
    public string Term { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public string Status { get; set; } = "Pending";
    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }
}
