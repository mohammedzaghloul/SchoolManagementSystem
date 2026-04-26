using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Chat.Queries;

public class GetChatHistoryQuery : IRequest<List<MessageDto>>
{
    public string UserId1 { get; set; }
    public string UserId2 { get; set; }
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 50;
}

public class MessagesByUsersSpecification : Specifications.BaseSpecification<Message>
{
    public MessagesByUsersSpecification(string userId1, string userId2, int page, int size)
        : base(m => !m.IsDeleted &&
                    ((m.SenderId == userId1 && m.ReceiverId == userId2) ||
                     (m.SenderId == userId2 && m.ReceiverId == userId1)))
    {
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(size, 10, 100);

        AddOrderByDescending(m => m.SentAt);
        ApplyPaging((safePage - 1) * safeSize, safeSize);
    }
}

public class MessageDto
{
    public int Id { get; set; }
    public string SenderId { get; set; }
    public string ReceiverId { get; set; }
    public string Content { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string MessageType { get; set; } = "text";
}

public class GetChatHistoryQueryHandler : IRequestHandler<GetChatHistoryQuery, List<MessageDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetChatHistoryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<MessageDto>> Handle(GetChatHistoryQuery request, CancellationToken cancellationToken)
    {
        var spec = new MessagesByUsersSpecification(request.UserId1, request.UserId2, request.Page, request.Size);

        var messages = await _unitOfWork.Repository<Message>().ListAsync(spec);

        return messages
            .OrderBy(m => m.SentAt)
            .Select(m => new MessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            ReceiverId = m.ReceiverId,
            Content = m.Content,
            SentAt = m.SentAt,
            IsRead = m.IsRead,
            FileUrl = m.FileUrl,
            FileName = m.FileName,
            FileType = m.FileType,
            FileSize = m.FileSize,
            MessageType = m.MessageType
        }).ToList();
    }
}
