using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.ClassRooms.Queries;

public class GetClassRoomByIdQuery : IRequest<ClassRoomDto>
{
    public int Id { get; set; }
}

public class GetClassRoomByIdQueryHandler : IRequestHandler<GetClassRoomByIdQuery, ClassRoomDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetClassRoomByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ClassRoomDto?> Handle(GetClassRoomByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _unitOfWork.Repository<ClassRoom>().GetByIdAsync(request.Id);

        if (r == null) return null;

        return new ClassRoomDto
        {
            Id = r.Id,
            Name = r.Name,
            GradeLevelId = r.GradeLevelId.GetValueOrDefault(),
            Capacity = r.Capacity
        };
    }
}
