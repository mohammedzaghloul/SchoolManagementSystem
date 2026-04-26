using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Teachers.Queries;

public class GetTeachersQuery : IRequest<List<TeacherDto>>
{
}

public class TeacherDto
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public int? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public bool IsActive { get; set; }
}

public class GetTeachersQueryHandler : IRequestHandler<GetTeachersQuery, List<TeacherDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTeachersQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<TeacherDto>> Handle(GetTeachersQuery request, CancellationToken cancellationToken)
    {
        var spec = new TeachersWithSubjectsSpecification();
        var teachers = await _unitOfWork.Repository<Teacher>().ListAsync(spec);
        
        return teachers.Select(t => {
            var primarySubject = t.Subjects?.FirstOrDefault();
            return new TeacherDto
            {
                Id = t.Id,
                FullName = t.FullName,
                Email = t.Email,
                Phone = t.Phone,
                SubjectId = primarySubject?.Id,
                SubjectName = primarySubject?.Name,
                IsActive = t.IsActive
            };
        }).ToList();
    }
}
