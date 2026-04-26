using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Teachers.Commands;

public class CreateTeacherCommand : IRequest<int>
{
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string? Password { get; set; }
    public int? SubjectId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateTeacherCommandHandler : IRequestHandler<CreateTeacherCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateTeacherCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(CreateTeacherCommand request, CancellationToken cancellationToken)
    {
        var teacher = new Teacher
        {
            UserId = Guid.NewGuid().ToString(), // Dummy identifier to satisfy EF Core constraints
            FullName = request.FullName ?? "Unknown Teacher",
            Email = request.Email ?? "teacher@domain.com",
            Phone = request.Phone ?? "",
            IsActive = request.IsActive
        };

        await _unitOfWork.Repository<Teacher>().AddAsync(teacher);
        await _unitOfWork.CompleteAsync();

        return teacher.Id;
    }
}
