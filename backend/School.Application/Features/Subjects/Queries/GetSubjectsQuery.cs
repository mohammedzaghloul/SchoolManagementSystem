using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Subjects.Queries;

public class GetSubjectsQuery : IRequest<List<SubjectDto>>
{
}

public class SubjectDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string Description { get; set; }
    public string? Term { get; set; }
    public bool IsActive { get; set; }
    public int? TeacherId { get; set; }
    public int? ClassRoomId { get; set; }
}

public class GetSubjectsQueryHandler : IRequestHandler<GetSubjectsQuery, List<SubjectDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetSubjectsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<SubjectDto>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        var subjects = await _unitOfWork.Repository<Subject>().ListAllAsync();

        return subjects.Select(s => new SubjectDto
        {
            Id = s.Id,
            Name = s.Name,
            Code = s.Code,
            Description = s.Description,
            Term = s.Term,
            IsActive = s.IsActive,
            TeacherId = s.TeacherId,
            ClassRoomId = s.ClassRoomId
        }).ToList();
    }
}
