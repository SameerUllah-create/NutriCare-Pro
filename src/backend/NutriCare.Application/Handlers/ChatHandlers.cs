namespace NutriCare.Application.Handlers.Chat;

using MediatR;
using NutriCare.Application.Commands.Chat;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Chat;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, ChatMessageDto>
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Patient> _patientRepository;
    private readonly IRepository<Nutritionist> _nutritionistRepository;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IRepository<DietPlan> _dietPlanRepository;
    private readonly IRepository<ClinicalAssignment> _assignments;
    private readonly INotificationOutbox _outbox;
    private readonly IRepository<Message> _messageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SendChatMessageCommandHandler(
        IRepository<User> userRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<DietPlan> dietPlanRepository,
        IRepository<Message> messageRepository,
        IUnitOfWork unitOfWork, IRepository<ClinicalAssignment> assignments, INotificationOutbox outbox)
    {
        _userRepository = userRepository;
        _patientRepository = patientRepository;
        _nutritionistRepository = nutritionistRepository;
        _appointmentRepository = appointmentRepository;
        _dietPlanRepository = dietPlanRepository;
        _messageRepository = messageRepository;
        _unitOfWork = unitOfWork;
        _assignments = assignments;
        _outbox = outbox;
    }

    public async Task<ChatMessageDto> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        if (request.MessageBody.Length > 5000) throw new ValidationException("Messages must not exceed 5000 characters");
        await ChatHandlerHelpers.EnsureCanChatAsync(
            _userRepository,
            _patientRepository,
            _nutritionistRepository,
            _appointmentRepository,
            _dietPlanRepository,
            _assignments,
            request.SenderId,
            request.ReceiverId);

        var key = request.ClientMessageId;
        if (key != null && (key.Length is < 16 or > 100 || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))) throw new ValidationException("Invalid client message key");
        await _unitOfWork.BeginTransactionAsync($"message:{request.SenderId}:{key ?? Guid.NewGuid().ToString("N")}");
        try
        {
        if (key != null)
        {
            var existing = (await _messageRepository.GetAsync(q => q.Where(m => m.SenderId == request.SenderId && m.ClientMessageId == key && m.DeletedAt == null))).SingleOrDefault();
            if (existing != null)
            {
                if (existing.ReceiverId != request.ReceiverId || existing.MessageBody != request.MessageBody || existing.MessageType != request.MessageType || existing.AttachmentFileSizeBytes != request.AttachmentFileSizeBytes)
                    throw new ConflictException("The message key was already used for different content");
                await _unitOfWork.CommitAsync();
                return ChatHandlerHelpers.ToDto(existing);
            }
        }
        var message = new Message
        {
            ClientMessageId = key,
            SenderId = request.SenderId,
            ReceiverId = request.ReceiverId,
            ConversationId = ChatHandlerHelpers.GetConversationId(request.SenderId, request.ReceiverId),
            MessageBody = request.MessageBody,
            MessageType = request.MessageType,
            AttachmentFileName = request.AttachmentFileName,
            AttachmentContentType = request.AttachmentContentType,
            AttachmentFileSizeBytes = request.AttachmentFileSizeBytes,
            AttachmentBlobContainer = request.AttachmentBlobContainer,
            AttachmentBlobName = request.AttachmentBlobName,
            DeliveredAt = null
        };

        await _messageRepository.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();
        var recipient = await _userRepository.GetByIdAsync(request.ReceiverId);
        if (recipient != null) await _outbox.EnqueueAsync(new NotificationRecipient(recipient.Id, recipient.Email ?? "", recipient.PhoneNumber, ""),
            new NotificationRequest("NewMessage", "New secure message", "You have a new secure message. Sign in to read it.", "Message", message.Id, "/messages", ["InApp", "Email"]), $"message:{message.Id}:created", cancellationToken);
        await _unitOfWork.CommitAsync();
        return ChatHandlerHelpers.ToDto(message);
        }
        catch { await _unitOfWork.RollbackAsync(); throw; }
    }
}

public class MarkConversationReadCommandHandler : IRequestHandler<MarkConversationReadCommand, int>
{
    private readonly IMediator _mediator;
    private readonly IRepository<Message> _messageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkConversationReadCommandHandler(IRepository<Message> messageRepository, IUnitOfWork unitOfWork, IMediator mediator)
    {
        _mediator = mediator;
        _messageRepository = messageRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(MarkConversationReadCommand request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new EnsureChatAccessQuery { CurrentUserId = request.CurrentUserId, OtherUserId = request.OtherUserId }, cancellationToken);
        var conversationId = ChatHandlerHelpers.GetConversationId(request.CurrentUserId, request.OtherUserId);
        var messages = await _messageRepository.GetAsync(query => query.Where(m =>
            m.ConversationId == conversationId &&
            m.ReceiverId == request.CurrentUserId &&
            !m.IsRead &&
            m.DeletedAt == null));

        var count = 0;
        foreach (var message in messages)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            message.DeliveredAt ??= message.ReadAt;
            await _messageRepository.UpdateAsync(message);
            count++;
        }

