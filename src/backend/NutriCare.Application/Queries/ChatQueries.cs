namespace NutriCare.Application.Queries.Chat;

using MediatR;
using NutriCare.Application.DTOs;

public class GetChatHistoryQuery : IRequest<IEnumerable<ChatMessageDto>>
{
    public long CurrentUserId { get; set; }
    public long OtherUserId { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
}

public class GetUnreadMessagesQuery : IRequest<UnreadMessageSummaryDto>
{
    public long CurrentUserId { get; set; }
}

public class DownloadChatAttachmentQuery : IRequest<ChatFileDto>
{
    public long MessageId { get; set; }
    public long CurrentUserId { get; set; }
}

public class EnsureChatAccessQuery : IRequest<bool>
{
    public long CurrentUserId { get; set; }
    public long OtherUserId { get; set; }
}

public class GetChatContactsQuery : IRequest<IEnumerable<ChatContactDto>>
{
    public long CurrentUserId { get; set; }
}
