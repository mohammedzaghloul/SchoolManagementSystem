using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Teachers.Commands;

public class DeleteTeacherCommand : IRequest<bool>
{
    public int Id { get; set; }
}

public class DeleteTeacherCommandHandler : IRequestHandler<DeleteTeacherCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteTeacherCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteTeacherCommand request, CancellationToken cancellationToken)
    {
        var teacher = await _unitOfWork.Repository<Teacher>().GetByIdAsync(request.Id);
        if (teacher == null) return false;

        _unitOfWork.Repository<Teacher>().Delete(teacher);
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