        await _unitOfWork.SaveChangesAsync();
        return count;
    }
}

public class GetChatHistoryQueryHandler : IRequestHandler<GetChatHistoryQuery, IEnumerable<ChatMessageDto>>
{
    private readonly IMediator _mediator;
    private readonly IRepository<Message> _messageRepository;

    public GetChatHistoryQueryHandler(IRepository<Message> messageRepository, IMediator mediator)
    {
        _mediator = mediator;
        _messageRepository = messageRepository;
    }

    public async Task<IEnumerable<ChatMessageDto>> Handle(GetChatHistoryQuery request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new EnsureChatAccessQuery { CurrentUserId = request.CurrentUserId, OtherUserId = request.OtherUserId }, cancellationToken);
        var conversationId = ChatHandlerHelpers.GetConversationId(request.CurrentUserId, request.OtherUserId);
        var messages = await _messageRepository.GetAsync(query => query
            .Where(m => m.ConversationId == conversationId && m.DeletedAt == null)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(Math.Max(0, request.Skip))
            .Take(Math.Clamp(request.Take, 1, 100)));

        return messages.OrderBy(m => m.CreatedAt).Select(ChatHandlerHelpers.ToDto).ToArray();
    }
}

public class GetUnreadMessagesQueryHandler : IRequestHandler<GetUnreadMessagesQuery, UnreadMessageSummaryDto>
{
    private readonly IRepository<Message> _messageRepository;
    private readonly IMediator _mediator;

    public GetUnreadMessagesQueryHandler(IRepository<Message> messageRepository, IMediator mediator)
    {
        _messageRepository = messageRepository;
        _mediator = mediator;
    }

    public async Task<UnreadMessageSummaryDto> Handle(GetUnreadMessagesQuery request, CancellationToken cancellationToken)
    {
        var contacts = (await _mediator.Send(new GetChatContactsQuery { CurrentUserId = request.CurrentUserId }, cancellationToken)).Select(c => c.UserId).ToArray();
        var messages = await _messageRepository.GetAsync(query => query.Where(m =>
            m.ReceiverId == request.CurrentUserId &&
            contacts.Contains(m.SenderId) &&
            !m.IsRead &&
            m.DeletedAt == null));

        var groups = messages
            .GroupBy(m => new { m.ConversationId, m.SenderId })
            .Select(g => new UnreadConversationDto
            {
                ConversationId = g.Key.ConversationId,
                SenderId = g.Key.SenderId,
                UnreadCount = g.Count()
            })
            .ToArray();

        return new UnreadMessageSummaryDto
        {
            TotalUnread = groups.Sum(g => g.UnreadCount),
            Conversations = groups
        };
    }
}

public class DownloadChatAttachmentQueryHandler : IRequestHandler<DownloadChatAttachmentQuery, ChatFileDto>
{
    private readonly IMediator _mediator;
    private readonly IRepository<Message> _messageRepository;
    private readonly IBlobStorageService _blobStorageService;

    public DownloadChatAttachmentQueryHandler(IRepository<Message> messageRepository, IBlobStorageService blobStorageService, IMediator mediator)
    {
        _messageRepository = messageRepository;
        _blobStorageService = blobStorageService;
        _mediator = mediator;
    }

    public async Task<ChatFileDto> Handle(DownloadChatAttachmentQuery request, CancellationToken cancellationToken)
    {
        var message = await ChatHandlerHelpers.GetRequiredAsync(_messageRepository, request.MessageId);
        if (message.SenderId != request.CurrentUserId && message.ReceiverId != request.CurrentUserId)
        {
            throw new ForbiddenAccessException("You can only download files from your own conversations");
        }
        await _mediator.Send(new EnsureChatAccessQuery { CurrentUserId = request.CurrentUserId, OtherUserId = message.SenderId == request.CurrentUserId ? message.ReceiverId : message.SenderId }, cancellationToken);

        if (string.IsNullOrWhiteSpace(message.AttachmentBlobContainer) || string.IsNullOrWhiteSpace(message.AttachmentBlobName))
        {
            throw new EntityNotFoundException("Chat attachment", request.MessageId);
        }

        var file = await _blobStorageService.DownloadAsync(
            message.AttachmentBlobContainer,
            message.AttachmentBlobName,
            message.AttachmentContentType ?? "application/octet-stream",
            cancellationToken);

        return new ChatFileDto
        {
            FileName = message.AttachmentFileName ?? $"attachment-{message.Id}",
            ContentType = file.ContentType,
            Content = file.Content
        };
    }
}

