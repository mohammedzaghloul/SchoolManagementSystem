using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Students.Commands;

public class UpdateStudentCommand : IRequest<bool>
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public int? ClassRoomId { get; set; }
    public int? ParentId { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateStudentCommandHandler : IRequestHandler<UpdateStudentCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStudentCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await _unitOfWork.Repository<Student>().GetByIdAsync(request.Id);
        if (student == null) return false;

        student.FullName = request.FullName ?? student.FullName;
        student.Email = request.Email ?? student.Email;
        student.Phone = request.Phone ?? student.Phone;
        student.ClassRoomId = request.ClassRoomId ?? student.ClassRoomId;
        student.ParentId = request.ParentId ?? student.ParentId;
        if (request.IsActive.HasValue) 
        {
            student.IsActive = request.IsActive.Value;
        }

        _unitOfWork.Repository<Student>().Update(student);
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
