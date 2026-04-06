using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.ClassRooms.Commands;

public class CreateClassRoomCommand : IRequest<int>
{
    public string Name { get; set; }
    public int GradeLevelId { get; set; }
    public int Capacity { get; set; }
}

public class CreateClassRoomCommandHandler : IRequestHandler<CreateClassRoomCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateClassRoomCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(CreateClassRoomCommand request, CancellationToken cancellationToken)
    {
        var classRoom = new ClassRoom
        {
            Name = request.Name ?? "Unnamed Class",
            GradeLevelId = request.GradeLevelId,
            Capacity = request.Capacity,
            Location = "TBD", // Fix for non-nullable property missing UI
            AcademicYear = "2026-2027" // Default Academic Year
        };

        await _unitOfWork.Repository<ClassRoom>().AddAsync(classRoom);
        await _unitOfWork.CompleteAsync();

        return classRoom.Id;
    }
}
