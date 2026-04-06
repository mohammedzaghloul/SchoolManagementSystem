using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Announcements.Commands;

public class CreateAnnouncementCommand : IRequest<int>
{
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string TargetAudience { get; set; } = null!;
}

public class CreateAnnouncementCommandHandler : IRequestHandler<CreateAnnouncementCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateAnnouncementCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(CreateAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var announcement = new Announcement
        {
            Title = request.Title,
            Content = request.Content,
            CreatedAt = DateTime.UtcNow,
            Audience = request.TargetAudience
        };

        await _unitOfWork.Repository<Announcement>().AddAsync(announcement);
        await _unitOfWork.CompleteAsync();

        return announcement.Id;
    }
}
