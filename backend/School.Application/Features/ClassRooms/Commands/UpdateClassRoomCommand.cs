using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.ClassRooms.Commands;

public class UpdateClassRoomCommand : IRequest<bool>
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Capacity { get; set; }
    public int? GradeLevelId { get; set; }
}

public class UpdateClassRoomCommandHandler : IRequestHandler<UpdateClassRoomCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateClassRoomCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateClassRoomCommand request, CancellationToken cancellationToken)
    {
        var classRoom = await _unitOfWork.Repository<ClassRoom>().GetByIdAsync(request.Id);
        if (classRoom == null) return false;

        classRoom.Name = request.Name ?? classRoom.Name;
        if(request.Capacity > 0) classRoom.Capacity = request.Capacity;
        classRoom.GradeLevelId = request.GradeLevelId ?? classRoom.GradeLevelId;

        _unitOfWork.Repository<ClassRoom>().Update(classRoom);
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
