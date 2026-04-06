using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.GradeLevels.Commands;

public class UpdateGradeLevelCommand : IRequest<bool>
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateGradeLevelCommandHandler : IRequestHandler<UpdateGradeLevelCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGradeLevelCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateGradeLevelCommand request, CancellationToken cancellationToken)
    {
        var grade = await _unitOfWork.Repository<GradeLevel>().GetByIdAsync(request.Id);

        if (grade == null) return false;

        grade.Name = request.Name;
        grade.Description = request.Description;

        _unitOfWork.Repository<GradeLevel>().Update(grade);
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
