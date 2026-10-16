namespace NutriCare.Application.Commands.Chat;

using MediatR;
using NutriCare.Application.DTOs;

public class SendChatMessageCommand : IRequest<ChatMessageDto>
{
    public string? ClientMessageId { get; set; }
    public long SenderId { get; set; }
    public long ReceiverId { get; set; }
    public string MessageBody { get; set; } = string.Empty;
    public string MessageType { get; set; } = "Text";
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public long? AttachmentFileSizeBytes { get; set; }
    public string? AttachmentBlobContainer { get; set; }
    public string? AttachmentBlobName { get; set; }
}

public class MarkConversationReadCommand : IRequest<int>
{
    public long CurrentUserId { get; set; }
    public long OtherUserId { get; set; }
}
