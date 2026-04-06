using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Subjects.Commands;

public class CreateSubjectCommand : IRequest<int>
{
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Term { get; set; }
    public bool IsActive { get; set; } = true;
    public int? TeacherId { get; set; }
    public int? ClassRoomId { get; set; }
}

public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateSubjectCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        var subject = new Subject
        {
            Name = request.Name ?? "Unnamed Subject",
            Code = request.Code ?? "SUBJ-" + new Random().Next(100, 999),
            Description = request.Description ?? "No description provided.",
            Term = request.Term ?? "الترم الأول",
            IsActive = request.IsActive,
            TeacherId = request.TeacherId,
            ClassRoomId = request.ClassRoomId
        };

        await _unitOfWork.Repository<Subject>().AddAsync(subject);
        await _unitOfWork.CompleteAsync();

        return subject.Id;
    }
}
