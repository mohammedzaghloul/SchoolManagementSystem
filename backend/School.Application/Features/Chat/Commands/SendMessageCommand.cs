using MediatR;
using School.Application.Interfaces;
using School.Domain.Entities;

namespace School.Application.Features.Chat.Commands;

public class SendMessageCommand : IRequest<int>
{
    public string SenderId { get; set; } = null!;
    public string ReceiverId { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public string MessageType { get; set; } = "text";
}

public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;

    public SendMessageCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            SenderId = request.SenderId,
            ReceiverId = request.ReceiverId,
            Content = request.Content,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            IsDeleted = false,
            FileUrl = request.FileUrl,
            FileName = request.FileName,
            FileType = request.FileType,
            FileSize = request.FileSize,
            MessageType = request.MessageType
        };

        await _unitOfWork.Repository<Message>().AddAsync(message);
        var saved = await _unitOfWork.CompleteAsync();
        return saved > 0 ? message.Id : 0;
    }
}
