namespace NutriCare.API.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NutriCare.API.Hubs;
using NutriCare.Application.Commands.Chat;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Chat;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Patient,Nutritionist")]
public class ChatController : ControllerBase
{
    private static readonly HashSet<string> AllowedFileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IMediator _mediator;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IHubContext<ChatHub> _chatHub;
    private readonly NutriCareDbContext _context;
    private readonly INotificationService _notificationService;

    [HttpGet("contacts")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ChatContactDto>>>> GetContacts(CancellationToken cancellationToken) =>
        Ok(ApiResponse<IEnumerable<ChatContactDto>>.SuccessResponse(await _mediator.Send(new GetChatContactsQuery { CurrentUserId = GetCurrentUserId() }, cancellationToken)));

    public ChatController(
        IMediator mediator,
        IBlobStorageService blobStorageService,
        IHubContext<ChatHub> chatHub,
        NutriCareDbContext context,
        INotificationService notificationService)
    {
        _mediator = mediator;
        _blobStorageService = blobStorageService;
        _chatHub = chatHub;
        _context = context;
        _notificationService = notificationService;
    }

    [HttpPost("messages")]
    public async Task<ActionResult<ApiResponse<ChatMessageDto>>> SendMessage([FromBody] SendChatMessageRequest request)
    {
        var result = await _mediator.Send(new SendChatMessageCommand
        {
            SenderId = GetCurrentUserId(),
            ClientMessageId = Request.Headers["Idempotency-Key"].FirstOrDefault(),
            ReceiverId = request.ReceiverId,
            MessageBody = request.MessageBody,
            MessageType = "Text"
        });

        await _chatHub.Clients.User(request.ReceiverId.ToString()).SendAsync("ReceiveMessage", result);
        await _chatHub.Clients.User(GetCurrentUserId().ToString()).SendAsync("MessageSent", result);

        return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(result, "Message sent successfully"));
    }

