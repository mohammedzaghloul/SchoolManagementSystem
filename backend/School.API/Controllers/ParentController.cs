using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using School.API.Hubs;
using School.Domain.Entities;
using School.Infrastructure.Data;
using School.Infrastructure.Identity;
using System.Security.Claims;

namespace School.API.Controllers;

[Authorize]
public class ParentController : BaseApiController
{
    private readonly SchoolDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public ParentController(
        SchoolDbContext context,
        IHubContext<ChatHub> hubContext,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _hubContext = hubContext;
        _userManager = userManager;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<object>>> GetParents()
    {
        var parents = await _context.Parents
            .Include(p => p.Children)
            .OrderBy(p => p.FullName)
            .Select(p => new
            {
                p.Id,
                p.UserId,
                fullName = p.FullName,
                p.Email,
                p.Phone,
                p.Address,
                childrenCount = p.Children.Count
            })
            .ToListAsync();

        return Ok(parents);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<object>> GetParentById(int id)
    {
        var parent = await _context.Parents
            .Include(p => p.Children)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (parent == null)
        {
            return NotFound("Parent not found");
        }

        return Ok(new
        {
            parent.Id,
            parent.UserId,
            fullName = parent.FullName,
            parent.Email,
            parent.Phone,
            parent.Address,
            children = parent.Children.Select(c => new
            {
                c.Id,
                c.FullName,
                c.Email
            })
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Parent")]
    public async Task<ActionResult<object>> UpdateParent(int id, [FromBody] Parent update)
    {
        var parent = await _context.Parents.FirstOrDefaultAsync(p => p.Id == id);
        if (parent == null)
        {
            return NotFound("Parent not found");
        }

        if (User.IsInRole("Parent"))
        {
            var currentParent = await GetCurrentParentAsync();
            if (currentParent == null || currentParent.Id != id)
            {
                return Forbid();
            }
        }

        parent.FullName = string.IsNullOrWhiteSpace(update.FullName) ? parent.FullName : update.FullName;
        parent.Phone = string.IsNullOrWhiteSpace(update.Phone) ? parent.Phone : update.Phone;
        parent.Address = update.Address ?? parent.Address;

        if (!string.IsNullOrWhiteSpace(parent.UserId))
        {
            var user = await _userManager.FindByIdAsync(parent.UserId);
            if (user != null)
            {
                user.FullName = parent.FullName;
                user.PhoneNumber = parent.Phone;

                var userUpdateResult = await _userManager.UpdateAsync(user);
                if (!userUpdateResult.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = "تعذر تحديث بيانات حساب ولي الأمر.",
                        errors = userUpdateResult.Errors.Select(error => error.Description)
                    });
                }
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            parent.Id,
            fullName = parent.FullName,
            parent.Email,
            parent.Phone,
            parent.Address
        });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteParent(int id)
    {
        var parent = await _context.Parents.FirstOrDefaultAsync(p => p.Id == id);
        if (parent == null)
        {
            return NotFound("Parent not found");
        }

        if (!string.IsNullOrWhiteSpace(parent.UserId))
        {
            var user = await _userManager.FindByIdAsync(parent.UserId);
            if (user != null)
            {
                var deleteUserResult = await _userManager.DeleteAsync(user);
                if (!deleteUserResult.Succeeded)
                {
                    return BadRequest(new
                    {
                        message = "تعذر حذف حساب ولي الأمر المرتبط.",
                        errors = deleteUserResult.Errors.Select(error => error.Description)
                    });
                }
            }
        }

        _context.Parents.Remove(parent);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{parentId:int}/children")]
    [Authorize(Roles = "Admin,Parent")]
    public async Task<ActionResult<IEnumerable<object>>> GetChildren(int parentId)
    {
        if (User.IsInRole("Parent"))
        {
            var currentParent = await GetCurrentParentAsync();
            if (currentParent == null || currentParent.Id != parentId)
            {
                return Forbid();
            }
        }

        var children = await _context.Students
            .Include(s => s.ClassRoom)
            .Where(s => s.ParentId == parentId)
            .OrderBy(s => s.FullName)
            .Select(s => new
            {
                s.Id,
                s.FullName,
                s.Email,
                classRoomName = s.ClassRoom != null ? s.ClassRoom.Name : "غير محدد"
            })
            .ToListAsync();

        return Ok(children);
    }

    [HttpGet("events")]
    [Authorize(Roles = "Parent")]
    public async Task<ActionResult<IEnumerable<object>>> GetUpcomingEvents()
    {
        var parent = await GetCurrentParentAsync(includeChildren: true);
        if (parent == null)
        {
            return NotFound("Parent not found");
        }

        var childIds = parent.Children.Select(c => c.Id).ToList();
        var classRoomIds = parent.Children
            .Where(c => c.ClassRoomId.HasValue)
            .Select(c => c.ClassRoomId!.Value)
            .Distinct()
            .ToList();

        var upcomingExams = await _context.Exams
            .Include(e => e.Subject)
            .Where(e => e.StartTime >= DateTime.Now && classRoomIds.Contains(e.ClassRoomId))
            .OrderBy(e => e.StartTime)
            .Take(8)
            .ToListAsync();

        var paymentDueItems = await _context.TuitionInvoices
            .Include(t => t.Student)
            .Where(t => childIds.Contains(t.StudentId) && t.AmountPaid < t.Amount)
            .OrderBy(t => t.DueDate)
            .Take(8)
            .ToListAsync();

        var examEvents = upcomingExams
            .SelectMany(exam => parent.Children
                .Where(child => child.ClassRoomId == exam.ClassRoomId)
                .Select(child => new ParentEventDto
                {
                    Id = $"exam-{exam.Id}-{child.Id}",
                    Title = exam.Title,
                    Description = $"{exam.Subject?.Name ?? "اختبار"} - {child.FullName}",
                    Date = exam.StartTime,
                    Type = "exam",
                    StudentName = child.FullName ?? "الطالب"
                }));

        var paymentEvents = paymentDueItems
            .Select(invoice => new ParentEventDto
            {
                Id = $"payment-{invoice.Id}",
                Title = invoice.Title,
                Description = $"مستحق على {invoice.Student?.FullName} بقيمة {(invoice.Amount - invoice.AmountPaid):0.##} ج.م",
                Date = invoice.DueDate,
                Type = "payment",
                StudentName = invoice.Student?.FullName ?? "الطالب"
            });

        var events = examEvents
            .Concat(paymentEvents)
            .OrderBy(item => item.Date)
            .Take(6)
            .ToList();

        return Ok(events);
    }

    [HttpGet("pending-payments")]
    [Authorize(Roles = "Parent")]
    public async Task<ActionResult<decimal>> GetPendingPayments()
    {
        var parent = await GetCurrentParentAsync(includeChildren: true);
        if (parent == null)
        {
            return NotFound("Parent not found");
        }

        var childIds = parent.Children.Select(c => c.Id).ToList();
        var invoices = await _context.TuitionInvoices
            .Where(t => childIds.Contains(t.StudentId))
            .ToListAsync();
        var pendingAmount = invoices.Sum(t => t.Amount - t.AmountPaid);

        return Ok(Math.Round(pendingAmount, 2));
    }

    [HttpGet("payments")]
    [Authorize(Roles = "Parent")]
    public async Task<ActionResult<IEnumerable<object>>> GetPayments()
    {
        var parent = await GetCurrentParentAsync(includeChildren: true);
        if (parent == null)
        {
            return NotFound("Parent not found");
        }

        var childIds = parent.Children.Select(c => c.Id).ToList();
        var invoices = await _context.TuitionInvoices
            .Include(t => t.Student)
            .Where(t => childIds.Contains(t.StudentId))
            .OrderByDescending(t => t.DueDate)
            .ToListAsync();

        return Ok(invoices.Select(invoice => new
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
            status = GetInvoiceStatus(invoice),
            invoice.PaymentMethod,
            invoice.ReferenceNumber,
            studentId = invoice.StudentId,
            studentName = invoice.Student?.FullName ?? "الطالب"
        }));
    }

    [HttpPost("payments/{paymentId:int}/pay")]
    [Authorize(Roles = "Parent")]
    public async Task<ActionResult<object>> PayInvoice(int paymentId, [FromBody] ParentPaymentRequest? request)
    {
        var parent = await GetCurrentParentAsync(includeChildren: true);
        if (parent == null)
        {
            return NotFound("Parent not found");
        }

        var childIds = parent.Children.Select(c => c.Id).ToList();
        var invoice = await _context.TuitionInvoices
            .Include(t => t.Student)
            .FirstOrDefaultAsync(t => t.Id == paymentId && childIds.Contains(t.StudentId));

        if (invoice == null)
        {
            return NotFound("Invoice not found");
        }

        var remainingAmount = Math.Max(invoice.Amount - invoice.AmountPaid, 0);
        if (remainingAmount <= 0)
        {
            return BadRequest("Invoice is already fully paid");
        }

        var amountToPay = request?.Amount ?? remainingAmount;
        if (amountToPay <= 0 || amountToPay > remainingAmount)
        {
            return BadRequest("Invalid payment amount");
        }

        invoice.AmountPaid += amountToPay;
        invoice.PaidAt = DateTime.UtcNow;
        invoice.PaymentMethod = string.IsNullOrWhiteSpace(request?.Method) ? "بطاقة" : request!.Method;
        invoice.ReferenceNumber = $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{invoice.Id}";
        invoice.Notes = string.IsNullOrWhiteSpace(request?.Note) ? invoice.Notes : request!.Note;
        invoice.Status = invoice.AmountPaid >= invoice.Amount ? "Paid" : "Partial";

        await _context.SaveChangesAsync();

        await _hubContext.Clients.Group(parent.UserId).SendAsync("ReceiveNotification", new
        {
            title = "تم تسجيل دفعة المصروفات",
            content = $"تم سداد {amountToPay:0.##} ج.م لفاتورة {invoice.Title} الخاصة بالطالب {invoice.Student?.FullName}.",
            type = "Payment"
        });

        return Ok(new
        {
            invoiceId = invoice.Id,
            paidAmount = amountToPay,
            totalPaid = invoice.AmountPaid,
            remainingAmount = Math.Max(invoice.Amount - invoice.AmountPaid, 0),
            status = GetInvoiceStatus(invoice),
            invoice.ReferenceNumber,
            invoice.PaymentMethod,
            invoice.PaidAt
        });
    }

    private async Task<Parent?> GetCurrentParentAsync(bool includeChildren = false)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return null;
        }

        var query = _context.Parents.AsQueryable();
        if (includeChildren)
        {
            query = query
                .Include(p => p.Children)
                .ThenInclude(c => c.ClassRoom);
        }

        return await query.FirstOrDefaultAsync(p => p.Email == userEmail);
    }

    private static string GetInvoiceStatus(TuitionInvoice invoice)
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

    public class ParentPaymentRequest
    {
        public decimal? Amount { get; set; }
        public string? Method { get; set; }
        public string? Note { get; set; }
    }

    public class ParentEventDto
    {
        public string Id { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime Date { get; set; }
        public string Type { get; set; } = null!;
        public string StudentName { get; set; } = null!;
    }
}
