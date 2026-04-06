using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Subjects.Commands;

public class UpdateSubjectCommand : IRequest<bool>
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Term { get; set; }
    public bool IsActive { get; set; } = true;
    public int? TeacherId { get; set; }
    public int? ClassRoomId { get; set; }
}

public class UpdateSubjectCommandHandler : IRequestHandler<UpdateSubjectCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSubjectCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        var subject = await _unitOfWork.Repository<Subject>().GetByIdAsync(request.Id);
        if (subject == null) return false;

        subject.Name = request.Name ?? subject.Name;
        subject.Code = request.Code ?? subject.Code;
        subject.Description = request.Description ?? subject.Description;
        subject.Term = request.Term ?? subject.Term;
        subject.IsActive = request.IsActive;
        subject.TeacherId = request.TeacherId ?? subject.TeacherId;
        subject.ClassRoomId = request.ClassRoomId ?? subject.ClassRoomId;

        _unitOfWork.Repository<Subject>().Update(subject);
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
