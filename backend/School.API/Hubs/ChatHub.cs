using Microsoft.AspNetCore.SignalR;
using MediatR;
using School.Application.Features.Chat.Commands;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Collections.Concurrent;

namespace School.API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMediator _mediator;
    private static readonly ConcurrentDictionary<string, HashSet<string>> _onlineUsers = new();

    public ChatHub(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            
            // Track online status
            _onlineUsers.AddOrUpdate(userId,
                _ => new HashSet<string> { Context.ConnectionId },
                (_, connections) => { connections.Add(Context.ConnectionId); return connections; });
            
            await Clients.All.SendAsync("UserOnline", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            if (_onlineUsers.TryGetValue(userId, out var connections))
            {
                connections.Remove(Context.ConnectionId);
                if (connections.Count == 0)
                {
                    _onlineUsers.TryRemove(userId, out _);
                    await Clients.All.SendAsync("UserOffline", userId);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string receiverId, string content, string? fileUrl = null, 
        string? fileName = null, string? fileType = null, long? fileSize = null, string messageType = "text")
    {
        var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(senderId)) return;

        var command = new SendMessageCommand
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            FileUrl = fileUrl,
            FileName = fileName,
            FileType = fileType,
            FileSize = fileSize,
            MessageType = messageType
        };

        var messageId = await _mediator.Send(command);

        if (messageId > 0)
        {
            var messageData = new
            {
                id = messageId,
                senderId,
                receiverId,
                content,
                fileUrl,
                fileName,
                fileType,
                fileSize,
                messageType,
                sentAt = DateTime.UtcNow,
                isRead = false
            };

            await Clients.Group(receiverId).SendAsync("ReceiveMessage", messageData);
            
            if (receiverId != senderId)
            {
                await Clients.Group(senderId).SendAsync("ReceiveMessage", messageData);
            }
        }
    }

    public async Task Typing(string receiverId)
    {
        var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(senderId))
        {
            await Clients.Group(receiverId).SendAsync("UserTyping", senderId);
        }
    }

    public async Task StopTyping(string receiverId)
    {
        var senderId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(senderId))
        {
            await Clients.Group(receiverId).SendAsync("UserStopTyping", senderId);
        }
    }

    public async Task MarkAsRead(string senderId)
    {
        var currentUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            await Clients.Group(senderId).SendAsync("MessagesRead", currentUserId);
        }
    }

    public List<string> GetOnlineUsers()
    {
        return _onlineUsers.Keys.ToList();
    }

    public async Task SendNotification(string userId, string title, string content)
    {
        await Clients.Group(userId).SendAsync("ReceiveNotification", new { title, content, type = "General" });
    }
}
