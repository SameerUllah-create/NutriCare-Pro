namespace NutriCare.API.Hubs;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NutriCare.Application.Commands.Chat;
using NutriCare.Application.Queries.Chat;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;

/// <summary>Authenticated, relationship-scoped direct messaging. System events are server-only.</summary>
[Authorize(Roles = "Patient,Nutritionist")]
public class ChatHub(IMediator mediator, IChatPresenceTracker presence, IRepository<Appointment> appointments,
    IRepository<Patient> patients, IRepository<Nutritionist> nutritionists) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId();
        var contacts = (await mediator.Send(new GetChatContactsQuery { CurrentUserId = userId }, Context.ConnectionAborted)).ToArray();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        if (await presence.UserConnectedAsync(userId, Context.ConnectionId))
            await Clients.Users(contacts.Select(c => c.UserId.ToString()).ToArray()).SendAsync("UserOnline", userId, DateTime.UtcNow);
        await Clients.Caller.SendAsync("OnlineUsers", contacts.Where(c => presence.IsOnline(c.UserId)).Select(c => c.UserId).ToArray());
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId();
        if (await presence.UserDisconnectedAsync(userId, Context.ConnectionId))
        {
            var contacts = await mediator.Send(new GetChatContactsQuery { CurrentUserId = userId });
            await Clients.Users(contacts.Select(c => c.UserId.ToString()).ToArray()).SendAsync("UserOffline", userId, DateTime.UtcNow);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessageToUser(long recipientId, string message, string? clientMessageId = null)
    {
        var saved = await mediator.Send(new SendChatMessageCommand { SenderId = CurrentUserId(), ReceiverId = recipientId, MessageBody = message, MessageType = "Text", ClientMessageId = clientMessageId }, Context.ConnectionAborted);
        await Clients.User(recipientId.ToString()).SendAsync("ReceiveMessage", saved);
        await Clients.User(CurrentUserId().ToString()).SendAsync("MessageSent", saved);
    }

    public async Task JoinAppointmentGroup(long appointmentId)
    {
        await EnsureAppointmentParticipantAsync(appointmentId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"appointment-{appointmentId}");
    }

    public Task LeaveAppointmentGroup(long appointmentId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"appointment-{appointmentId}");

    public async Task SendTypingIndicator(long recipientId)
    {
        await EnsureContactAsync(recipientId);
        await Clients.User(recipientId.ToString()).SendAsync("UserTyping", CurrentUserId(), DateTime.UtcNow);
    }

    public async Task StopTyping(long recipientId)
    {
        await EnsureContactAsync(recipientId);
        await Clients.User(recipientId.ToString()).SendAsync("UserStoppedTyping", CurrentUserId(), DateTime.UtcNow);
    }

    public async Task MarkConversationRead(long otherUserId)
    {
        await EnsureContactAsync(otherUserId);
        var count = await mediator.Send(new MarkConversationReadCommand { CurrentUserId = CurrentUserId(), OtherUserId = otherUserId }, Context.ConnectionAborted);
        await Clients.User(otherUserId.ToString()).SendAsync("MessagesRead", CurrentUserId(), count, DateTime.UtcNow);
        await Clients.Caller.SendAsync("ConversationRead", otherUserId, count, DateTime.UtcNow);
    }

    public async Task GetOnlineStatus(long userId)
    {
        await EnsureContactAsync(userId);
        await Clients.Caller.SendAsync("UserOnlineStatus", userId, presence.IsOnline(userId), DateTime.UtcNow);
    }

    private Task<bool> EnsureContactAsync(long otherUserId) => mediator.Send(new EnsureChatAccessQuery { CurrentUserId = CurrentUserId(), OtherUserId = otherUserId }, Context.ConnectionAborted);

    private async Task EnsureAppointmentParticipantAsync(long id)
    {
        var appointment = await appointments.GetByIdAsync(id);
        if (appointment == null || appointment.DeletedAt != null || appointment.Status is "Cancelled" or "Rejected")
            throw new HubException("Appointment is unavailable");
        var patient = await patients.GetByIdAsync(appointment.PatientId);
        var clinician = await nutritionists.GetByIdAsync(appointment.NutritionistId);
        if (patient?.UserId != CurrentUserId() && clinician?.UserId != CurrentUserId())
            throw new HubException("Appointment access denied");
    }

    private long CurrentUserId() => long.TryParse(Context.User?.FindFirst("uid")?.Value, out var id) && id > 0 ? id : throw new HubException("Authentication required");
}

/// <summary>Only the server publishes notification events via IHubContext.</summary>
[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!long.TryParse(Context.User?.FindFirst("uid")?.Value, out var id) || id <= 0) throw new HubException("Authentication required");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{id}");
        await base.OnConnectedAsync();
    }
}
