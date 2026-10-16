namespace NutriCare.Domain.Interfaces;

/// <summary>
/// Repository interface for data access
/// </summary>
public interface IRepository<T> where T : class
{
    Task ReloadAsync(T entity) => Task.CompletedTask;
    Task<T?> GetByIdAsync(long id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> GetAsync(Func<IQueryable<T>, IQueryable<T>>? predicate = null);
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(long id);
    Task<bool> ExistsAsync(long id);
}

/// <summary>
/// Unit of Work interface for transaction management
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync();
    Task<bool> BeginTransactionAsync(string? resource = null);
    Task<bool> CommitAsync();
    Task<bool> RollbackAsync();
}

/// <summary>
/// JWT token service interface
/// </summary>
public interface IJwtTokenService
{
    string GenerateAccessToken(long userId, string email, string role, string? securityStamp = null, string? sessionId = null);
    string GenerateRefreshToken();
    Task<(string AccessToken, string RefreshToken)> GenerateTokenPairAsync(long userId, string email, string role, string? securityStamp = null, string? sessionId = null);
    bool ValidateToken(string token);
    string? GetClaimValue(string token, string claimType);
}

/// <summary>
/// Password service interface
/// </summary>
public interface IPasswordService
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

/// <summary>
/// Email service interface
/// </summary>
public interface IEmailService
{
    Task<bool> SendEmailAsync(string email, string subject, string message);
    Task<bool> SendVerificationEmailAsync(string email, string verificationLink);
    Task<bool> SendPasswordResetEmailAsync(string email, string resetLink);
}

public record BlobUploadResult(string ContainerName, string BlobName, string StoredFileName, Uri? Uri);

public record BlobDownloadResult(Stream Content, string ContentType);

/// <summary>
/// Secure private file storage abstraction.
/// </summary>
public interface IBlobStorageService
{
    Task<BlobUploadResult> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default);
    Task<BlobDownloadResult> DownloadAsync(string containerName, string blobName, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string containerName, string blobName, CancellationToken cancellationToken = default);
}

public record PaymentGatewayInitializeRequest(
    decimal Amount,
    string Currency,
    string Description,
    string TransactionId,
    string SuccessUrl,
    string CancelUrl,
    string? IdempotencyKey = null);

public record PaymentGatewayInitializeResult(
    string ProviderPaymentId,
    string? CheckoutUrl,
    string Status,
    string RawResponse,
    string? PaymentIntentId = null);

public record PaymentGatewayRefundResult(
    string ProviderRefundId,
    string Status,
    string RawResponse);

public record PaymentGatewayWebhookResult(
    string TransactionId,
    string? ProviderPaymentId,
    string Status,
    string RawResponse,
    string EventId = "",
    string EventType = "",
    decimal? Amount = null,
    string? Currency = null,
    DateTime? OccurredAt = null,
    string? ProviderSessionId = null,
    string? ProviderRefundId = null);

public interface IPaymentGatewayService
{
    Task<PaymentGatewayInitializeResult> InitializePaymentAsync(string provider, PaymentGatewayInitializeRequest request, CancellationToken cancellationToken = default);
    Task<PaymentGatewayRefundResult> RefundAsync(string provider, string providerPaymentId, decimal amount, string currency, string? reason, CancellationToken cancellationToken = default, string? idempotencyKey = null);
    Task<PaymentGatewayWebhookResult> ParseWebhookAsync(string provider, string payload, string? signature, CancellationToken cancellationToken = default);
}

public record VideoMeetingCreateRequest(
    string Topic,
    DateTime StartTime,
    DateTime EndTime,
    string TimeZone,
    string? Description);

public record VideoMeetingCreateResult(
    string Provider,
    string ProviderMeetingId,
    string MeetingUrl,
    string? HostUrl,
    string RawResponse);

public interface IVideoMeetingService
{
    Task<VideoMeetingCreateResult> CreateMeetingAsync(string provider, VideoMeetingCreateRequest request, CancellationToken cancellationToken = default);
}

public record NotificationRecipient(
    long UserId,
    string Email,
    string? PhoneNumber,
    string FullName);

public record NotificationRequest(
    string EventType,
    string Title,
    string Message,
    string? RelatedToType,
    long? RelatedToId,
    string? ActionUrl,
    IReadOnlyCollection<string> Channels);

public record NotificationDispatchResult(
    long UserId,
    string Channel,
    bool IsSuccess,
    string? ErrorMessage);

public interface ISmsService
{
    Task<bool> SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<IReadOnlyCollection<NotificationDispatchResult>> SendAsync(
        NotificationRecipient recipient,
        NotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NotificationDispatchResult>> SendAppointmentBookedAsync(
        NotificationRecipient patient,
        NotificationRecipient nutritionist,
        long appointmentId,
        DateTime appointmentDateTime,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NotificationDispatchResult>> SendAppointmentReminderAsync(
        NotificationRecipient recipient,
        long appointmentId,
        DateTime appointmentDateTime,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NotificationDispatchResult>> SendDietPlanAssignedAsync(
        NotificationRecipient patient,
        long dietPlanId,
        string planTitle,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NotificationDispatchResult>> SendPaymentReceivedAsync(
        NotificationRecipient patient,
        long paymentId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<NotificationDispatchResult>> SendNewMessageAsync(
        NotificationRecipient recipient,
        long senderId,
        long messageId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Current user service interface
/// </summary>
public interface ICurrentUserService
{
    bool CanAccessClinicalData { get; }
    long UserId { get; }
    string Email { get; }
    string Role { get; }
    bool IsAuthenticated { get; }
}