internal static class ChatHandlerHelpers
{
    internal static string GetConversationId(long firstUserId, long secondUserId)
    {
        var min = Math.Min(firstUserId, secondUserId);
        var max = Math.Max(firstUserId, secondUserId);
        return $"{min}:{max}";
    }

    internal static async Task<T> GetRequiredAsync<T>(IRepository<T> repository, long id) where T : BaseEntity
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted)
        {
            throw new EntityNotFoundException(typeof(T).Name, id);
        }

        return entity;
    }

    internal static async Task EnsureCanChatAsync(
        IRepository<User> userRepository,
        IRepository<Patient> patientRepository,
        IRepository<Nutritionist> nutritionistRepository,
        IRepository<Appointment> appointmentRepository,
        IRepository<DietPlan> dietPlanRepository,
        IRepository<ClinicalAssignment> assignmentRepository,
        long senderId,
        long receiverId)
    {
        var sender = await userRepository.GetByIdAsync(senderId);
        var receiver = await userRepository.GetByIdAsync(receiverId);
        if (sender == null || receiver == null || sender.DeletedAt != null || receiver.DeletedAt != null || !sender.IsActive || !receiver.IsActive || senderId == receiverId)
        {
            throw new ForbiddenAccessException("This conversation is unavailable");
        }

        var senderPatients = await patientRepository.GetAsync(query => query.Where(p => p.UserId == senderId && p.DeletedAt == null));
        var receiverPatients = await patientRepository.GetAsync(query => query.Where(p => p.UserId == receiverId && p.DeletedAt == null));
        var senderNutritionists = await nutritionistRepository.GetAsync(query => query.Where(n => n.UserId == senderId && n.DeletedAt == null));
        var receiverNutritionists = await nutritionistRepository.GetAsync(query => query.Where(n => n.UserId == receiverId && n.DeletedAt == null));

        var patient = senderPatients.FirstOrDefault();
        var nutritionist = receiverNutritionists.FirstOrDefault();
        if (patient == null || nutritionist == null) { patient = receiverPatients.FirstOrDefault(); nutritionist = senderNutritionists.FirstOrDefault(); }
        if (patient == null || nutritionist == null || patient.UserId == nutritionist.UserId)
        {
            throw new ForbiddenAccessException("Chat is allowed only between patients and nutritionists");
        }

        var assignmentHistory = await assignmentRepository.GetAsync(q => q.Where(a => a.PatientId == patient.Id && a.NutritionistId == nutritionist.Id && a.DeletedAt == null));
        if (assignmentHistory.Any())
        {
            var now = DateTime.UtcNow;
            if (assignmentHistory.Any(a => a.StartedAt <= now && (a.EndedAt == null || a.EndedAt > now))) return;
            throw new ForbiddenAccessException("This care relationship is inactive");
        }

        var appointments = await appointmentRepository.GetAsync(query => query.Where(a =>
            a.PatientId == patient.Id &&
            a.NutritionistId == nutritionist.Id &&
            a.DeletedAt == null && (a.Status == "Confirmed" || a.Status == "Approved" || a.Status == "Completed")));
        if (appointments.Any())
        {
            return;
        }

        var plans = await dietPlanRepository.GetAsync(query => query.Where(d =>
            d.PatientId == patient.Id &&
            d.NutritionistId == nutritionist.Id &&
            d.DeletedAt == null && d.PublishedAt != null));
        if (plans.Any())
        {
            return;
        }

        throw new ForbiddenAccessException("Chat requires an appointment or diet plan relationship");
    }

    internal static ChatMessageDto ToDto(Message message)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            ConversationId = message.ConversationId,
            MessageBody = message.MessageBody,
            MessageType = message.MessageType,
            IsRead = message.IsRead,
            ReadAt = message.ReadAt,
            DeliveredAt = message.DeliveredAt,
            AttachmentFileName = message.AttachmentFileName,
            AttachmentContentType = message.AttachmentContentType,
            AttachmentFileSizeBytes = message.AttachmentFileSizeBytes,
            CreatedAt = message.CreatedAt,
            UpdatedAt = message.UpdatedAt
        };
    }
}
