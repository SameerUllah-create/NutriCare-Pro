namespace NutriCare.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NutriCareDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IClinicalAccessService _clinicalAccess;

    public NotificationsController(NutriCareDbContext context, INotificationService notificationService, IClinicalAccessService clinicalAccess)
    {
        _context = context;
        _notificationService = notificationService;
        _clinicalAccess = clinicalAccess;
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<ApiResponse<NotificationPreferences>>> GetPreferences(CancellationToken cancellationToken)
    {
        var user = await _context.Users.SingleAsync(u => u.Id == GetCurrentUserId(), cancellationToken);
        return Ok(ApiResponse<NotificationPreferences>.SuccessResponse(new(user.EmailNotificationsEnabled, user.SmsNotificationsEnabled, user.InAppNotificationsEnabled)));
    }

    [HttpPut("preferences")]
    public async Task<ActionResult<ApiResponse<NotificationPreferences>>> UpdatePreferences([FromBody] NotificationPreferences request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.SingleAsync(u => u.Id == GetCurrentUserId(), cancellationToken);
        user.EmailNotificationsEnabled = request.Email;
        user.SmsNotificationsEnabled = request.Sms;
        user.InAppNotificationsEnabled = request.InApp;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<NotificationPreferences>.SuccessResponse(request, "Notification preferences saved"));
    }

    [HttpGet("delivery-status")]
    public async Task<ActionResult<ApiResponse<object>>> GetDeliveryStatus(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _context.Notifications.Where(n => n.UserId == userId && n.DeletedAt == null).OrderByDescending(n => n.CreatedAt).Take(100)
            .Select(n => new { n.Id, n.Type, n.Channel, n.Status, n.DeliveryStatus, n.AttemptCount, n.SentAt, n.NextAttemptAt, n.LastError }).ToListAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost("{id:long}/retry")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> RetryDelivery(long id, CancellationToken cancellationToken)
    {
        var notification = await _context.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.DeletedAt == null, cancellationToken);
        if (notification == null) return NotFound();
        if (notification.Status != "Failed") return Conflict(ApiResponse<object>.FailureResponse("Only failed notifications can be retried", 409));
        notification.Status = "Pending"; notification.DeliveryStatus = "Pending"; notification.AttemptCount = 0; notification.NextAttemptAt = DateTime.UtcNow; notification.LastError = null;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(new { notification.Id }, "Notification queued for retry"));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<NotificationDto>>>> GetMyNotifications(
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var query = _context.Notifications
            .Where(n => n.DeletedAt == null && n.UserId == userId && n.Channel == "InApp");

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 100))
            .Select(n => ToDto(n))
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IEnumerable<NotificationDto>>.SuccessResponse(notifications));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<int>>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var count = await _context.Notifications.CountAsync(n =>
            n.DeletedAt == null &&
            n.UserId == userId &&
            n.Channel == "InApp" &&
            !n.IsRead,
            cancellationToken);

        return Ok(ApiResponse<int>.SuccessResponse(count));
    }

    [HttpPut("{id:long}/read")]
    public async Task<ActionResult<ApiResponse<NotificationDto>>> MarkRead(long id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var notification = await _context.Notifications.FirstOrDefaultAsync(n =>
            n.Id == id &&
            n.UserId == userId &&
            n.DeletedAt == null,
            cancellationToken);

        if (notification == null)
        {
            return NotFound(ApiResponse<NotificationDto>.FailureResponse("Notification was not found", 404));
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<NotificationDto>.SuccessResponse(ToDto(notification), "Notification marked as read"));
    }

    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse<int>>> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var notifications = await _context.Notifications
            .Where(n => n.DeletedAt == null && n.UserId == userId && n.Channel == "InApp" && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<int>.SuccessResponse(notifications.Count, "Notifications marked as read"));
    }

    [HttpPost("appointment-reminders")]
    [Authorize(Roles = "Admin,Nutritionist")]
    public async Task<ActionResult<ApiResponse<IEnumerable<NotificationDispatchResult>>>> TriggerAppointmentReminder(
        [FromBody] TriggerAppointmentReminderRequest request,
        CancellationToken cancellationToken)
    {
        await _clinicalAccess.EnsureAppointmentAccessAsync(request.AppointmentId, cancellationToken);
        var appointment = await _context.Appointments
            .Include(a => a.Patient)
            .ThenInclude(p => p!.User)
            .Include(a => a.Nutritionist)
            .ThenInclude(n => n!.User)
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId && a.DeletedAt == null, cancellationToken);

        if (appointment == null)
        {
            return NotFound(ApiResponse<IEnumerable<NotificationDispatchResult>>.FailureResponse("Appointment was not found", 404));
        }
        if (appointment.Status is not ("Confirmed" or "Approved") || appointment.AppointmentDateTime <= DateTime.UtcNow)
            return Conflict(ApiResponse<IEnumerable<NotificationDispatchResult>>.FailureResponse("Only upcoming confirmed appointments can have reminders", 409));

        if (GetCurrentRole() == "Nutritionist" && appointment.Nutritionist?.UserId != GetCurrentUserId())
        {
            return Forbid();
        }

        var recipients = new[]
        {
            ToRecipient(appointment.Patient?.User),
            ToRecipient(appointment.Nutritionist?.User)
        }.Where(r => r != null).Cast<NotificationRecipient>().ToArray();

        var results = new List<NotificationDispatchResult>();
        foreach (var recipient in recipients)
        {
            results.AddRange(await _notificationService.SendAppointmentReminderAsync(
                recipient,
                appointment.Id,
                appointment.AppointmentDateTime,
                cancellationToken));
        }

        return Ok(ApiResponse<IEnumerable<NotificationDispatchResult>>.SuccessResponse(results, "Appointment reminders dispatched"));
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : 0;
    }

    private string GetCurrentRole()
    {
        return User.FindFirst("role")?.Value ?? string.Empty;
    }

    private static NotificationRecipient? ToRecipient(User? user)
    {
        return user == null
            ? null
            : new NotificationRecipient(
                user.Id,
                user.Email ?? string.Empty,
                user.PhoneNumber,
                $"{user.FirstName} {user.LastName}".Trim());
    }

    private static NotificationDto ToDto(Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            CreatedAt = notification.CreatedAt,
            UpdatedAt = notification.UpdatedAt,
            UserId = notification.UserId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            Icon = notification.Icon,
            RelatedToType = notification.RelatedToType,
            RelatedToId = notification.RelatedToId,
            ActionUrl = notification.ActionUrl,
            IsRead = notification.IsRead,
            ReadAt = notification.ReadAt,
            Channel = notification.Channel,
            Status = notification.Status,
            SentAt = notification.SentAt
        };
    }
}

public record NotificationPreferences(bool Email, bool Sms, bool InApp);
