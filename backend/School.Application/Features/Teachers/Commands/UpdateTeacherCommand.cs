using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Teachers.Commands;

public class UpdateTeacherCommand : IRequest<bool>
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateTeacherCommandHandler : IRequestHandler<UpdateTeacherCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTeacherCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateTeacherCommand request, CancellationToken cancellationToken)
    {
        var teacher = await _unitOfWork.Repository<Teacher>().GetByIdAsync(request.Id);
        if (teacher == null) return false;

        teacher.FullName = request.FullName ?? teacher.FullName;
        teacher.Email = request.Email ?? teacher.Email;
        teacher.Phone = request.Phone ?? teacher.Phone;
        teacher.IsActive = request.IsActive;

        _unitOfWork.Repository<Teacher>().Update(teacher);
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
