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
    public string QrCodeValue { get; set; }
    public bool IsActive { get; set; }
    public string ClassRoomName { get; set; }
    public string ParentName { get; set; }
    public int? ParentId { get; set; }
}

public class StudentWithDetailsSpecification : Specifications.BaseSpecification<Student>
{
    public StudentWithDetailsSpecification(int? classRoomId) 
        : base(s => !classRoomId.HasValue || s.ClassRoomId == classRoomId)
    {
        AddInclude(s => s.Parent);
        AddInclude(s => s.ClassRoom);
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

        return students.Select(s => new StudentDto
        {
            Id = s.Id,
            FullName = s.FullName,
            Email = s.Email,
            Phone = s.Phone,
            ClassRoomId = s.ClassRoomId.GetValueOrDefault(),
            ClassRoomName = s.ClassRoom?.Name,
            ParentName = s.Parent?.FullName,
            ParentId = s.ParentId,
            QrCodeValue = s.QrCodeValue,
            IsActive = s.IsActive
        }).ToList();
    }
}
