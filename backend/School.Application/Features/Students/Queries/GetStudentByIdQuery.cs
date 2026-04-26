using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Students.Queries;

public class GetStudentByIdQuery : IRequest<StudentDto>
{
    public int Id { get; set; }
}

public class GetStudentByIdQueryHandler : IRequestHandler<GetStudentByIdQuery, StudentDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetStudentByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<StudentDto?> Handle(GetStudentByIdQuery request, CancellationToken cancellationToken)
    {
        var spec = new StudentWithDetailsSpecification(request.Id);
        var student = await _unitOfWork.Repository<Student>().GetEntityWithSpec(spec);

        if (student == null) return null;

        var gradeLevel = student.ClassRoom?.GradeLevel;
        return new StudentDto
        {
            Id = student.Id,
            FullName = student.FullName,
            Email = student.Email,
            Phone = student.Phone,
            ClassRoomId = student.ClassRoomId.GetValueOrDefault(),
            GradeId = gradeLevel?.Id,
            GradeLevelId = gradeLevel?.Id,
            ClassRoomName = student.ClassRoom?.Name,
            GradeName = gradeLevel?.Name,
            GradeLevelName = gradeLevel?.Name,
            ParentName = student.Parent?.FullName,
            ParentId = student.ParentId,
            QrCodeValue = student.QrCodeValue,
            IsActive = student.IsActive
        };
    }
}
