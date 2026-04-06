using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.GradeLevels.Commands;

public class DeleteGradeLevelCommand : IRequest<bool>
{
    public int Id { get; set; }
}

public class DeleteGradeLevelCommandHandler : IRequestHandler<DeleteGradeLevelCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteGradeLevelCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteGradeLevelCommand request, CancellationToken cancellationToken)
    {
        var grade = await _unitOfWork.Repository<GradeLevel>().GetByIdAsync(request.Id);

        if (grade == null) return false;

        _unitOfWork.Repository<GradeLevel>().Delete(grade);
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
