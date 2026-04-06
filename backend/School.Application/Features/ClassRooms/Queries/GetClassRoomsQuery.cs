using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.ClassRooms.Queries;

public class GetClassRoomsQuery : IRequest<List<ClassRoomDto>>
{
}

public class ClassRoomDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int GradeLevelId { get; set; }
    public int Capacity { get; set; }
}

public class GetClassRoomsQueryHandler : IRequestHandler<GetClassRoomsQuery, List<ClassRoomDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetClassRoomsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<ClassRoomDto>> Handle(GetClassRoomsQuery request, CancellationToken cancellationToken)
    {
        var rooms = await _unitOfWork.Repository<ClassRoom>().ListAllAsync();

        return rooms.Select(r => new ClassRoomDto
        {
            Id = r.Id,
            Name = r.Name,
            GradeLevelId = r.GradeLevelId.GetValueOrDefault(),
            Capacity = r.Capacity
        }).ToList();
    }
}