    [HttpPost("messages/file")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ChatMessageDto>>> SendFileMessage(
        IFormFile file,
        [FromForm] long receiverId,
        [FromForm] string? messageBody = null)
    {
        ValidateFile(file);
        var senderId = GetCurrentUserId();
        await _mediator.Send(new EnsureChatAccessQuery { CurrentUserId = senderId, OtherUserId = receiverId });
        var clientKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (clientKey != null && await _context.Messages.AnyAsync(m => m.SenderId == senderId && m.ClientMessageId == clientKey && m.DeletedAt == null))
            return Conflict(ApiResponse<ChatMessageDto>.FailureResponse("This upload has already been submitted; refresh conversation history", 409));

        await using var stream = file.OpenReadStream();
        await ValidateFileSignatureAsync(stream, file.ContentType);
        stream.Position = 0;
        var upload = await _blobStorageService.UploadAsync(
            stream,
            file.FileName,
            file.ContentType,
            $"chat/{Math.Min(senderId, receiverId)}-{Math.Max(senderId, receiverId)}");

        ChatMessageDto result;
        try
        {
        result = await _mediator.Send(new SendChatMessageCommand
        {
            SenderId = senderId,
            ClientMessageId = clientKey,
            ReceiverId = receiverId,
            MessageBody = messageBody ?? file.FileName,
            MessageType = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "Image" : "File",
            AttachmentFileName = Path.GetFileName(file.FileName),
            AttachmentContentType = file.ContentType,
            AttachmentFileSizeBytes = file.Length,
            AttachmentBlobContainer = upload.ContainerName,
            AttachmentBlobName = upload.BlobName
        });
        }
        catch
        {
            await _blobStorageService.DeleteAsync(upload.ContainerName, upload.BlobName);
            throw;
        }

        var persistedBlob = await _context.Messages.Where(m => m.Id == result.Id).Select(m => m.AttachmentBlobName).SingleAsync();
        if (persistedBlob != upload.BlobName) await _blobStorageService.DeleteAsync(upload.ContainerName, upload.BlobName);
        await _chatHub.Clients.User(receiverId.ToString()).SendAsync("ReceiveMessage", result);
        await _chatHub.Clients.User(senderId.ToString()).SendAsync("MessageSent", result);

        return Ok(ApiResponse<ChatMessageDto>.SuccessResponse(result, "File message sent successfully"));
    }

    [HttpGet("history/{otherUserId:long}")]
    public async Task<ActionResult<ApiResponse<IEnumerable<ChatMessageDto>>>> GetHistory(
        long otherUserId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var result = await _mediator.Send(new GetChatHistoryQuery
        {
            CurrentUserId = GetCurrentUserId(),
            OtherUserId = otherUserId,
            Skip = skip,
            Take = take
        });

        return Ok(ApiResponse<IEnumerable<ChatMessageDto>>.SuccessResponse(result));
    }

    [HttpGet("unread")]
    public async Task<ActionResult<ApiResponse<UnreadMessageSummaryDto>>> GetUnreadMessages()
    {
        var result = await _mediator.Send(new GetUnreadMessagesQuery { CurrentUserId = GetCurrentUserId() });
        return Ok(ApiResponse<UnreadMessageSummaryDto>.SuccessResponse(result));
    }

    [HttpPut("conversations/{otherUserId:long}/read")]
    public async Task<ActionResult<ApiResponse<int>>> MarkConversationRead(long otherUserId)
    {
        var result = await _mediator.Send(new MarkConversationReadCommand
        {
            CurrentUserId = GetCurrentUserId(),
            OtherUserId = otherUserId
        });

        await _chatHub.Clients.User(otherUserId.ToString()).SendAsync("MessagesRead", GetCurrentUserId(), result, DateTime.UtcNow);
        return Ok(ApiResponse<int>.SuccessResponse(result, "Conversation marked as read"));
    }

    [HttpGet("messages/{messageId:long}/attachment")]
    public async Task<IActionResult> DownloadAttachment(long messageId)
    {
        var result = await _mediator.Send(new DownloadChatAttachmentQuery
        {
            MessageId = messageId,
            CurrentUserId = GetCurrentUserId()
        });

        return File(result.Content, result.ContentType, result.FileName);
    }

    private void ValidateFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("A file is required");
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            throw new ArgumentException("File size must not exceed 10 MB");
        }

        if (!AllowedFileTypes.Contains(file.ContentType))
        {
            throw new ArgumentException("Only PDF, JPEG, PNG, and WEBP files are allowed");
        }
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var validExtension = file.ContentType.ToLowerInvariant() switch { "application/pdf" => extension == ".pdf", "image/jpeg" => extension is ".jpg" or ".jpeg", "image/png" => extension == ".png", "image/webp" => extension == ".webp", _ => false };
        if (!validExtension) throw new ArgumentException("File extension must match the allowed content type");
    }

    private static async Task ValidateFileSignatureAsync(Stream stream, string contentType)
    {
        var bytes = new byte[12];
        var count = await stream.ReadAtLeastAsync(bytes, bytes.Length, throwOnEndOfStream: false);
        var valid = contentType.ToLowerInvariant() switch
        {
            "application/pdf" => count >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            "image/jpeg" => count >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/png" => count >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/webp" => count >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
        if (!valid) throw new ArgumentException("File contents do not match the allowed type");
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : 0;
    }

    private async Task NotifyNewMessageAsync(long receiverId, long messageId)
    {
        var receiver = await _context.Users.FirstOrDefaultAsync(u => u.Id == receiverId && u.DeletedAt == null);
        if (receiver == null)
        {
            return;
        }

        await _notificationService.SendNewMessageAsync(
            new NotificationRecipient(
                receiver.Id,
                receiver.Email ?? string.Empty,
                receiver.PhoneNumber,
                $"{receiver.FirstName} {receiver.LastName}".Trim()),
            GetCurrentUserId(),
            messageId);
    }
}
