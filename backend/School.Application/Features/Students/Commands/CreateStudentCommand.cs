using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Students.Commands;

public class CreateStudentCommand : IRequest<int>
{
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string? Password { get; set; }
    public int? ClassRoomId { get; set; }
    public int? ParentId { get; set; }
    public bool? IsActive { get; set; }
}

public class CreateStudentCommandHandler : IRequestHandler<CreateStudentCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateStudentCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(CreateStudentCommand request, CancellationToken cancellationToken)
    {
        var student = new Student
        {
            UserId = Guid.NewGuid().ToString(), // Provide a dummy UserId to satisfy constraints
            FullName = request.FullName ?? "Unknown Student",
            Email = request.Email ?? "student@domain.com",
            Phone = request.Phone ?? "",
            ClassRoomId = request.ClassRoomId,
            ParentId = request.ParentId,
            BirthDate = DateTime.UtcNow.AddYears(-10), 
            QrCodeValue = Guid.NewGuid().ToString(),
            IsActive = request.IsActive ?? true
        };

        await _unitOfWork.Repository<Student>().AddAsync(student);
        await _unitOfWork.CompleteAsync();

        return student.Id;
    }
}
