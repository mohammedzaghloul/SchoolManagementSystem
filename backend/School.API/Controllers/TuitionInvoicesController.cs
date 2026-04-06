using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using School.Domain.Entities;
using School.Infrastructure.Data;

namespace School.API.Controllers;

[Authorize(Roles = "Admin")]
public class TuitionInvoicesController : BaseApiController
{
    private readonly SchoolDbContext _context;

    public TuitionInvoicesController(SchoolDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetInvoices()
    {
        var invoices = await _context.TuitionInvoices
            .AsNoTracking()
            .Include(invoice => invoice.Student)
                .ThenInclude(student => student!.Parent)
            .Include(invoice => invoice.Student)
                .ThenInclude(student => student!.ClassRoom)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .ToListAsync();

        return Ok(invoices.Select(MapInvoice));
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateInvoice([FromBody] UpsertTuitionInvoiceRequest request)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return BadRequest(new { message = validationError });
        }

        var student = await _context.Students
            .Include(item => item.Parent)
            .Include(item => item.ClassRoom)
            .FirstOrDefaultAsync(item => item.Id == request.StudentId);

        if (student == null)
        {
            return NotFound(new { message = "الطالب المحدد غير موجود." });
        }

        var invoice = new TuitionInvoice
        {
            StudentId = request.StudentId,
            Title = request.Title.Trim(),
            Description = NormalizeOptional(request.Description),
            AcademicYear = request.AcademicYear.Trim(),
            Term = request.Term.Trim(),
            Amount = request.Amount,
            AmountPaid = 0,
            DueDate = request.DueDate,
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        };

        _context.TuitionInvoices.Add(invoice);
        await _context.SaveChangesAsync();

        invoice.Student = student;
        return Ok(MapInvoice(invoice));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<object>> UpdateInvoice(int id, [FromBody] UpsertTuitionInvoiceRequest request)
    {
        var validationError = ValidateRequest(request);
        if (validationError != null)
        {
            return BadRequest(new { message = validationError });
        }

        var invoice = await _context.TuitionInvoices
            .Include(item => item.Student)
                .ThenInclude(student => student!.Parent)
            .Include(item => item.Student)
                .ThenInclude(student => student!.ClassRoom)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (invoice == null)
        {
            return NotFound(new { message = "الفاتورة غير موجودة." });
        }

        if (request.Amount < invoice.AmountPaid)
        {
            return BadRequest(new { message = "لا يمكن أن يكون مبلغ الفاتورة أقل من المبلغ الذي تم تحصيله بالفعل." });
        }

        var student = await _context.Students
            .Include(item => item.Parent)
            .Include(item => item.ClassRoom)
            .FirstOrDefaultAsync(item => item.Id == request.StudentId);

        if (student == null)
        {
            return NotFound(new { message = "الطالب المحدد غير موجود." });
        }

        invoice.StudentId = request.StudentId;
        invoice.Student = student;
        invoice.Title = request.Title.Trim();
        invoice.Description = NormalizeOptional(request.Description);
        invoice.AcademicYear = request.AcademicYear.Trim();
        invoice.Term = request.Term.Trim();
        invoice.Amount = request.Amount;
        invoice.DueDate = request.DueDate;
        invoice.Status = ResolveStatus(invoice);

        await _context.SaveChangesAsync();

        return Ok(MapInvoice(invoice));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteInvoice(int id)
    {
        var invoice = await _context.TuitionInvoices.FirstOrDefaultAsync(item => item.Id == id);
        if (invoice == null)
        {
            return NotFound(new { message = "الفاتورة غير موجودة." });
        }

        if (invoice.AmountPaid > 0)
        {
            return BadRequest(new { message = "لا يمكن حذف فاتورة تم تحصيل جزء منها أو كلها." });
        }

        _context.TuitionInvoices.Remove(invoice);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static object MapInvoice(TuitionInvoice invoice)
    {
        return new
        {
            invoice.Id,
            invoice.Title,
            invoice.Description,
            invoice.AcademicYear,
            invoice.Term,
            invoice.Amount,
            invoice.AmountPaid,
            remainingAmount = Math.Max(invoice.Amount - invoice.AmountPaid, 0),
            invoice.DueDate,
            invoice.CreatedAt,
            invoice.PaidAt,
            status = ResolveStatus(invoice),
            invoice.PaymentMethod,
            invoice.ReferenceNumber,
            studentId = invoice.StudentId,
            studentName = invoice.Student?.FullName ?? "الطالب",
            parentId = invoice.Student?.ParentId,
            parentName = invoice.Student?.Parent?.FullName,
            classRoomName = invoice.Student?.ClassRoom?.Name
        };
    }

    private static string? ValidateRequest(UpsertTuitionInvoiceRequest request)
    {
        if (request.StudentId <= 0)
        {
            return "يرجى اختيار الطالب أولاً.";
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.AcademicYear) || string.IsNullOrWhiteSpace(request.Term))
        {
            return "يرجى استكمال عنوان الفاتورة والعام الدراسي والترم.";
        }

        if (request.Amount <= 0)
        {
            return "يجب أن يكون مبلغ الفاتورة أكبر من صفر.";
        }

        return null;
    }

    private static string ResolveStatus(TuitionInvoice invoice)
    {
        if (invoice.AmountPaid >= invoice.Amount)
        {
            return "Paid";
        }

        if (invoice.DueDate.Date < DateTime.Today)
        {
            return "Overdue";
        }

        if (invoice.AmountPaid > 0)
        {
            return "Partial";
        }

        return "Pending";
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public class UpsertTuitionInvoiceRequest
    {
        public int StudentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string AcademicYear { get; set; } = string.Empty;
        public string Term { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
    }
}
