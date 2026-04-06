using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Subjects.Queries;

public class GetSubjectByIdQuery : IRequest<SubjectDto>
{
    public int Id { get; set; }
}

public class GetSubjectByIdQueryHandler : IRequestHandler<GetSubjectByIdQuery, SubjectDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetSubjectByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<SubjectDto?> Handle(GetSubjectByIdQuery request, CancellationToken cancellationToken)
    {
        var s = await _unitOfWork.Repository<Subject>().GetByIdAsync(request.Id);

        if (s == null) return null;

        return new SubjectDto
        {
            Id = s.Id,
            Name = s.Name,
            Code = s.Code,
            Description = s.Description,
            Term = s.Term,
            IsActive = s.IsActive,
            TeacherId = s.TeacherId,
            ClassRoomId = s.ClassRoomId
        };
    }
}
