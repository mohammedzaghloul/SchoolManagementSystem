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
    private readonly IConfiguration _configuration;

    public ParentController(
        SchoolDbContext context,
        IHubContext<ChatHub> hubContext,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _context = context;
        _hubContext = hubContext;
        _userManager = userManager;
        _configuration = configuration;
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

    [HttpGet("grades/history")]
    [Authorize(Roles = "Parent")]
    public async Task<ActionResult<IEnumerable<object>>> GetChildrenGradeHistory([FromQuery] int? childId, [FromQuery] int take = 80)
    {
        var parent = await GetCurrentParentAsync(includeChildren: true);
        if (parent == null)
        {
            return NotFound("Parent not found");
        }

        var childIds = parent.Children.Select(c => c.Id).ToList();
        if (childId.HasValue)
        {
            if (!childIds.Contains(childId.Value))
            {
                return Forbid();
            }

            childIds = [childId.Value];
        }

        var safeTake = Math.Clamp(take, 10, 200);
        var grades = await _context.GradeRecords
            .AsNoTracking()
            .Include(grade => grade.Student)
            .Include(grade => grade.Subject)
            .Where(grade => childIds.Contains(grade.StudentId))
            .OrderByDescending(grade => grade.Date)
            .ThenByDescending(grade => grade.Id)
            .Take(safeTake)
            .Select(grade => new
            {
                grade.Id,
                grade.StudentId,
                studentName = grade.Student != null ? grade.Student.FullName : "الطالب",
                grade.SubjectId,
                subjectName = grade.Subject != null ? grade.Subject.Name : "المادة",
                grade.GradeType,
                grade.Score,
                percentage = Math.Round(grade.Score, 1),
                grade.Notes,
                grade.Date
            })
            .ToListAsync();

        return Ok(grades);
    }

    [HttpGet("payments/methods")]
    [Authorize(Roles = "Parent")]
    public ActionResult<IEnumerable<PaymentMethodDto>> GetPaymentMethods()
    {
        return Ok(BuildPaymentMethods());
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
        var method = ResolvePaymentMethod(request);
        invoice.PaymentMethod = method.Label;
        invoice.ReferenceNumber = $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{invoice.Id}";
        invoice.Notes = string.IsNullOrWhiteSpace(request?.Note)
            ? method.ProviderCode
            : $"{request!.Note.Trim()} | Provider: {method.ProviderCode}";
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
            paymentMethodCode = method.Code,
            providerCode = method.ProviderCode,
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
        public string? MethodCode { get; set; }
        public string? Note { get; set; }
    }

    public class PaymentMethodDto
    {
        public string Code { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Hint { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string ProviderCode { get; set; } = string.Empty;
        public string? Receiver { get; set; }
        public bool RequiresOtp { get; set; }
        public bool RequiresReferenceConfirmation { get; set; }
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

    private PaymentMethodDto ResolvePaymentMethod(ParentPaymentRequest? request)
    {
        var methods = BuildPaymentMethods();
        var requestedCode = request?.MethodCode?.Trim();
        var requestedLabel = request?.Method?.Trim();

        return methods.FirstOrDefault(method =>
                string.Equals(method.Code, requestedCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method.Label, requestedLabel, StringComparison.OrdinalIgnoreCase))
            ?? methods.First(method => method.Code == "card");
    }

    private List<PaymentMethodDto> BuildPaymentMethods()
    {
        var vodafoneNumber = GetPaymentSetting("VodafoneCashNumber", "0100");
        var supportPhone = GetPaymentSetting("SupportPhone", vodafoneNumber);
        var instaPayHandle = GetPaymentSetting("InstaPayHandle", "school@instapay");
        var fawryBillerCode = GetPaymentSetting("FawryBillerCode", "788");

        return
        [
            new PaymentMethodDto
            {
                Code = "vodafone_cash",
                Label = "Vodafone Cash",
                Hint = "إرسال تعليمات السداد ورقم المرجع على رقم ولي الأمر",
                Icon = "fas fa-mobile-screen-button",
                ProviderCode = "VODAFONE_CASH",
                Receiver = vodafoneNumber,
                RequiresOtp = true
            },
            new PaymentMethodDto
            {
                Code = "instapay",
                Label = "InstaPay",
                Hint = "تحويل لحظي على عنوان InstaPay ثم تأكيد المرجع",
                Icon = "fas fa-building-columns",
                ProviderCode = "INSTAPAY",
                Receiver = instaPayHandle,
                RequiresReferenceConfirmation = true
            },
            new PaymentMethodDto
            {
                Code = "fawry",
                Label = "فوري",
                Hint = $"إنشاء كود سداد على خدمة فوري رقم {fawryBillerCode}",
                Icon = "fas fa-barcode",
                ProviderCode = "FAWRY",
                Receiver = fawryBillerCode,
                RequiresReferenceConfirmation = true
            },
            new PaymentMethodDto
            {
                Code = "card",
                Label = "بطاقة بنكية",
                Hint = $"محاكاة دفع آمنة داخل المنصة مع دعم المتابعة {supportPhone}",
                Icon = "far fa-credit-card",
                ProviderCode = "CARD_DEMO",
                RequiresOtp = true
            }
        ];
    }

    private string GetPaymentSetting(string key, string fallback)
    {
        return _configuration[$"PaymentGateway:{key}"]
            ?? _configuration[key]
            ?? Environment.GetEnvironmentVariable(key)
            ?? fallback;
    }
}
