namespace School.Domain.Entities;

public class Message : BaseEntity
{
    public string SenderId { get; set; } = null!; // ApplicationUserId
    public string ReceiverId { get; set; } = null!; // ApplicationUserId
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public bool IsDeleted { get; set; }
    
    // File/Media support
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; } // image, audio, pdf, document
    public long? FileSize { get; set; }
    
    // Message type: text, image, audio, file
    public string MessageType { get; set; } = "text";
}
