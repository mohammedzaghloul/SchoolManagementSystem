using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Teachers.Queries;

public class GetTeacherByIdQuery : IRequest<TeacherDto>
{
    public int Id { get; set; }
}

public class GetTeacherByIdQueryHandler : IRequestHandler<GetTeacherByIdQuery, TeacherDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTeacherByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TeacherDto?> Handle(GetTeacherByIdQuery request, CancellationToken cancellationToken)
    {
        var spec = new TeachersWithSubjectsSpecification(request.Id);
        var teacher = await _unitOfWork.Repository<Teacher>().GetEntityWithSpec(spec);

        if (teacher == null) return null;

        var primarySubject = teacher.Subjects?.FirstOrDefault();
        return new TeacherDto
        {
            Id = teacher.Id,
            FullName = teacher.FullName,
            Email = teacher.Email,
            Phone = teacher.Phone,
            SubjectId = primarySubject?.Id,
            SubjectName = primarySubject?.Name,
            IsActive = teacher.IsActive
        };
    }
}
