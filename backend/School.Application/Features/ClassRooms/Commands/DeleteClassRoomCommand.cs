using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.ClassRooms.Commands;

public class DeleteClassRoomCommand : IRequest<bool>
{
    public int Id { get; set; }
}

public class DeleteClassRoomCommandHandler : IRequestHandler<DeleteClassRoomCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteClassRoomCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteClassRoomCommand request, CancellationToken cancellationToken)
    {
        var classRoom = await _unitOfWork.Repository<ClassRoom>().GetByIdAsync(request.Id);
        if (classRoom == null) return false;

        _unitOfWork.Repository<ClassRoom>().Delete(classRoom);
        await _unitOfWork.CompleteAsync();

        return true;
    }
}
