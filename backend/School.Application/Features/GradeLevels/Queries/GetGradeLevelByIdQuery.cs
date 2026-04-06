using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.GradeLevels.Queries;

public class GetGradeLevelByIdQuery : IRequest<GradeLevelDto>
{
    public int Id { get; set; }
}

public class GetGradeLevelByIdQueryHandler : IRequestHandler<GetGradeLevelByIdQuery, GradeLevelDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetGradeLevelByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GradeLevelDto?> Handle(GetGradeLevelByIdQuery request, CancellationToken cancellationToken)
    {
        var g = await _unitOfWork.Repository<GradeLevel>().GetByIdAsync(request.Id);

        if (g == null) return null;

        return new GradeLevelDto
        {
            Id = g.Id,
            Name = g.Name
        };
    }
}
