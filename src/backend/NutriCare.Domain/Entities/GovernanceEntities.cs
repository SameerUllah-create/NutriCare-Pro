namespace NutriCare.Domain.Entities;

public class AuthSession : BaseEntity
{
    public long UserId { get; set; }
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public string RefreshTokenHash { get; set; } = string.Empty;
    public string? PreviousRefreshTokenHash { get; set; }
    public string SecurityStamp { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class DataRightsRequest : BaseEntity
{
    public long UserId { get; set; }
    public string RequestType { get; set; } = "Deletion";
    public string Status { get; set; } = "Pending";
    public string? Reason { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? ReviewedByUserId { get; set; }
}

public class PublicContent : BaseEntity
{
    public string Kind { get; set; } = "FAQ";
    public string Title { get; set; } = string.Empty;
    public string? TitleUrdu { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ContentUrdu { get; set; }
    public bool IsPublished { get; set; }
    public string? ConsentReference { get; set; }
    public string? VerificationReference { get; set; }
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public class ContactInquiry : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = "Received";
    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }
}

public class PatientFeedback : BaseEntity
{
    public long PatientId { get; set; }
    public long? AppointmentId { get; set; }
    public int Rating { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public bool PublicationConsent { get; set; }
    public DateTime? ConsentedAt { get; set; }
    public DateTime? WithdrawnAt { get; set; }
    public long? ModeratedByUserId { get; set; }
    public DateTime? ModeratedAt { get; set; }
}
