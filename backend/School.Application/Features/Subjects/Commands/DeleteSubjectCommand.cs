using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Subjects.Commands;

public class DeleteSubjectCommand : IRequest<bool>
{
    public int Id { get; set; }
}

public class DeleteSubjectCommandHandler : IRequestHandler<DeleteSubjectCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSubjectCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteSubjectCommand request, CancellationToken cancellationToken)
    {
        var subject = await _unitOfWork.Repository<Subject>().GetByIdAsync(request.Id);
        if (subject == null) return false;

        _unitOfWork.Repository<Subject>().Delete(subject);
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
