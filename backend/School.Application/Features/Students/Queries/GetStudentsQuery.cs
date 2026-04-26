using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Students.Queries;

public class GetStudentsQuery : IRequest<List<StudentDto>>
{
    public int? ClassRoomId { get; set; }
}

public class StudentDto
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public int ClassRoomId { get; set; }
    public int? GradeId { get; set; }
    public int? GradeLevelId { get; set; }
    public string QrCodeValue { get; set; }
    public bool IsActive { get; set; }
    public string ClassRoomName { get; set; }
    public string? GradeName { get; set; }
    public string? GradeLevelName { get; set; }
    public string ParentName { get; set; }
    public int? ParentId { get; set; }
}

public class StudentWithDetailsSpecification : Specifications.BaseSpecification<Student>
{
    public StudentWithDetailsSpecification(int? classRoomId) 
        : base(s => !classRoomId.HasValue || s.ClassRoomId == classRoomId)
    {
        AddDetailsIncludes();
    }

    public StudentWithDetailsSpecification(int id)
        : base(s => s.Id == id)
    {
        AddDetailsIncludes();
    }

    private void AddDetailsIncludes()
    {
        AddInclude(s => s.Parent);
        AddInclude("ClassRoom.GradeLevel");
    }
}

public class GetStudentsQueryHandler : IRequestHandler<GetStudentsQuery, List<StudentDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetStudentsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<StudentDto>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        var spec = new StudentWithDetailsSpecification(request.ClassRoomId);

        var students = await _unitOfWork.Repository<Student>().ListAsync(spec);

        return students.Select(s =>
        {
            var gradeLevel = s.ClassRoom?.GradeLevel;

            return new StudentDto
            {
                Id = s.Id,
                FullName = s.FullName,
                Email = s.Email,
                Phone = s.Phone,
                ClassRoomId = s.ClassRoomId.GetValueOrDefault(),
                GradeId = gradeLevel?.Id,
                GradeLevelId = gradeLevel?.Id,
                ClassRoomName = s.ClassRoom?.Name,
                GradeName = gradeLevel?.Name,
                GradeLevelName = gradeLevel?.Name,
                ParentName = s.Parent?.FullName,
                ParentId = s.ParentId,
                QrCodeValue = s.QrCodeValue,
                IsActive = s.IsActive
            };
        }).ToList();
    }
}
