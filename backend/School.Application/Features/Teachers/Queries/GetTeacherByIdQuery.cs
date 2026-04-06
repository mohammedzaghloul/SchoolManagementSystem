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
        var teacher = await _unitOfWork.Repository<Teacher>().GetByIdAsync(request.Id);

        if (teacher == null) return null;

        return new TeacherDto
        {
            Id = teacher.Id,
            FullName = teacher.FullName,
            Email = teacher.Email,
            Phone = teacher.Phone,
            IsActive = teacher.IsActive
        };
    }
}
